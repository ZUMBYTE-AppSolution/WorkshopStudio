using System.IO.Compression;
using System.Text;
using System.Text.Json;
using WorkshopStudio.Core;

internal static partial class Program
{
    private static void RegisterMetadataBackupTests(string root)
    {
        static RemoteWorkshopItem Remote()=>new(){Id=123,OwnerSteamId64=76561198000000000UL,ConsumerAppId=108600,FileType=0,
            Title="Meine Mod",Description="Steam-Beschreibung",Visibility=0,UpdatedAt=DateTimeOffset.FromUnixTimeSeconds(1700000000),FieldStatus=new(){["owner"]=RemoteFieldStatus.Known}};
        static ModProject Mod()=>new(){Name="Meine Mod",Description="Steam-Beschreibung",WorkshopId=123,TestWorkshopId=456,Visibility=0,ChangeNote=""};
        static WorkshopMetadataPlan Plan(ModProject m,RemoteWorkshopItem? r=null,bool history=true,string localImage="",string remoteImage="")=>
            WorkshopMetadataPlanner.Compare(m,ReleaseChannel.Live,r??Remote(),localImage,remoteImage,history);
        Test("Metadaten: abweichende Beschreibung automatisch ausgewählt",()=>{
            var m=Mod();m.Description="Neue Beschreibung";Assert(Plan(m).Fields.SequenceEqual(new[]{"description"}));
        });
        Test("Metadaten: fehlende Werte löschen keine Steam-Angaben",()=>{
            var m=Mod();m.Description="";Assert(!Plan(m).Fields.Contains("description"));
        });
        Test("Metadaten: Zeilenenden allein erzeugen kein Update",()=>{
            var m=Mod();m.Description="A\r\nB\n";var r=Remote();r.Description="A\nB";Assert(!Plan(m,r).Fields.Contains("description"));
        });
        Test("Metadaten: Testtitel und private Sichtbarkeit bleiben getrennt",()=>{
            var m=Mod();var r=Remote();r.Id=456;r.Title=m.Name+m.TestTitleSuffix;r.Visibility=2;
            Assert(WorkshopMetadataPlanner.Compare(m,ReleaseChannel.Test,r,"","",true).Fields.Count==0);
        });
        Test("Metadaten: nur aktuelle Changelog-Version verwenden",()=>{
            var note=WorkshopMetadataPlanner.LatestChangeNote("# Changelog\n\n## [2.0] - heute\n### Neu\n- A\n\n## [1.0]\n- Alt");
            Assert(note.Contains("- A")&&!note.Contains("- Alt"));
        });
        Test("Metadaten: gefüllter Unreleased-Abschnitt geht vor alter Version",()=>{
            var note=WorkshopMetadataPlanner.LatestChangeNote("# Changelog\n## [Unreleased]\n- Neu\n## [1.0]\n- Alt");
            Assert(note.Contains("- Neu")&&!note.Contains("- Alt"));
        });
        Test("Metadaten: lokale Dateien ergänzen importierte Baseline",()=>{
            var m=Mod();var schema=new LocalWorkshopSchema("Neuer Titel","Neue Beschreibung","2.0","","## 2.0\n- Neu",[],[],[]);
            var resolved=WorkshopMetadataPlanner.ResolveLocal(m,schema,Remote(),true);
            Assert(resolved.Name=="Neuer Titel"&&resolved.Description=="Neue Beschreibung"&&resolved.Version=="2.0"&&resolved.ChangeNote.Contains("- Neu"));
            Assert(m.Description=="Steam-Beschreibung");
        });
        Test("Metadaten: eigene Editorwerte gehen vor Dateivorschlägen",()=>{
            var m=Mod();m.Name="Editor-Titel";m.Description="Editor-Text";m.ChangeNote="Bewusst eigene Notiz";
            var schema=new LocalWorkshopSchema("Dateititel","Dateitext","2.0","","Andere Notiz",[],[],[]);
            var resolved=WorkshopMetadataPlanner.ResolveLocal(m,schema,Remote(),true);
            Assert(resolved.Name==m.Name&&resolved.Description==m.Description&&resolved.ChangeNote==m.ChangeNote);
        });
        Test("Metadaten: vorhandene Versionszeile folgt ausdrücklich lokaler Version",()=>{
            var m=Mod();m.Description="Text\nVersion: 1.0";var r=Remote();r.Description=m.Description;
            var schema=new LocalWorkshopSchema("","","2.0","","",[],[],[]);
            var resolved=WorkshopMetadataPlanner.ResolveLocal(m,schema,r,true);Assert(resolved.Description=="Text\nVersion: 2.0");
        });
        Test("Metadaten: Live-Sichtbarkeit braucht eigenes Opt-in",()=>{
            var m=Mod();m.Visibility=2;Assert(!Plan(m).Fields.Contains("visibility"));
            m.SynchronizeVisibility=true;Assert(Plan(m).Fields.Contains("visibility"));
        });
        Test("Metadaten: ausgeschaltete Erkennung verändert Profil nicht",()=>{
            var m=Mod();var schema=new LocalWorkshopSchema("Dateititel","Dateitext","2.0","","Notiz",[],[],[]);
            Assert(WorkshopMetadataPlanner.ResolveLocal(m,schema,Remote(),false).Version==m.Version);
        });
        Test("Metadaten: neuer akzeptierter Remote-Text ersetzt unveränderten Import",()=>{
            var m=Mod();var r=Remote();WorkshopMetadataPlanner.RememberBaseline(m,ReleaseChannel.Live,r);
            r.Description="Extern neuer Text";WorkshopMetadataPlanner.RememberBaseline(m,ReleaseChannel.Live,r,true);
            Assert(m.Description==r.Description);
        });
        Test("Metadaten: Baseline-Übernahme erhält echte lokale Editoränderung",()=>{
            var m=Mod();var r=Remote();WorkshopMetadataPlanner.RememberBaseline(m,ReleaseChannel.Live,r);
            m.Description="Mein Editorwert";r.Description="Extern neu";WorkshopMetadataPlanner.RememberBaseline(m,ReleaseChannel.Live,r,true);
            Assert(m.Description=="Mein Editorwert");
        });
        Test("Metadaten: alte Baseline vor neuem Scan unterscheidet Import von Editor",()=>{
            var m=Mod();var r=Remote();WorkshopMetadataPlanner.RememberBaseline(m,ReleaseChannel.Live,r);r.Description="Neu auf Steam";
            var empty=new LocalWorkshopSchema("","","","","",[],[],[]);
            Assert(WorkshopMetadataPlanner.ResolveLocal(m,empty,r,true).Description=="Neu auf Steam");
            m.Description="Lokal editiert";Assert(WorkshopMetadataPlanner.ResolveLocal(m,empty,r,true).Description=="Lokal editiert");
        });
        Test("Metadaten: veröffentlichte Notiz nicht doppelt, Kanal getrennt",()=>{
            var m=Mod();m.ChangeNote="Neue Notiz";m.LastLiveNoteHash=WorkshopMetadataPlanner.HashText(m.ChangeNote);
            Assert(!Plan(m).Fields.Contains("changenote"));Assert(WorkshopMetadataPlanner.Compare(m,ReleaseChannel.Test,Remote(),"","",true).Fields.Contains("changenote"));
        });
        Test("Metadaten: identische Notiz im Steam-Verlauf wird erkannt",()=>{
            var m=Mod();m.ChangeNote="Neue Notiz";var r=Remote();r.RemoteChangelog="Update heute\nNeue Notiz\n\nAlt";
            r.RemoteChangeNotes.Add("Neue Notiz");
            Assert(!Plan(m,r).Fields.Contains("changenote"));
        });
        Test("Metadaten: Teiltext einer alten Notiz ist keine identische Release-Notiz",()=>{
            var m=Mod();m.ChangeNote="Fehler behoben";var r=Remote();r.RemoteChangeNotes.Add("Fehler behoben und weitere Dinge geändert");
            Assert(Plan(m,r).Fields.Contains("changenote"));
        });
        Test("Metadaten: unbekannte erste Notiz bei nicht lesbarem Verlauf zurückstellen",()=>{
            var m=Mod();m.ChangeNote="Notiz";Assert(!Plan(m,history:false).Fields.Contains("changenote"));
            m.LastLiveNoteHash="alt";Assert(Plan(m,history:false).Fields.Contains("changenote"));
        });
        Test("Metadaten: überlange Notiz wird vor Upload abgewiesen",()=>{
            var m=Mod();m.ChangeNote=new string('ü',4001);Throws<InvalidDataException>(()=>Plan(m));
        });
        Test("Metadaten: Bilddaten statt Dateiname vergleichen",()=>{
            var m=Mod();Assert(!Plan(m,localImage:"same",remoteImage:"same").Fields.Contains("previewfile"));
            Assert(Plan(m,localImage:"new",remoteImage:"old").Fields.Contains("previewfile"));
            m.LastLivePreviewHash="new";Assert(!Plan(m,localImage:"new",remoteImage:"steam-recompressed").Fields.Contains("previewfile"));
        });
        Test("Metadaten: mehrzeilige workshop.txt am Contents-Projekt lesen",()=>{
            var project=Path.Combine(root,"metadata-wrapper");var source=Path.Combine(project,"Contents","mods","Mine");Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source,"mod.info"),"id=mine\nname=Mine\n");
            File.WriteAllText(Path.Combine(project,"workshop.txt"),"title=Mine\ndescription=Zeile 1\ndescription=\ndescription=Zeile 3\n");
            var app=new AppProfile();var m=Mod();m.AppProfileId=app.Id;m.ContentFolder=source;
            var schema=LocalWorkshopSchemaReader.Inspect(app,m);Assert(schema.Description=="Zeile 1\n\nZeile 3",schema.Description);
        });
        Test("Metadaten: alte Workspaces erhalten IDs und neue sichere Vorgaben",()=>{
            var old="{\"schemaVersion\":2,\"settings\":{},\"mods\":[{\"workshopId\":123,\"lastLiveFingerprint\":\"old\"}]}";
            var w=JsonSerializer.Deserialize<Workspace>(old,WorkspaceStore.JsonOptions)!;
            Assert(w.Settings.AutoDetectMetadata&&w.Mods[0].WorkshopId==123&&w.Mods[0].LastLiveFingerprint=="old"&&w.Mods[0].LastLiveNoteHash=="");
        });
        Test("Metadaten: gemeinsames Auswahlpaket liest nur zugehörige Wrapper-Metadaten",()=>{
            var project=Path.Combine(root,"metadata-bundle");var container=Path.Combine(project,"Contents","mods");
            foreach(var name in new[]{"A","B"}){Directory.CreateDirectory(Path.Combine(container,name));File.WriteAllText(Path.Combine(container,name,"mod.info"),"id="+name+"\n");}
            File.WriteAllText(Path.Combine(project,"workshop.txt"),"description=Gemeinsame Workshop-Beschreibung\n");
            var app=new AppProfile();var m=Mod();m.AppProfileId=app.Id;m.ContentFolder=container;m.SourceMode=ModSourceMode.SelectedMods;m.SelectedModFolders=["A","B"];
            Assert(LocalWorkshopSchemaReader.Inspect(app,m).Description=="Gemeinsame Workshop-Beschreibung");
            Assert(ModSourceResolver.Resolve(app,m).Sources.Count==2);
        });
        (WorkspaceStore Store,string Content,RemoteWorkshopItem Remote,ModProject Mod) Fixture(string name) {
            var dir=Path.Combine(root,"backup-"+name);var store=new WorkspaceStore(Path.Combine(dir,"studio"));var content=Path.Combine(dir,"download");
            Directory.CreateDirectory(Path.Combine(content,"mods","Mine"));File.WriteAllText(Path.Combine(content,"mods","Mine","mod.info"),"id=mine\n");
            File.WriteAllText(Path.Combine(content,"mods","Mine","data.txt"),"Vorheriger Steam-Inhalt");return(store,content,Remote(),Mod());
        }
        AsyncTest("Workshop-Backup: bisheriger Inhalt und Metadaten vollständig geprüft",async()=>{
            var f=Fixture("complete");var path=await new WorkshopItemBackup(f.Store).CreateAsync(f.Remote,f.Mod,ReleaseChannel.Live,f.Content,"",new(),null,CancellationToken.None);
            await WorkshopItemBackup.VerifyAsync(path.Path,CancellationToken.None);using var zip=ZipFile.OpenRead(path.Path);
            Assert(zip.GetEntry("content/mods/Mine/data.txt") is not null&&zip.GetEntry("metadata/description.txt") is not null&&File.Exists(path.Path+".sha256")&&path.ContentFiles==2);
        });
        AsyncTest("Workshop-Backup: Eigentümer oder Ziel-ID unbestätigt blockiert",async()=>{
            var f=Fixture("owner");f.Remote.FieldStatus.Clear();await ThrowsAsync<InvalidDataException>(()=>new WorkshopItemBackup(f.Store).CreateAsync(f.Remote,f.Mod,ReleaseChannel.Live,f.Content,"",new(),null,CancellationToken.None));
            Assert(!Directory.EnumerateFiles(f.Store.BackupRoot,"*.zip",SearchOption.AllDirectories).Any());
        });
        AsyncTest("Workshop-Backup: Größenfehler hinterlässt kein fertiges Backup",async()=>{
            var f=Fixture("limit");await File.WriteAllBytesAsync(Path.Combine(f.Content,"large.bin"),new byte[1048577]);
            await ThrowsAsync<InvalidDataException>(()=>new WorkshopItemBackup(f.Store).CreateAsync(f.Remote,f.Mod,ReleaseChannel.Live,f.Content,"",new(){MaxPackageMiB=1},null,CancellationToken.None));
            Assert(!Directory.EnumerateFiles(f.Store.BackupRoot,"*",SearchOption.AllDirectories).Any());
        });
        AsyncTest("Workshop-Backup: beschädigte Inhaltsdatei wird erkannt",async()=>{
            var f=Fixture("corrupt");var saved=await new WorkshopItemBackup(f.Store).CreateAsync(f.Remote,f.Mod,ReleaseChannel.Live,f.Content,"",new(),null,CancellationToken.None);
            using(var zip=ZipFile.Open(saved.Path,ZipArchiveMode.Update)) {var entry=zip.GetEntry("content/mods/Mine/data.txt")!;entry.Delete();var changed=zip.CreateEntry("content/mods/Mine/data.txt");using var writer=new StreamWriter(changed.Open());writer.Write("Manipuliert");}
            await ThrowsAsync<InvalidDataException>(()=>WorkshopItemBackup.VerifyAsync(saved.Path,CancellationToken.None));
        });
        AsyncTest("Workshop-Backup: fehlendes altes Vorschaubild stoppt Sicherung",async()=>{
            var f=Fixture("preview");f.Remote.PreviewUrl="https://steamusercontent.com/example";
            await ThrowsAsync<InvalidDataException>(()=>new WorkshopItemBackup(f.Store).CreateAsync(f.Remote,f.Mod,ReleaseChannel.Live,f.Content,"",new(),null,CancellationToken.None));
            Assert(!Directory.EnumerateFiles(f.Store.BackupRoot,"*.zip",SearchOption.AllDirectories).Any());
        });
        AsyncTest("Workshop-Backup: Aufbewahrung erst nach erfolgreicher neuer Sicherung",async()=>{
            var f=Fixture("retention");var service=new WorkshopItemBackup(f.Store);
            var a=await service.CreateAsync(f.Remote,f.Mod,ReleaseChannel.Live,f.Content,"",new(){BackupRetention=1},null,CancellationToken.None);
            var b=await service.CreateAsync(f.Remote,f.Mod,ReleaseChannel.Live,f.Content,"",new(){BackupRetention=1},null,CancellationToken.None);
            Assert(!File.Exists(a.Path)&&File.Exists(b.Path));
        });
        Test("Backup-Download: Argumente enthalten nur Download, IDs und Identitätsprüfung",()=>{
            var args=SteamCmdRunner.CreateDownloadArguments("test_user",108600,123).ToArray();
            Assert(args.Contains("+workshop_download_item")&&!args.Contains("+workshop_build_item")&&args.Contains("validate")&&args.Contains("+info")&&args[^1]=="+quit");
            Throws<InvalidDataException>(()=>SteamCmdRunner.CreateDownloadArguments("test_user",0,123));
        });
        Test("Backup-Download: Exit 0 oder fremdes Ziel allein reicht nicht",()=>{
            var dir=Path.Combine(root,"fake-steamcmd");Directory.CreateDirectory(dir);var exe=Path.Combine(dir,"steamcmd.exe");
            var expected=Path.Combine(dir,"steamapps","workshop","content","108600","123");Directory.CreateDirectory(expected);
            var success=new SteamCmdResult(0,true,false,$"Success. Downloaded item 123 to \"{expected}\" (10 bytes)");
            Assert(SteamCmdRunner.ConfirmDownload(exe,108600,123,success)==expected);
            Throws<IOException>(()=>SteamCmdRunner.ConfirmDownload(exe,108600,456,success));
            Throws<IOException>(()=>SteamCmdRunner.ConfirmDownload(exe,108600,123,new(0,true,false,"ERROR! Download failed")));
            Throws<InvalidDataException>(()=>SteamCmdRunner.ConfirmDownload(exe,108600,123,success with {Output=$"Success. Downloaded item 123 to \"{root}\""}));
        });
    }
}
