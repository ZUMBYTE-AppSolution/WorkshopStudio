using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace WorkshopStudio.Core;

public enum SecretKind { Password, SteamGuard }
public sealed record SteamCmdResult(int ExitCode, bool LoggedIn, bool Published, string Output, ulong? SteamId64 = null);

public sealed class SecretRedactor
{
    private readonly List<string> _secrets = [];
    private readonly object _sync = new();
    public void Add(string? value) { if (!string.IsNullOrEmpty(value)) lock (_sync) _secrets.Add(value); }
    public string Redact(string input)
    {
        lock (_sync) foreach (var secret in _secrets.OrderByDescending(s=>s.Length)) input = input.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        return Regex.Replace(input, @"(?i)(access_token|refresh_token|sessionid|steamLoginSecure|password)\s*[:=]\s*[^\s]+", "$1=[REDACTED]");
    }
}

/// <summary>Redacts complete logical lines, even when a secret crosses process read boundaries.</summary>
public sealed class SafeLogBuffer
{
    private readonly StringBuilder _pending = new();
    private bool _discardLine;
    private readonly SecretRedactor _redactor;
    private readonly Action<string> _emit;
    public SafeLogBuffer(SecretRedactor redactor, Action<string> emit) { _redactor=redactor; _emit=emit; }
    public void Append(string chunk)
    {
        foreach(var c in chunk) {
            if(c=='\n') { if(!_discardLine)Flush(); _discardLine=false; continue; }
            if(_discardLine)continue;
            _pending.Append(c);
            if(_pending.Length>65536) { _pending.Clear(); _discardLine=true; _emit("[Überlange Protokollzeile aus Sicherheitsgründen verworfen]"); }
        }
    }
    public void Flush()
    {
        if(_pending.Length==0)return;
        var line=_pending.ToString().TrimEnd('\r'); _pending.Clear();
        _emit(_redactor.Redact(line));
    }
}

public sealed class SteamCmdRunner
{
    private readonly SemaphoreSlim _exclusive = new(1, 1);
    public bool IsBusy { get; private set; }

