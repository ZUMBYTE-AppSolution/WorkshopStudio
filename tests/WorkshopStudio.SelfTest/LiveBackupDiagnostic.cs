using System.Text.Json;
using WorkshopStudio.Core;

internal static partial class Program
{
    private static async Task<int> LiveBackupDiagnosticAsync(string output)
    {
        output=Path.GetFullPath(output);Directory.CreateDirectory(output);
        var report=Path.Combine(output,"backup-readonly-result.txt");
        try {
            var userRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorkshopStudio");
            var before=await File.ReadAllBytesAsync(Path.Combine(userRoot,"workspace.json"));
            var workspace=JsonSerializer.Deserialize<Workspace>(before,WorkspaceStore.JsonOptions)??throw new InvalidDataException("Arbeitsbereich fehlt.");
            var account=workspace.Accounts.Single(a=>a.Id==workspace.Settings.ActiveAccountId);
            var candidate=workspace.RemoteItems.Where(i=>i.OwnerSteamId64==account.VerifiedWebSteamId64&&i.FileType==0&&i.FileSize is >0&&
                i.FieldStatus.GetValueOrDefault("owner")==RemoteFieldStatus.Known).OrderBy(i=>i.FileSize).FirstOrDefault()
                ??throw new InvalidOperationException("Kein bestätigter eigener Eintrag mit bekannter Größe vorhanden.");
            var store=new WorkspaceStore(Path.Combine(output,"diagnostic-data"));using var steam=new SteamService(store);
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
            var current=new RemoteWorkshopItem{Id=candidate.Id,OwnerSteamId64=account.VerifiedWebSteamId64,FileType=candidate.FileType};await steam.EnrichWorkshopItemsAsync([current],ct);
            var runner=new SteamCmdRunner();var logLock=new object();var log=new Progress<string>(line=>{lock(logLock)File.AppendAllText(Path.Combine(output,"backup-readonly.log"),line+"\n");});
            var download=await runner.DownloadWorkshopItemAsync(workspace.Settings.SteamCmdPath,account.UserName,current.ConsumerAppId,current.Id,
                TimeSpan.FromMinutes(2),(_,_)=>Task.FromResult<string?>(null),log,ct);
            if(download.Command.SteamId64!=account.VerifiedWebSteamId64)throw new InvalidOperationException("Download-Konto stimmt nicht mit Webkonto überein.");
            var note=await steam.GetChangeNotesAsync(current.Id,ct);current.RemoteChangelog=note.Text;current.RemoteChangelogStatus=note.Status;current.RemoteChangeNotes=note.Notes.ToList();
            var preview=current.PreviewUrl.Length>0?await steam.CacheWorkshopPreviewAsync(current,0,ct):"";
            var profile=workspace.Mods.FirstOrDefault(m=>m.WorkshopId==current.Id)??new ModProject{WorkshopId=current.Id,Name=current.Title};
            var backup=await new WorkshopItemBackup(store).CreateAsync(current,profile,ReleaseChannel.Live,download.ContentPath,preview,workspace.Settings,log,ct);
            var after=new RemoteWorkshopItem{Id=current.Id,OwnerSteamId64=current.OwnerSteamId64};await steam.EnrichWorkshopItemsAsync([after],ct);
            if(current.UpdatedAt!=after.UpdatedAt)throw new InvalidOperationException("Remote-Zeitstand änderte sich während der Diagnose.");
            var userAfter=await File.ReadAllBytesAsync(Path.Combine(userRoot,"workspace.json"));
            if(!before.SequenceEqual(userAfter))throw new InvalidOperationException("Workspace wurde parallel geändert; Diagnose kann dessen Unverändertheit nicht bestätigen.");
            await File.WriteAllTextAsync(report,$"PASS: Eigener Eintrag über SteamCMD heruntergeladen; Konto stimmt mit verifiziertem Webkonto überein.\nBackup mit {backup.ContentFiles} Inhaltsdateien und {backup.Bytes} ZIP-Bytes erstellt und SHA-256-Dateiliste erneut geprüft.\nRemote-Zeitstand unverändert; Nutzer-Workspace bytegleich; keine Uploadargumente ausgeführt.\nBackup: {backup.Path}\n");
            Console.WriteLine(await File.ReadAllTextAsync(report));return 0;
        } catch(Exception ex) {
            await File.WriteAllTextAsync(report,"FAIL: "+ex.GetType().Name+" · "+ex.Message+"\nKein Upload ausgeführt.\n");
            Console.WriteLine(await File.ReadAllTextAsync(report));return 1;
        }
    }
}
