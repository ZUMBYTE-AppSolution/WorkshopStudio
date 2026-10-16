using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorkshopStudio.Core;

public sealed record WorkshopItemBackupResult(string Path,long Bytes,int ContentFiles,string Sha256);
public sealed record WorkshopBackupManifest(int FormatVersion,ulong WorkshopId,uint AppId,ReleaseChannel Channel,
    DateTimeOffset CreatedAt,IReadOnlyList<PackageFile> Files);

/// <summary>A verified snapshot of the downloaded published content, before an existing ID is changed.</summary>
public sealed class WorkshopItemBackup
{
    private readonly WorkspaceStore _store;
    public WorkshopItemBackup(WorkspaceStore store)=>_store=store;
    private static void EnsurePlainPath(string path)
    {
        for(var current=new DirectoryInfo(Path.GetFullPath(path));current is not null;current=current.Parent)
            if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Verknüpfte Backup-Pfade sind nicht erlaubt: "+current.FullName);
    }
    public async Task<WorkshopItemBackupResult> CreateAsync(RemoteWorkshopItem remote,ModProject profile,ReleaseChannel channel,
        string downloadedContent,string previewPath,StudioSettings settings,IProgress<string>? progress,CancellationToken ct)
    {
        if(remote.Id==0||remote.Id!=profile.GetWorkshopId(channel)||remote.ConsumerAppId==0||remote.FileType!=0||
            remote.FieldStatus.GetValueOrDefault("owner")!=RemoteFieldStatus.Known||remote.UpdatedAt is null)
            throw new InvalidDataException("Workshop-Ziel ist für ein vollständiges Backup nicht bestätigt.");
        EnsurePlainPath(downloadedContent);EnsurePlainPath(_store.BackupRoot);
        if(!Directory.Exists(downloadedContent))throw new DirectoryNotFoundException("Heruntergeladener Workshop-Inhalt fehlt.");
        var directory=Path.Combine(_store.BackupRoot,"workshop-items",remote.Id.ToString(),channel.ToString());
        Directory.CreateDirectory(directory);EnsurePlainPath(directory);
        var final=Path.Combine(directory,"before-"+DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")[..8]+".zip");
        var temporary=final+".partial";var files=new List<PackageFile>();long total=0;var contentFiles=0;
        try {
            using(var archive=ZipFile.Open(temporary,ZipArchiveMode.Create)) {
                async Task AddStream(string name,Stream input) {
                    if(files.Count>=settings.MaxFileCount+10)throw new InvalidDataException("Zu viele Dateien im Workshop-Backup.");
                    var entry=archive.CreateEntry(name,CompressionLevel.Fastest);
                    await using var output=entry.Open();using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer=new byte[81920];long bytes=0;int read;
                    while((read=await input.ReadAsync(buffer,ct))>0) {
                        total+=read;bytes+=read;
                        if(total>settings.MaxPackageMiB*1048576L)throw new InvalidDataException("Der bisherige Workshop-Inhalt überschreitet die eingestellte Backup-/Paketgröße.");
                        hash.AppendData(buffer,0,read);await output.WriteAsync(buffer.AsMemory(0,read),ct);
                    }
                    files.Add(new(name,bytes,Convert.ToHexString(hash.GetHashAndReset())));
                }
                async Task Text(string name,string value) {
                    using var input=new MemoryStream(Encoding.UTF8.GetBytes(value));await AddStream(name,input);
                }
                var pending=new Stack<string>();pending.Push(downloadedContent);
                while(pending.TryPop(out var current)) {
                    ct.ThrowIfCancellationRequested();EnsurePlainPath(current);
                    foreach(var file in Directory.EnumerateFiles(current).OrderBy(f=>f,StringComparer.Ordinal)) {
                        if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Verknüpfte Datei im bisherigen Workshop-Inhalt.");
                        var relative=Path.GetRelativePath(downloadedContent,file).Replace('\\','/');
                        if(!PackageBuilder.IsWithin(file,downloadedContent)||relative.StartsWith("../",StringComparison.Ordinal))throw new InvalidDataException("Ungültiger Backup-Pfad.");
                        await using var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read,81920,true);
                        await AddStream("content/"+relative,input);contentFiles++;
                        if(contentFiles%100==0)progress?.Report($"Backup: {contentFiles:N0} Dateien · {Format.Bytes(total)}");
                    }
                    foreach(var sub in Directory.EnumerateDirectories(current))pending.Push(sub);
                    if(!Directory.EnumerateFileSystemEntries(current).Any())archive.CreateEntry("content/"+Path.GetRelativePath(downloadedContent,current).Replace('\\','/').Trim('.')+"/");
                }
                if(contentFiles==0)throw new InvalidDataException("Der heruntergeladene Workshop-Inhalt ist leer. Update gestoppt.");
                await Text("metadata/steam-item.json",JsonSerializer.Serialize(remote,WorkspaceStore.JsonOptions));
                await Text("metadata/local-profile-before-update.json",JsonSerializer.Serialize(profile,WorkspaceStore.JsonOptions));
                await Text("metadata/description.txt",remote.Description);
                await Text("metadata/changelog.txt",remote.RemoteChangelogStatus+"\n\n"+remote.RemoteChangelog);
                if(remote.PreviewUrl.Length>0) {
                    if(!File.Exists(previewPath)||(File.GetAttributes(previewPath)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Das bisherige Workshop-Vorschaubild konnte nicht gesichert werden.");
                    await using var preview=File.OpenRead(previewPath);await AddStream("metadata/preview"+Path.GetExtension(previewPath),preview);
                }
                await Text("RESTORE.txt",$"Workshop Studio · Sicherung vor einem Update\nWorkshop-ID: {remote.Id}\nAppID: {remote.ConsumerAppId}\nKanal: {channel}\nSteam-Stand: {remote.UpdatedAt:O}\n\ncontent/ enthält den von Steam heruntergeladenen bisherigen Inhalt. metadata/ enthält die vor dem Upload gelesenen Steam-Daten, Beschreibung, erreichbaren Änderungsverlauf und das Vorschaubild. Alte Steam-Änderungsnotizen können nicht zurückgerollt werden.\n\nZIP vollständig in einen neuen Ordner entpacken. Im Mod-Profil die bestehende Workshop-ID beibehalten und die gewünschte Einzelmod unter content/ als Quelle auswählen. Bei einem Mehrmod-Paket nur die beabsichtigten Mods auswählen. Die alten Metadaten anhand metadata/ im Editor übernehmen. Paketvorschau prüfen und den Rücksetz-Upload gezielt freigeben. Die Sicherung selbst veröffentlicht nichts.\n");
                var manifest=new WorkshopBackupManifest(1,remote.Id,remote.ConsumerAppId,channel,DateTimeOffset.UtcNow,files);
                var manifestEntry=archive.CreateEntry("backup-manifest.json");
                await using var manifestOutput=manifestEntry.Open();await JsonSerializer.SerializeAsync(manifestOutput,manifest,WorkspaceStore.JsonOptions,ct);
            }
            await VerifyAsync(temporary,ct);
            await using var zip=File.OpenRead(temporary);var sha=Convert.ToHexString(await SHA256.HashDataAsync(zip,ct));
            var bytes=zip.Length;zip.Close();File.Move(temporary,final);
            await File.WriteAllTextAsync(final+".sha256",sha+"  "+Path.GetFileName(final)+"\n",Encoding.ASCII,ct);
            progress?.Report($"Backup geprüft: {contentFiles:N0} Dateien · {Format.Bytes(bytes)} · {final}");
            foreach(var old in new DirectoryInfo(directory).GetFiles("before-*.zip").Where(f=>File.Exists(f.FullName+".sha256"))
                .OrderByDescending(f=>f.FullName.Equals(final,StringComparison.OrdinalIgnoreCase)).ThenByDescending(f=>f.Name).Skip(Math.Max(1,settings.BackupRetention)))
                try {old.Delete();File.Delete(old.FullName+".sha256");}catch(IOException ex){progress?.Report("Älteres Backup bleibt erhalten: "+ex.Message);}
            return new(final,bytes,contentFiles,sha);
        } finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static async Task VerifyAsync(string path,CancellationToken ct)
    {
        using var archive=ZipFile.OpenRead(path);
        var manifestEntry=archive.GetEntry("backup-manifest.json")??throw new InvalidDataException("Backup-Manifest fehlt.");
        await using var input=manifestEntry.Open();
        var manifest=await JsonSerializer.DeserializeAsync<WorkshopBackupManifest>(input,WorkspaceStore.JsonOptions,ct)??throw new InvalidDataException("Backup-Manifest ungültig.");
        if(manifest.FormatVersion!=1||manifest.WorkshopId==0||manifest.Files.Count==0)throw new InvalidDataException("Backup-Manifest unvollständig.");
        if(manifest.Files.Select(f=>f.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=manifest.Files.Count)throw new InvalidDataException("Doppelte Backup-Pfade.");
        foreach(var file in manifest.Files) {
            ct.ThrowIfCancellationRequested();var entry=archive.GetEntry(file.RelativePath)??throw new InvalidDataException("Backup-Datei fehlt: "+file.RelativePath);
            if(entry.Length!=file.Bytes)throw new InvalidDataException("Backup-Dateigröße stimmt nicht: "+file.RelativePath);
            await using var source=entry.Open();var hash=Convert.ToHexString(await SHA256.HashDataAsync(source,ct));
            if(hash!=file.Sha256)throw new InvalidDataException("Backup-Prüfsumme stimmt nicht: "+file.RelativePath);
        }
    }
}