    public static void ValidateUserName(string username)
    {
        if (!Regex.IsMatch(username, @"^[A-Za-z0-9_][A-Za-z0-9_.@-]{0,63}$"))
            throw new InvalidDataException("Steam-Loginname ist leer oder ungültig. Den Steam-Kontonamen verwenden, nicht den Anzeigenamen.");
    }
    public static void EnsureNoExternalInstance(string exe)
    {
        foreach (var running in Process.GetProcessesByName("steamcmd"))
            using (running)
            {
                string? other = null;
                try { other = running.MainModule?.FileName; }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
                if (other is not null && Path.GetFullPath(other).Equals(Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Diese SteamCMD-Installation läuft bereits. Externe Konsole mit quit beenden, bevor Studio sie verwendet.");
            }
    }
    public static IReadOnlyList<string> CreateArguments(string username, string? vdfPath, bool includeInfo = false)
    {
        ValidateUserName(username);
        if (username.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Für Workshop-Veröffentlichungen ein eigenes Steam-Konto statt anonymous auswählen.");
        // Prefer Valve's cached session. Password and Guard codes NEVER appear in arguments or scripts.
        var args = new List<string> { "+@ShutdownOnFailedCommand", "1", "+@NoPromptForPassword", "0", "+login", username };
        if (vdfPath is not null) { args.Add("+workshop_build_item"); args.Add(Path.GetFullPath(vdfPath)); }
        if (includeInfo) args.Add("+info");
        args.Add("+quit");
        return args;
    }

    public async Task<SteamCmdResult> RunAsync(string exe, string username, string? vdfPath, TimeSpan timeout,
        Func<SecretKind, CancellationToken, Task<string?>> requestSecret, IProgress<string>? log, CancellationToken ct,
        IProgress<SteamLoginSignal>? authentication = null)
    {
        ValidateUserName(username);
        if (!File.Exists(exe)) throw new FileNotFoundException("SteamCMD fehlt. Unter Einstellungen installieren oder Pfad auswählen.", exe);
        if (vdfPath is not null && !File.Exists(vdfPath)) throw new FileNotFoundException("Upload-Konfiguration fehlt.", vdfPath);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        await _exclusive.WaitAsync(ct).ConfigureAwait(false);
        IsBusy = true;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        SteamCmdTransport? transport = null;
        Task? exitTask = null, readTask = null;
        var shuttingDown = false;
        var channel = Channel.CreateUnbounded<(int Reader, string Text)>();
        var redactor = new SecretRedactor();
        var output = new StringBuilder();
        try
        {
            EnsureNoExternalInstance(exe);
            log?.Report(vdfPath is null ? "SteamCMD: Sitzung prüfen, bei Bedarf neu anmelden." : "SteamCMD: Workshop-Upload wird gestartet.");
            transport = SteamCmdTransport.Start(Path.GetFullPath(exe), CreateArguments(username, vdfPath, vdfPath is null));
            log?.Report("Anbindung: " + transport.Name);
            using var kill = deadline.Token.Register(transport.Kill);
            var active = transport;
            // Read on dedicated workers: even while a password dialog is open or the console is closing,
            // output must continue to drain. Never await a UI prompt on a native pipe reader thread.
            readTask = Task.WhenAll(active.Readers.Select((reader, index) => Task.Run(async () =>
            {
                var buffer = new byte[2048];
                var chars = new char[4096];
                var decoder = Encoding.UTF8.GetDecoder();
                try
                {
                    int length;
                    // Read raw pipe bytes. StreamReader may wait to fill its character buffer
                    // while SteamCMD has printed an unterminated password prompt.
                    while ((length = await reader.BaseStream.ReadAsync(buffer.AsMemory(), deadline.Token).ConfigureAwait(false)) > 0) {
                        var count = decoder.GetChars(buffer, 0, length, chars, 0, false);
                        if (count > 0) channel.Writer.TryWrite((index, new string(chars, 0, count)));
                    }
                    var trailing = decoder.GetChars([], 0, 0, chars, 0, true);
                    if (trailing > 0) channel.Writer.TryWrite((index, new string(chars, 0, trailing)));
                }
                catch (IOException) when (shuttingDown || deadline.IsCancellationRequested) { }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
            })));
            var complete = readTask.ContinueWith(task => channel.Writer.TryComplete(task.Exception?.GetBaseException()), TaskScheduler.Default);
            exitTask = Task.Run(async () =>
            {
                try { await active.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
                finally { await active.CloseConsoleAsync().ConfigureAwait(false); }
            });
            var filters = active.Readers.Select(_ => new TerminalTextFilter()).ToArray();
            var detectors = active.Readers.Select(_ => new SteamCmdPromptDetector()).ToArray();
            var buffers = active.Readers.Select(_ => new SafeLogBuffer(redactor, clean =>
            {
                output.AppendLine(clean);
                if (output.Length > 2_000_000) output.Remove(0, output.Length - 2_000_000);
                log?.Report(clean);
            })).ToArray();
            var passwords = 0;
            var guards = 0;
            await foreach (var part in channel.Reader.ReadAllAsync(deadline.Token).ConfigureAwait(false))
            {
                var text = filters[part.Reader].Append(part.Text);
                buffers[part.Reader].Append(text);
                var signal = detectors[part.Reader].Append(text);
                if (signal is null) continue;
                authentication?.Report(signal.Value);
                if (signal == SteamLoginSignal.MobileApproval)
                {
                    buffers[part.Reader].Flush();
                    log?.Report("Steam Guard: Anmeldung in der Steam-App bestätigen. Es wird kein Code automatisch eingegeben.");
                    continue;
                }
                if (signal == SteamLoginSignal.Authenticated) continue;
                var kind = signal == SteamLoginSignal.Password ? SecretKind.Password : SecretKind.SteamGuard;
                if ((kind == SecretKind.Password && ++passwords > 3) || (kind == SecretKind.SteamGuard && ++guards > 5))
                    throw new InvalidOperationException("Zu viele Passwort-/Code-Abfragen. Anmeldung beendet, um weitere Fehlversuche zu vermeiden.");
                buffers[part.Reader].Flush();
                var secret = await requestSecret(kind, deadline.Token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(secret)) throw new OperationCanceledException("Steam-Anmeldung wurde abgebrochen.");
                if (secret.Length > 512 || secret.Any(char.IsControl))
                    throw new InvalidDataException("Anmeldedaten dürfen keine Steuerzeichen enthalten und höchstens 512 Zeichen lang sein.");
                redactor.Add(secret);
                await active.SendLineAsync(secret, deadline.Token).ConfigureAwait(false);
            }
            foreach (var buffer in buffers) buffer.Flush();
            await readTask.ConfigureAwait(false);
            await complete.ConfigureAwait(false);
            await exitTask.ConfigureAwait(false);
            var resultText = redactor.Redact(output.ToString());
            var published = Regex.IsMatch(resultText, @"Success\s*\.\s*Published\s+File\s*Id\s*:\s*[1-9]\d*", RegexOptions.IgnoreCase);
            return new(active.ExitCode, SteamCmdPromptDetector.IsAuthenticated(resultText), published, resultText,
                SteamCmdPromptDetector.ReadSteamId64(resultText));
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException("Zeitlimit für SteamCMD erreicht. Das Login-Protokoll und eine eventuell offene Steam-Guard-Freigabe prüfen. Bei Uploads vor einem erneuten Versuch den Workshop-Status kontrollieren.");
        }
        finally
        {
            try
            {
                shuttingDown = true;
                channel.Writer.TryComplete();
                if (transport is not null)
                {
                    transport.Kill();
                    await transport.CloseConsoleAsync().ConfigureAwait(false);
                    if (readTask is not null) { try { await readTask.ConfigureAwait(false); } catch (IOException) { } }
                    if (exitTask is not null) { try { await exitTask.ConfigureAwait(false); } catch (OperationCanceledException) { } }
                    await transport.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally { IsBusy = false; _exclusive.Release(); }
        }
    }
}
