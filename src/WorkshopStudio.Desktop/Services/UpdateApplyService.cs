using System.Diagnostics;
using System.Text.Json;
using WorkshopStudio.Core;

namespace WorkshopStudio.Desktop.Services;

internal static class UpdateApplyService
{
    public static void Start(string payload, string installation, Version version)
    {
        Validate(payload, installation, version);
        var info = new ProcessStartInfo(Path.Combine(payload, "WorkshopStudio.exe")) {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = payload
        };
        info.ArgumentList.Add("--apply-update");
        info.ArgumentList.Add(Path.GetFullPath(installation));
        info.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        info.ArgumentList.Add(version.ToString());
        if (Process.Start(info) is null) throw new IOException("Der Update-Helfer konnte nicht gestartet werden.");
    }

    public static async Task<int> ApplyAsync(string payload, string installation, int parentId, Version version)
    {
        var report = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorkshopStudio", "updates", "last-update-result.txt");
        try
        {
            Validate(payload, installation, version);
            try {
                using var parent = Process.GetProcessById(parentId);
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                await parent.WaitForExitAsync(timeout.Token);
            } catch (ArgumentException) { }
            InstallWithRollback(payload, installation);
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            await File.WriteAllTextAsync(report, $"OK: Workshop Studio {version} installiert.");
            Process.Start(new ProcessStartInfo(Path.Combine(installation, "WorkshopStudio.exe")) {
                UseShellExecute = true, WorkingDirectory = installation
            });
            return 0;
        }
        catch (Exception ex)
        {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(report)!);
                await File.WriteAllTextAsync(report, "FEHLER: " + ex.Message);
                if (File.Exists(Path.Combine(installation, "WorkshopStudio.exe")))
                    Process.Start(new ProcessStartInfo(Path.Combine(installation, "WorkshopStudio.exe")) {
                        UseShellExecute = true, WorkingDirectory = installation
                    });
            } catch (Exception) { }
            return 1;
        }
    }

    internal static void Validate(string payload, string installation, Version version)
    {
        payload = Path.GetFullPath(payload);
        installation = Path.GetFullPath(installation);
        if (!Directory.Exists(payload) || !Directory.Exists(installation) ||
            PackageBuilder.IsWithin(payload, installation) || PackageBuilder.IsWithin(installation, payload) ||
            !File.Exists(Path.Combine(installation, "WorkshopStudio.exe")) ||
            !File.Exists(Path.Combine(payload, "WorkshopStudio.exe")))
            throw new InvalidDataException("Update-Quelle oder bestehende Installation ist ungültig.");
        foreach (var path in new[] { payload, installation })
            for (var dir = new DirectoryInfo(path); dir is not null; dir = dir.Parent)
                if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Verknüpfte Update- oder Installationspfade sind nicht erlaubt.");
        using var metadata = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(payload, "release-info.json")));
        if (metadata.RootElement.GetProperty("version").GetString() != version.ToString() ||
            metadata.RootElement.GetProperty("repository").GetString() != ReleaseUpdater.Repository)
            throw new InvalidDataException("Das vorbereitete Update hat die falsche Version oder Herkunft.");
    }

    internal static void InstallWithRollback(string payload, string installation)
    {
        var backup = Path.Combine(Directory.GetParent(payload)!.FullName, "backup");
        if (Directory.Exists(backup)) throw new InvalidDataException("Update-Sicherung existiert bereits.");
        Directory.CreateDirectory(backup);
        var files = Directory.EnumerateFiles(payload, "*", SearchOption.AllDirectories).ToArray();
        if (files.Length is 0 or > 10000) throw new InvalidDataException("Ungültige Anzahl von Update-Dateien.");
        var installed = new List<(string Destination, string? Backup)>();
        try
        {
            foreach (var file in files)
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Verknüpfte Update-Datei ist nicht erlaubt.");
                var relative = Path.GetRelativePath(payload, file);
                var destination = Path.GetFullPath(Path.Combine(installation, relative));
                if (!destination.StartsWith(Path.TrimEndingDirectorySeparator(installation) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update-Datei verlässt die Installation.");
                var parent = Path.GetDirectoryName(destination)!;
                EnsureNoLinkedDestination(parent, installation);
                Directory.CreateDirectory(parent);
                EnsureNoLinkedDestination(parent, installation);
                string? old = null;
                if (File.Exists(destination))
                {
                    old = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(old)!);
                    File.Copy(destination, old, false);
                }
                installed.Add((destination, old));
                File.Copy(file, destination, true);
            }
        }
        catch
        {
            var errors = new List<Exception>();
            foreach (var (destination, old) in installed.AsEnumerable().Reverse())
                try { if (old is null) File.Delete(destination); else File.Copy(old, destination, true); }
                catch (Exception ex) { errors.Add(ex); }
            if (errors.Count > 0) throw new AggregateException("Update-Rücknahme war unvollständig.", errors);
            throw;
        }
    }

    private static void EnsureNoLinkedDestination(string folder, string installation)
    {
        var boundary = Path.TrimEndingDirectorySeparator(installation) + Path.DirectorySeparatorChar;
        for (DirectoryInfo? dir = new(folder); dir is not null &&
            (dir.FullName.Equals(installation, StringComparison.OrdinalIgnoreCase) ||
             dir.FullName.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)); dir = dir.Parent)
            if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Verknüpfter Zielordner ist nicht erlaubt.");
    }
}
