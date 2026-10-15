using System.Text.Json;
using WorkshopStudio.Core;

internal static partial class Program
{
    private static void RegisterModSourceTests(string testRoot, PackageBuilder builder, WorkspaceStore store)
    {
        var app = new AppProfile { AppId = 108600, Template = GameTemplate.ProjectZomboid42 };
        var settings = new StudioSettings();
        string Fresh() { var p=Path.Combine(testRoot,"scope-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p; }
        string Mod(string parent, string name, bool versioned=false)
        {
            var p=Path.Combine(parent,name);var metadata=versioned?Path.Combine(p,"42"):p;
            Directory.CreateDirectory(Path.Combine(metadata,"media","lua","shared"));
            File.WriteAllText(Path.Combine(metadata,"mod.info"),$"name={name}\nid={name}\n");
            File.WriteAllText(Path.Combine(metadata,"media","lua","shared","main.lua"),"return 1");
            if(versioned)Directory.CreateDirectory(Path.Combine(p,"common"));
            return p;
        }
        ModProject Profile(string path)=>new(){Name="Scope Test",AppProfileId=app.Id,ContentFolder=path};
        Task<PackageSnapshot> Build(ModProject m)=>builder.BuildAsync(app,m,ReleaseChannel.Test,settings,Guid.NewGuid(),null,CancellationToken.None);
        Task<PackageSnapshot> BuildPath(string p)=>Build(Profile(p));

        AsyncTest("Einzelmod B41: Wrapper wird automatisch erzeugt",async()=>{
            var path=Mod(Fresh(),"rp-core");var p=await BuildPath(path);
            Assert(p.Files.Any(f=>f.RelativePath=="mods/rp-core/mod.info"));
            Assert(p.Files.All(f=>f.RelativePath.StartsWith("mods/rp-core/")));
            Assert(p.Plan.Sources.Single().SourceRoot==path);
        });
        AsyncTest("Einzelmod B42: 42 und common bleiben gemeinsam",async()=>{
            var path=Mod(Fresh(),"rp-core",true);File.WriteAllText(Path.Combine(path,"common","asset.txt"),"shared");
            var p=await BuildPath(path);
            Assert(p.Files.Any(f=>f.RelativePath=="mods/rp-core/42/mod.info"));
            Assert(p.Files.Any(f=>f.RelativePath=="mods/rp-core/common/asset.txt"));
        });
        AsyncTest("Auswahl 42/: Mod-Stamm statt isolierter Versionsordner",async()=>{
            var path=Mod(Fresh(),"rp-core",true);File.WriteAllText(Path.Combine(path,"common","asset.txt"),"shared");
            var p=await BuildPath(Path.Combine(path,"42"));
            Assert(p.Files.Any(f=>f.RelativePath=="mods/rp-core/common/asset.txt"));
            Assert(p.Files.All(f=>!f.RelativePath.StartsWith("mods/42/")));
        });
        AsyncTest("Einzelmod unter Spiel/mods: Spiel wird nicht kopiert",async()=>{
            var game=Fresh();File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"not a real executable");
            var path=Mod(Path.Combine(game,"mods"),"rp-core");var p=await BuildPath(path);
            Assert(p.Files.All(f=>f.RelativePath.StartsWith("mods/rp-core/")));
        });
        AsyncTest("Spielverzeichnis: Abbruch vor Erstellung des Staging-Ordners",async()=>{
            var game=Fresh();File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"dummy");Mod(Path.Combine(game,"mods"),"rp-core");
            var id=Guid.NewGuid();await ThrowsAsync<InvalidDataException>(()=>builder.BuildAsync(app,Profile(game),ReleaseChannel.Test,settings,id,null,CancellationToken.None));
            Assert(!Directory.Exists(Path.Combine(store.StagingRoot,id.ToString("N"))));
        });
        Test("Spielverzeichnis: Prüfung lässt sich nicht abschalten",()=>{
            var game=Fresh();File.WriteAllText(Path.Combine(game,"ProjectZomboid64.json"),"{}");
            Throws<InvalidDataException>(()=>builder.Validate(app,Profile(game),new StudioSettings{ValidateZomboidLayout=false}));
        });
        Test("Spielverzeichnis: Vanilla-Layout ohne EXE gesperrt",()=>{
            var game=Fresh();Directory.CreateDirectory(Path.Combine(game,"media"));Directory.CreateDirectory(Path.Combine(game,"zombie"));
            Throws<InvalidDataException>(()=>ModSourceResolver.Discover(game));
        });
        Test("Steam-Verzeichnis: Installationswurzel gesperrt",()=>{
            var steam=Fresh();Directory.CreateDirectory(Path.Combine(steam,"steamapps"));
            Throws<InvalidDataException>(()=>ModSourceResolver.EnsureSafeDirectory(steam));
        });
        Test("Mehrere Mods: keine stille Auswahl oder Vollverpackung",()=>{
            var mods=Path.Combine(Fresh(),"mods");Mod(mods,"A");Mod(mods,"B");
            Throws<InvalidDataException>(()=>ModSourceResolver.Resolve(app,Profile(mods)));
        });
        Test("Lokaler Stammordner: Einzelmods in Unterprojekten und B42 erkennen",()=>{
            var root=Fresh();var a=Mod(Path.Combine(root,"ProjektA","Contents","mods"),"A");
            var b=Mod(Path.Combine(root,"ProjektB","mods"),"B",true);
            Directory.CreateDirectory(Path.Combine(root,"notizen"));File.WriteAllText(Path.Combine(root,"notizen","readme.txt"),"kein Mod");
            var found=LocalModCatalog.Discover(root);
            Assert(found.Candidates.Count==2 && found.Candidates.Any(c=>c.Root==a) &&
                found.Candidates.Any(c=>c.Root==b && c.Versioned));
        });
        Test("Lokale B42-Versionsauswahl ergibt einen Mod-Stamm",()=>{
            var root=Mod(Fresh(),"Versioned",true);
            var found=LocalModCatalog.Discover(Path.Combine(root,"42"));
            Assert(found.Candidates.Count==1 && found.Candidates[0].Root==root);
        });
        Test("Lokaler Katalog lehnt Spielinstallation ab",()=>{
            var root=Fresh();File.WriteAllText(Path.Combine(root,"ProjectZomboid64.exe"),"dummy");
            Mod(Path.Combine(root,"mods"),"A");
            Throws<InvalidDataException>(()=>LocalModCatalog.Discover(root));
        });
        Test("Lokaler Abgleich: technische ID vor Name, danach eindeutiger Name",()=>{
            var root=Fresh();Mod(root,"A");Mod(root,"B");
            var candidates=LocalModCatalog.Discover(root).Candidates;
            var byId=new ModProject{AppProfileId=app.Id,Name="Anderer Titel",ModIds="A"};
            var byName=new ModProject{AppProfileId=app.Id,Name="B",ModIds=""};
            var matches=LocalModCatalog.Suggest(candidates,[byId,byName],app.Id);
            Assert(matches.Single(s=>s.Candidate.FolderName=="A").ExistingModId==byId.Id);
            Assert(matches.Single(s=>s.Candidate.FolderName=="B").ExistingModId==byName.Id);
        });
        Test("Lokaler Abgleich: mehrere passende Quellen bleiben unzugeordnet",()=>{
            var root=Fresh();var a=Mod(root,"A");var b=Mod(root,"B");
            File.WriteAllText(Path.Combine(b,"mod.info"),"name=B\nid=A\n");
            var candidates=LocalModCatalog.Discover(root).Candidates;
            var profile=new ModProject{AppProfileId=app.Id,ModIds="A"};
            Assert(LocalModCatalog.Suggest(candidates,[profile],app.Id).All(s=>s.ExistingModId is null));
        });
        Test("Lokale Zuordnung aktualisiert nur Quelle und schaltet Auto-Publish aus",()=>{
            var root=Fresh();var path=Mod(root,"A");var candidate=LocalModCatalog.Discover(root).Candidates.Single();
            var existing=new ModProject{AppProfileId=app.Id,Name="Workshop A",ModIds="A",Description="Remote-Beschreibung",
                WorkshopId=123456,OwnerSteamId64=76561197960265729,ImportedFromWorkshop=true,
                ContentFolder="",AutoPublish=true};
            var workspace=new Workspace{Apps=[app],Mods=[existing]};
            var result=LocalModMapper.Prepare(workspace,app,[new(candidate,existing.Id,false)],store.Root);
            var assigned=result.Workspace.Mods.Single();
            Assert(result.Updated==1 && result.Created==0 && assigned.ContentFolder==path);
            Assert(assigned.WorkshopId==existing.WorkshopId && assigned.Description==existing.Description);
            Assert(!assigned.AutoPublish && assigned.SourceMode==ModSourceMode.SingleMod && workspace.Mods.Single().ContentFolder=="");
        });
        Test("Lokale Zuordnung legt neue Mod ohne Workshop-ID und Upload an",()=>{
            var root=Fresh();var path=Mod(root,"Neu");var candidate=LocalModCatalog.Discover(root).Candidates.Single();
            var result=LocalModMapper.Prepare(new Workspace{Apps=[app]},app,[new(candidate,null,true)],store.Root);
            var added=result.Workspace.Mods.Single();
            Assert(result.Created==1 && added.ContentFolder==path && added.ModIds=="Neu");
            Assert(added.WorkshopId==0 && added.TestWorkshopId==0 && !added.AutoPublish && result.Workspace.Jobs.Count==0);
        });
        Test("Lokale Zuordnung blockiert falsche ID und wartenden Auftrag",()=>{
            var root=Fresh();Mod(root,"B");var candidate=LocalModCatalog.Discover(root).Candidates.Single();
            var existing=new ModProject{AppProfileId=app.Id,ModIds="A",WorkshopId=123456};
            var workspace=new Workspace{Apps=[app],Mods=[existing]};
            Throws<InvalidDataException>(()=>LocalModMapper.Prepare(workspace,app,[new(candidate,existing.Id,false)],store.Root));
            existing.ModIds="B";workspace.Jobs.Add(new PublishJob{ModId=existing.Id,Status=JobStatus.Pending});
            Throws<InvalidOperationException>(()=>LocalModMapper.Prepare(workspace,app,[new(candidate,existing.Id,false)],store.Root));
        });
        Test("Lokale Zuordnung blockiert doppelte Zielprofile und Workspace-Quelle",()=>{
            var root=Fresh();Mod(root,"A");Mod(root,"B");var found=LocalModCatalog.Discover(root).Candidates;
            var existing=new ModProject{AppProfileId=app.Id};var workspace=new Workspace{Apps=[app],Mods=[existing]};
            Throws<InvalidDataException>(()=>LocalModMapper.Prepare(workspace,app,
                found.Select(c=>new LocalModMappingSelection(c,existing.Id,false)).ToArray(),store.Root));
            Throws<InvalidDataException>(()=>LocalModMapper.Prepare(workspace,app,[new(found[0],null,true)],root));
        });
        AsyncTest("mods/-Ordner: einzelne Mod, keine Nachbardateien",async()=>{
            var mods=Path.Combine(Fresh(),"mods");Mod(mods,"A");File.WriteAllText(Path.Combine(mods,"not-a-mod.txt"),"excluded by scope");
            var p=await BuildPath(mods);Assert(p.Files.All(f=>f.RelativePath.StartsWith("mods/A/")));
        });
        AsyncTest("Contents/: Inhalte neben mods/ werden ausgelassen",async()=>{
            var contents=Path.Combine(Fresh(),"Contents");Mod(Path.Combine(contents,"mods"),"A");File.WriteAllText(Path.Combine(contents,"private-notes.txt"),"outside");
            var p=await BuildPath(contents);Assert(p.Files.All(f=>f.RelativePath.StartsWith("mods/A/")));
        });
        AsyncTest("Workshop-Projekt: kein Contents/ im Upload",async()=>{
            var project=Fresh();Mod(Path.Combine(project,"Contents","mods"),"A");File.WriteAllText(Path.Combine(project,"workshop.txt"),"title=test");
            var p=await BuildPath(project);Assert(p.Files.All(f=>f.RelativePath.StartsWith("mods/A/")));
        });
        AsyncTest("Gezieltes Mehrmod-Paket: nur gespeicherte Auswahl",async()=>{
            var mods=Path.Combine(Fresh(),"mods");Mod(mods,"A");Mod(mods,"B");Mod(mods,"C");
            var profile=Profile(mods);profile.SourceMode=ModSourceMode.SelectedMods;profile.SelectedModFolders=["A","C"];
            var p=await Build(profile);Assert(p.Plan.Sources.Count==2);Assert(p.Files.All(f=>!f.RelativePath.StartsWith("mods/B/")));
        });
        AsyncTest("Neuer Nachbarordner verändert ausgewähltes Paket nicht",async()=>{
            var mods=Path.Combine(Fresh(),"mods");Mod(mods,"A");var profile=Profile(mods);profile.SourceMode=ModSourceMode.SelectedMods;profile.SelectedModFolders=["A"];
            var first=await Build(profile);Mod(mods,"B");var next=await Build(profile);
            Assert(first.Fingerprint==next.Fingerprint);
        });
        Test("Fehlende ausgewählte Mod wird nicht ersetzt",()=>{
            var mods=Path.Combine(Fresh(),"mods");Mod(mods,"B");var profile=Profile(mods);profile.SourceMode=ModSourceMode.SelectedMods;profile.SelectedModFolders=["A"];
            Throws<InvalidDataException>(()=>ModSourceResolver.Resolve(app,profile));
        });
        Test("Mehrmod-Paket ohne Auswahl wird abgewiesen",()=>{
            var path=Mod(Fresh(),"A");var profile=Profile(path);profile.SourceMode=ModSourceMode.SelectedMods;
            Throws<InvalidDataException>(()=>ModSourceResolver.Resolve(app,profile));
        });
        Test("Mehrmod-Auswahl: Pfad-Traversal gesperrt",()=>{
            var path=Mod(Fresh(),"A");var profile=Profile(path);profile.SourceMode=ModSourceMode.SelectedMods;profile.SelectedModFolders=["../A"];
            Throws<InvalidDataException>(()=>ModSourceResolver.Resolve(app,profile));
        });
        Test("Mehrmod-Auswahl: doppelte Auswahl gesperrt",()=>{
            var path=Mod(Fresh(),"A");var profile=Profile(path);profile.SourceMode=ModSourceMode.SelectedMods;profile.SelectedModFolders=["A","a"];
            Throws<InvalidDataException>(()=>ModSourceResolver.Resolve(app,profile));
        });
        Test("Doppelte Mod-IDs in verschiedenen Mod-Ordnern gesperrt",()=>{
            var mods=Path.Combine(Fresh(),"mods");Mod(mods,"A");var b=Mod(mods,"B");File.WriteAllText(Path.Combine(b,"mod.info"),"name=B\nid=A");
            var profile=Profile(mods);profile.SourceMode=ModSourceMode.SelectedMods;profile.SelectedModFolders=["A","B"];
            Throws<InvalidDataException>(()=>ModSourceResolver.Resolve(app,profile));
        });
        Test("Ordner ohne mod.info ist keine Zomboid-Mod",()=>{
            var path=Fresh();Directory.CreateDirectory(Path.Combine(path,"media"));
            Throws<InvalidDataException>(()=>ModSourceResolver.Resolve(app,Profile(path)));
        });
        Test("AppID 108600 bleibt Zomboid trotz generischem Profil",()=>{
            var generic=new AppProfile{AppId=108600,Template=GameTemplate.Generic};
            Throws<InvalidDataException>(()=>ModSourceResolver.Resolve(generic,Profile(Fresh())));
        });
        AsyncTest("Leerer common/-Ordner bleibt im Snapshot erhalten",async()=>{
            var path=Mod(Fresh(),"A",true);var p=await BuildPath(path);
            Assert(Directory.Exists(Path.Combine(p.ContentPath,"mods","A","common")));
            Assert(p.Directories.Contains("mods/A/common"));
        });
        AsyncTest("Fehlender common/-Ordner: nur im Staging ergänzen",async()=>{
            var path=Mod(Fresh(),"A",true);Directory.Delete(Path.Combine(path,"common"));var p=await BuildPath(path);
            Assert(Directory.Exists(Path.Combine(p.ContentPath,"mods","A","common")));
            Assert(!Directory.Exists(Path.Combine(path,"common")) && p.Plan.Warnings.Count>0);
        });
        AsyncTest("Dateifilter funktionieren relativ zur Einzelmod",async()=>{
            var path=Mod(Fresh(),"A");File.WriteAllText(Path.Combine(path,"secret.tmp"),"omit");
            var p=await BuildPath(path);Assert(p.Files.All(f=>!f.RelativePath.EndsWith(".tmp")));
        });
        AsyncTest("Dateifilter funktionieren relativ zum Upload-Ziel",async()=>{
            var path=Mod(Fresh(),"A");var profile=Profile(path);profile.Exclusions="mods/A/media/**";
            var p=await Build(profile);Assert(p.Files.Single().RelativePath=="mods/A/mod.info");
        });
        AsyncTest("Ausgeschlossene mod.info verhindert Uploadpaket",async()=>{
            var path=Mod(Fresh(),"A");var profile=Profile(path);profile.Exclusions="mod.info";
            await ThrowsAsync<InvalidDataException>(()=>Build(profile));
        });
        AsyncTest("Wrapper und Einzelmod ergeben identischen Inhaltshash",async()=>{
            var root=Fresh();var path=Mod(Path.Combine(root,"Contents","mods"),"A",true);
            var direct=await BuildPath(path);var wrapped=await BuildPath(root);
            Assert(direct.Fingerprint==wrapped.Fingerprint);
        });
        AsyncTest("Paketierung verändert keine Quellmod-Dateien",async()=>{
            var path=Mod(Fresh(),"A");var before=Directory.GetFiles(path,"*",SearchOption.AllDirectories).ToDictionary(f=>f,File.ReadAllText);
            await BuildPath(path);var after=Directory.GetFiles(path,"*",SearchOption.AllDirectories).ToDictionary(f=>f,File.ReadAllText);
            Assert(before.Count==after.Count && before.All(entry=>after[entry.Key]==entry.Value));
        });
        Test("Alte Profile ohne Mod-Auswahl laden sicher als Einzelmod",()=>{
            var p=JsonSerializer.Deserialize<ModProject>("{\"Name\":\"Alt\"}",WorkspaceStore.JsonOptions)!;
            Assert(p.SourceMode==ModSourceMode.SingleMod && p.SelectedModFolders.Count==0);
        });
        Test("Mehrmod-Auswahl bleibt beim Speichern erhalten",()=>{
            var p=new ModProject{SourceMode=ModSourceMode.SelectedMods,SelectedModFolders=["A","B"]};var copy=WorkspaceStore.Clone(p);
            Assert(copy.SourceMode==ModSourceMode.SelectedMods && copy.SelectedModFolders.SequenceEqual(new[]{"A","B"}));
        });
        Test("Vorlage erzeugt direkt auswählbare Einzelmod",()=>{
            var path=TemplateCreator.Create(Fresh(),"rp-core","RP Core",GameTemplate.ProjectZomboid42);
            Assert(File.Exists(Path.Combine(path,"42","mod.info")) && !Directory.Exists(Path.Combine(path,"Contents")));
            Assert(ModSourceResolver.Resolve(app,Profile(path)).Sources.Single().TargetPrefix=="mods/rp-core");
        });
        Test("Strenge Metadatenprüfung: jede Version braucht eine ID",()=>{
            var path=Mod(Fresh(),"A",true);Directory.CreateDirectory(Path.Combine(path,"42.5"));File.WriteAllText(Path.Combine(path,"42.5","mod.info"),"name=A\n");
            Throws<InvalidDataException>(()=>builder.Validate(app,Profile(path),settings));
            builder.Validate(app,Profile(path),new StudioSettings{ValidateZomboidLayout=false});
        });
        AsyncTest("Generische App: originale relative Struktur bleibt erhalten",async()=>{
            var path=Fresh();File.WriteAllText(Path.Combine(path,"addon.txt"),"addon");var generic=new AppProfile{AppId=480,Template=GameTemplate.Generic};
            var p=await builder.BuildAsync(generic,Profile(path),ReleaseChannel.Test,settings,Guid.NewGuid(),null,CancellationToken.None);
            Assert(p.Files.Single().RelativePath=="addon.txt");
        });
        AsyncTest("Spielkopie innerhalb einer Mod wird ebenfalls blockiert",async()=>{
            var path=Mod(Fresh(),"A");var game=Path.Combine(path,"accidental-game-copy");Directory.CreateDirectory(game);File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"dummy");
            await ThrowsAsync<InvalidDataException>(()=>BuildPath(path));
        });
        AsyncTest("Mod mit numerischem Namen unter mods/ bleibt Einzelmod",async()=>{
            var mods=Path.Combine(Fresh(),"mods");var path=Mod(mods,"42");Mod(mods,"Other");
            var p=await BuildPath(path);Assert(p.Files.All(f=>f.RelativePath.StartsWith("mods/42/")));
        });
    }
}
