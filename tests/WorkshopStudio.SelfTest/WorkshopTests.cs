using System.Net;
using System.Text;
using System.Text.Json;
using WorkshopStudio.Core;

internal static partial class Program
{
    private static void RegisterWorkshopTests(string root)
    {
        const ulong owner=76561198056536755UL;
        static string Page(int total, params (ulong Id,uint AppId,string Title)[] items) =>
            "<div class=\"workshopBrowsePagingInfo\">Showing 1-30 of "+total+" entries</div><div class=\"workshopBrowseItems\">"+
            string.Concat(items.Select(i=>"<div class=\"workshopItem\"><a class=\"ugc\" data-appid=\""+i.AppId+
                "\" data-publishedfileid=\""+i.Id+"\"></a><div class=\"workshopItemTitle ellipsis\">"+
                WebUtility.HtmlEncode(i.Title)+"</div></div>"))+"</div>";
        Test("Portfolio: eigene Einträge mit AppID und Titel lesen",()=>{
            var page=WorkshopPortfolio.ParsePage(Page(1,(123,108600,"Ä & B")),owner);
            Assert(page.Recognized&&page.Total==1&&page.Items.Single().Title=="Ä & B");
            Assert(page.Items.Single().ConsumerAppId==108600&&page.Items.Single().OwnerSteamId64==owner);
        });
        AsyncTest("Portfolio: alle Seiten und Collections, Duplikate entfernt",async()=>{
            var calls=0;
            Task<string> Read(Uri uri,CancellationToken _) {
                calls++;
                if(uri.Query.Contains("section=collections"))return Task.FromResult(Page(1,(900,108600,"Sammlung")));
                if(uri.Query.Contains("p=2"))return Task.FromResult(Page(31,(31,480,"Neues Spiel")));
                var first=Enumerable.Range(1,30).Select(i=>((ulong)i,108600u,"Mod "+i)).ToArray();
                return Task.FromResult(Page(31,first));
            }
            var result=await new WorkshopPortfolio().ScanAsync(owner,Read,null,null,CancellationToken.None);
            Assert(result.Report.Complete&&result.Report.PagesRead==3&&result.Items.Count==32&&calls==3);
            Assert(result.Items.Single(i=>i.Id==900).FileType==2);
            Assert(result.Items.Single(i=>i.Id==31).ConsumerAppId==480);
        });
        AsyncTest("Portfolio: Teilausfall löscht erste Ergebnisse nicht",async()=>{
            Task<string> Read(Uri uri,CancellationToken _) => uri.Query.Contains("p=2")
                ? Task.FromException<string>(new IOException("Seite 2 nicht abrufbar"))
                : Task.FromResult(Page(31,(1,108600,"Vorhanden")));
            var result=await new WorkshopPortfolio().ScanAsync(owner,Read,null,null,CancellationToken.None);
            Assert(!result.Report.Complete&&result.Items.Count==1&&result.Report.PagesRead==1&&result.Report.Error.Contains("Seite 2"));
        });
        Test("VDF: Bestandsupdate schützt alle nicht gewählten Metadaten",()=>{
            var app=new AppProfile{AppId=108600};var mod=new ModProject{AppProfileId=app.Id,WorkshopId=123,
                Name="Nicht schreiben",Description="Rohtext",Visibility=2,ChangeNote="Notiz"};
            var fields=VdfWriter.ReadFlat(VdfWriter.Build(app,mod,ReleaseChannel.Live,root,"",[]));
            Assert(fields["publishedfileid"]=="123"&&fields.ContainsKey("contentfolder"));
            Assert(!fields.ContainsKey("title")&&!fields.ContainsKey("description")&&!fields.ContainsKey("visibility")&&
                !fields.ContainsKey("previewfile")&&!fields.ContainsKey("changenote"));
            var chosen=VdfWriter.ReadFlat(VdfWriter.Build(app,mod,ReleaseChannel.Live,root,"",["description"]));
            Assert(chosen["description"]=="Rohtext"&&!chosen.ContainsKey("title"));
        });
        Test("Workshop-Schema: lokale Version, Bild und Changelog nur aus gewählter Mod",()=>{
            var own=Path.Combine(root,"metadata","mods","own");var other=Path.Combine(root,"metadata","mods","other");
            Directory.CreateDirectory(own);Directory.CreateDirectory(other);
            File.WriteAllText(Path.Combine(own,"mod.info"),"id=own\nversion=2.4\n");
            File.WriteAllText(Path.Combine(own,"CHANGELOG.md"),"## 2.4\nKorrektur");
            File.WriteAllBytes(Path.Combine(own,"poster.png"),Enumerable.Repeat((byte)1,32).ToArray());
            File.WriteAllText(Path.Combine(other,"mod.info"),"id=other\nversion=99\n");
            var app=new AppProfile{AppId=108600};var mod=new ModProject{AppProfileId=app.Id,ContentFolder=own};
            var found=LocalWorkshopSchemaReader.Inspect(app,mod);
            Assert(found.Version=="2.4"&&found.PreviewPath.EndsWith("poster.png")&&found.Changelog.Contains("Korrektur"));
            Assert(found.ModIds.SequenceEqual(["own"])&&!found.Summary.Contains(other,StringComparison.OrdinalIgnoreCase));
        });
        Test("Workshop-Schema: Wrapper-Metadaten ändern die Paketauswahl nicht",()=>{
            var wrapper=Path.Combine(root,"wrapped");var own=Path.Combine(wrapper,"Contents","mods","own");
            Directory.CreateDirectory(own);File.WriteAllText(Path.Combine(own,"mod.info"),"id=own\n");
            File.WriteAllText(Path.Combine(wrapper,"workshop.txt"),"title=Mein Workshop\ndescription=Originaltext\n");
            var app=new AppProfile{AppId=108600};var mod=new ModProject{AppProfileId=app.Id,ContentFolder=wrapper};
            var found=LocalWorkshopSchemaReader.Inspect(app,mod);
            Assert(found.Title=="Mein Workshop"&&found.Description=="Originaltext");
            Assert(ModSourceResolver.Resolve(app,mod).Sources.Single().SourceRoot==own);
        });
        Test("Steam-Changelog: lesbare Änderungsnotizen",()=>{
            var html="<div class=\"detailBox workshopAnnouncement noFooter changeLogCtn\"><div class=\"changelog headline\">Update: Heute</div>"+
                "<p id=\"123\">Neue Funktion<br>Fehler &amp; Korrektur</p></div>";
            var notes=SteamService.ParseChangeNotes(html);
            Assert(notes.Count==1&&notes.Text.Contains("Heute")&&notes.Text.Contains("Neue Funktion\nFehler & Korrektur"));
        });
        Test("Workspace: alter Export lässt sich ohne Quelländerung migrieren",()=>{
            var exported=new Workspace{SchemaVersion=1};WorkspaceStore.UpgradeSchema1(exported);
            WorkspaceStore.Validate(exported);Assert(exported.SchemaVersion==2);
        });
        Test("Import: eigene bestätigte Mods und Collections ohne Upload übernehmen",()=>{
            var workspace=new Workspace();var account=new SteamAccount{UserName="owner"};
            workspace.Accounts.Add(account);workspace.Settings.ActiveAccountId=account.Id;
            var items=new[] {
                new RemoteWorkshopItem{Id=111,OwnerSteamId64=owner,ConsumerAppId=108600,FileType=0,
                    Title="Meine Mod",Description="Steam-Original",FieldStatus=new(){{"owner",RemoteFieldStatus.Known}}},
                new RemoteWorkshopItem{Id=222,OwnerSteamId64=owner,ConsumerAppId=108600,FileType=2,
                    Title="Sammlung",FieldStatus=new(){{"owner",RemoteFieldStatus.Known}}},
                new RemoteWorkshopItem{Id=333,OwnerSteamId64=owner,ConsumerAppId=108600,FileType=0,
                    Title="Privat",FieldStatus=new(){{"details",RemoteFieldStatus.AccessDenied}}}
            };
            var result=new PortfolioResult(items,new WorkshopScanReport{SteamId64=owner,PagesRead=2,ItemsSeen=3});
            var summary=WorkshopImporter.Merge(workspace,account,result,owner);
            Assert(summary.AddedRemote==3&&summary.AddedMods==1&&summary.AddedCollections==1&&summary.ReadOnly==1);
            Assert(workspace.Mods.Single().WorkshopId==111&&workspace.Mods.Single().ContentFolder==""&&
                !workspace.Mods.Single().AutoPublish&&workspace.Collections.Single().SteamCollectionId==222);
            Assert(workspace.RemoteItems.Count==3&&account.VerifiedWebSteamId64==owner&&account.VerifiedUploadSteamId64==owner);
            var again=WorkshopImporter.Merge(workspace,account,result,owner);
            Assert(again.AddedMods==0&&again.AddedCollections==0&&workspace.RemoteItems.Count==3);
            Throws<InvalidOperationException>(()=>WorkshopImporter.Merge(workspace,account,result,owner+1));
        });
        AsyncTest("Workspace: Schema-1-Migration sichert Original und IDs",async()=>{
            var dir=Path.Combine(root,"migration");
            var store=new WorkspaceStore(dir);
            var old=new Workspace{SchemaVersion=1,Accounts=[new SteamAccount{UserName="test_user"}]};
            old.Mods.Add(new ModProject{AppProfileId=old.Apps[0].Id,WorkshopId=12345678901234567UL});
            var original=JsonSerializer.Serialize(old,WorkspaceStore.JsonOptions);
            await File.WriteAllTextAsync(store.FilePath,original);
            var migrated=await store.LoadAsync();
            Assert(migrated.SchemaVersion==2&&migrated.Mods.Single().WorkshopId==12345678901234567UL&&migrated.Accounts.Count==1);
            var backup=Directory.GetFiles(store.BackupRoot,"workspace-schema1-*.json").Single();
            Assert(await File.ReadAllTextAsync(backup)==original);
            await store.SaveAsync(migrated);
            Assert((await store.LoadAsync()).SchemaVersion==2);
            Assert(Directory.GetFiles(store.BackupRoot,"workspace-schema1-*.json").Length==1);
        });
        AsyncTest("Config: Originaltext, Unicode, statische Werte und Warnung",async()=>{
            var dir=Path.Combine(root,"configmod");Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir,"mod.info"),"id=rp-core\nname=RP Core\n");
            var original="; Kommentar\n[Server]\nBegrüßung=Grüße\npassword=geheim\n";
            await File.WriteAllTextAsync(Path.Combine(dir,"server.ini"),original,new UTF8Encoding(true));
            await File.WriteAllTextAsync(Path.Combine(dir,"config.json"),"{\"active\":true,\"count\":3}");
            await File.WriteAllTextAsync(Path.Combine(dir,"config.lua"),"max = 3\ncalc = GetValue()\n");
            var app=new AppProfile{AppId=108600};
            var mod=new ModProject{AppProfileId=app.Id,ContentFolder=dir,WorkshopId=42};
            var result=await ConfigDiscovery.FindAsync(app,mod,512,CancellationToken.None);
            Assert(result.Files.Count==3&&result.State=="Bereit");
            var ini=result.Files.Single(f=>f.Format=="INI");
            Assert(ini.RawText==original&&ini.Options.Any(o=>o.Key=="Begrüßung"&&o.Value=="Grüße")&&ini.PossibleSecrets);
            Assert(result.Files.Single(f=>f.Format=="LUA").Options.Count==1);
            Assert(result.ServerLines.Contains("WorkshopItems=42")&&result.ServerLines.Contains("Mods=rp-core"));
        });
        AsyncTest("Config: fehlende Quelle bleibt von keinem Fund unterscheidbar",async()=>{
            var app=new AppProfile{AppId=108600};
            var missing=await ConfigDiscovery.FindAsync(app,new ModProject{AppProfileId=app.Id,ContentFolder="",WorkshopId=42},512,CancellationToken.None);
            Assert(missing.State=="Lokale Quelle fehlt"&&missing.ServerLines.Contains("Mods=[Mod-ID"));
            var dir=Path.Combine(root,"emptyconfig");Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir,"mod.info"),"id=empty\n");
            var empty=await ConfigDiscovery.FindAsync(app,new ModProject{AppProfileId=app.Id,ContentFolder=dir},512,CancellationToken.None);
            Assert(empty.State=="Keine Konfiguration gefunden");
        });
    }

    private static async Task<int> LivePublicPortfolioAsync()
    {
        const ulong id=76561198329871201UL; // Public workshop profile, never the user's account.
        using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WorkshopStudio-ReadOnly-Test/1.1");
        var store=new WorkspaceStore(Path.Combine(Path.GetTempPath(),"WorkshopStudio-public-"+Guid.NewGuid().ToString("N")));
        using var steam=new SteamService(store);
        var result=await new WorkshopPortfolio().ScanAsync(id,
            (url,ct)=>http.GetStringAsync(url,ct),steam.EnrichWorkshopItemsAsync,
            new ConsoleProgress(),CancellationToken.None);
        Console.WriteLine($"Öffentliches Profil: {result.Report.Summary}; AppIDs={result.Items.Select(x=>x.ConsumerAppId).Distinct().Count()}; " +
            "Eigentümer per API bestätigt="+result.Items.Count(i=>i.FieldStatus.GetValueOrDefault("owner")==RemoteFieldStatus.Known));
        return result.Report.PagesRead>1&&result.Items.Count>30?0:1;
    }
    private sealed class ConsoleProgress : IProgress<PortfolioProgress>
    { public void Report(PortfolioProgress p)=>Console.WriteLine($"Seite {p.Pages}: {p.Found} Einträge ({p.Phase})"); }

    private static async Task<int> CheckLocalSessionAsync()
    {
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorkshopStudio","workspace.json");
        if(!File.Exists(path)){Console.WriteLine("Kein lokaler WorkshopStudio-Arbeitsbereich.");return 2;}
        var workspace=JsonSerializer.Deserialize<Workspace>(await File.ReadAllTextAsync(path),WorkspaceStore.JsonOptions);
        var account=workspace?.Accounts.FirstOrDefault(a=>a.Id==workspace.Settings.ActiveAccountId);
        if(account is null||!File.Exists(workspace!.Settings.SteamCmdPath)){Console.WriteLine("Aktives Konto oder SteamCMD fehlt.");return 2;}
        try {
            var result=await new SteamCmdRunner().RunAsync(workspace.Settings.SteamCmdPath,account.UserName,null,
                TimeSpan.FromMinutes(2),(_,_)=>Task.FromResult<string?>(null),null,CancellationToken.None);
            Console.WriteLine("SteamCMD-Sitzung bestätigt: "+(result.ExitCode==0&&result.LoggedIn)+
                "; SteamID64 per info geprüft: "+(result.SteamId64 is not null));
            return result.ExitCode==0&&result.LoggedIn&&result.SteamId64 is not null?0:1;
        } catch(OperationCanceledException) {Console.WriteLine("SteamCMD fordert interaktive Zugangsdaten oder Steam Guard. Sitzung nicht bestätigt.");return 1;}
        catch(Exception ex) {Console.WriteLine("SteamCMD-Prüfung fehlgeschlagen: "+ex.GetType().Name);return 1;}
    }
}
