using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WorkshopStudio.Core;

/// <summary>
/// A real Windows pseudoconsole lets SteamCMD use its console password/Guard input APIs.
/// Redirected stdin alone does not provide console semantics. The reader must keep draining
/// output while ClosePseudoConsole runs, as required by the Windows Console API.
/// </summary>
internal sealed class SteamCmdTransport : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _input;
    private nint _console;
    private Task? _closeTask;
    private readonly object _closeSync = new();
    public IReadOnlyList<StreamReader> Readers { get; }
    public string Name { get; }
    public int ExitCode => _process.ExitCode;

    private SteamCmdTransport(Process process, StreamWriter input, IReadOnlyList<StreamReader> readers, nint console, string name)
    { _process = process; _input = input; Readers = readers; _console = console; Name = name; }

    public static SteamCmdTransport Start(string exe, IReadOnlyList<string> arguments)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
                throw new PlatformNotSupportedException("Der integrierte SteamCMD-Login benötigt Windows 10 ab 1809 oder Windows 11. Alternativ die originale SteamCMD-Konsole verwenden.");
            return StartWindows(exe, arguments);
        }
        // Also allows the protocol integration tests to run on a non-Windows build agent.
        var start = new ProcessStartInfo(exe) {
            WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        var process = Process.Start(start) ?? throw new IOException("SteamCMD konnte nicht gestartet werden.");
        process.StandardInput.NewLine = "\n";
        return new(process, process.StandardInput, [process.StandardOutput, process.StandardError], 0, "Prozess-Pipes");
    }

    public async Task SendLineAsync(string value, CancellationToken ct)
    {
        await _input.WriteLineAsync(value.AsMemory(), ct).ConfigureAwait(false);
        await _input.FlushAsync(ct).ConfigureAwait(false);
    }
    public Task WaitForExitAsync(CancellationToken ct) => _process.WaitForExitAsync(ct);
    public void Kill()
    {
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }
    public Task CloseConsoleAsync()
    {
        lock (_closeSync)
        {
            if (_closeTask is not null) return _closeTask;
            var handle = _console; _console = 0;
            return _closeTask = handle == 0 ? Task.CompletedTask : Task.Run(() => Native.ClosePseudoConsole(handle));
        }
    }
    public async ValueTask DisposeAsync()
    {
        Kill();
        await CloseConsoleAsync().ConfigureAwait(false);
        _input.Dispose();
        foreach (var reader in Readers) reader.Dispose();
        _process.Dispose();
    }

    private static SteamCmdTransport StartWindows(string exe, IReadOnlyList<string> arguments)
    {
        SafeFileHandle? inputRead = null, inputWrite = null, outputRead = null, outputWrite = null;
        nint console = 0, attributeList = 0;
        bool attributesReady = false;
        Process? process = null;
        try
        {
            if (!Native.CreatePipe(out inputRead, out inputWrite, 0, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!Native.CreatePipe(out outputRead, out outputWrite, 0, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var hr = Native.CreatePseudoConsole(new Native.Coord { X = 2048, Y = 80 }, inputRead, outputWrite, 0, out console);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            nuint bytes = 0;
            Native.InitializeProcThreadAttributeList(0, 1, 0, ref bytes);
            attributeList = Marshal.AllocHGlobal(checked((nint)bytes));
            if (!Native.InitializeProcThreadAttributeList(attributeList, 1, 0, ref bytes)) throw new Win32Exception(Marshal.GetLastWin32Error());
            attributesReady = true;
            if (!Native.UpdateProcThreadAttribute(attributeList, 0, (nint)0x00020016, console, (nuint)IntPtr.Size, 0, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var startup = new Native.StartupInfoEx { AttributeList = attributeList };
            startup.StartupInfo.cb = Marshal.SizeOf<Native.StartupInfoEx>();
            // Without STARTF_USESTDHANDLES Windows may duplicate redirected parent
            // stdin/stdout into a console child, bypassing ConPTY entirely.
            startup.StartupInfo.flags = 0x00000100;
            var command = new StringBuilder(string.Join(" ", new[] { exe }.Concat(arguments).Select(WindowsCommandLine.Quote)));
            // Start suspended so the .NET process handle is acquired before even a short-lived child exits.
            if (!Native.CreateProcessW(exe, command, 0, 0, false, 0x00080000 | 0x00000004, 0,
                    Path.GetDirectoryName(exe)!, ref startup, out var info))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                process = Process.GetProcessById(info.ProcessId);
                _ = process.Handle;
                if (Native.ResumeThread(info.Thread) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { Native.CloseHandle(info.Thread); Native.CloseHandle(info.Process); }
            inputRead.Dispose(); inputRead = null; outputWrite.Dispose(); outputWrite = null;
            var input = new StreamWriter(new FileStream(inputWrite, FileAccess.Write, 4096, isAsync: false), new UTF8Encoding(false)) { NewLine = "\r" };
            inputWrite = null;
            var output = new StreamReader(new FileStream(outputRead, FileAccess.Read, 4096, isAsync: false), new UTF8Encoding(false), false, 4096);
            outputRead = null;
            return new(process, input, [output], console, "Windows-Pseudokonsole (ConPTY)");
        }
        catch
        {
            try { if (process is not null && !process.HasExited) process.Kill(true); } catch { }
            process?.Dispose();
            // No output reader exists after failed startup. Close its pipe before the console to avoid a teardown deadlock.
            outputRead?.Dispose(); outputRead = null;
            if (console != 0) Native.ClosePseudoConsole(console);
            throw;
        }
        finally
        {
            if (attributesReady) Native.DeleteProcThreadAttributeList(attributeList);
            if (attributeList != 0) Marshal.FreeHGlobal(attributeList);
            inputRead?.Dispose(); inputWrite?.Dispose(); outputRead?.Dispose(); outputWrite?.Dispose();
        }
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Coord { public short X, Y; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct StartupInfo
        {
            public int cb; public nint reserved, desktop, title;
            public int x, y, xSize, ySize, xCountChars, yCountChars, fillAttribute, flags;
            public short showWindow, reserved2; public nint reservedPointer, stdInput, stdOutput, stdError;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct StartupInfoEx { public StartupInfo StartupInfo; public nint AttributeList; }
        [StructLayout(LayoutKind.Sequential)] internal struct ProcessInfo { public nint Process, Thread; public int ProcessId, ThreadId; }
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, nint attributes, uint size);
        [DllImport("kernel32.dll")] internal static extern int CreatePseudoConsole(Coord size, SafeFileHandle input, SafeFileHandle output, uint flags, out nint console);
        [DllImport("kernel32.dll")] internal static extern void ClosePseudoConsole(nint console);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitializeProcThreadAttributeList(nint list, int count, int flags, ref nuint size);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateProcThreadAttribute(nint list, uint flags, nint attribute, nint value, nuint size, nint previous, nint returnedSize);
        [DllImport("kernel32.dll")] internal static extern void DeleteProcThreadAttributeList(nint list);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcessW(string app, StringBuilder command, nint processAttributes, nint threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, nint environment, string directory, ref StartupInfoEx startup, out ProcessInfo info);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint ResumeThread(nint thread);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CloseHandle(nint handle);
    }
}
