using System.Xml.Linq;
using System.Text.Json;
using WorkshopStudio.Core;

// No test framework dependency, no network requests, no Steam uploads.
// dotnet run --project tests/WorkshopStudio.SelfTest -c Release
internal static partial class Program
{
    private static readonly List<(string Name, Func<Task> Run)> Tests = [];
    private static void Test(string name, Action body) => Tests.Add((name, () => { body(); return Task.CompletedTask; }));
    private static void AsyncTest(string name, Func<Task> body) => Tests.Add((name, body));
    private static void Assert(bool condition, string message = "Assertion fehlgeschlagen") { if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Erwartete Ausnahme {typeof(T).Name} blieb aus.");
    }
    private static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new Exception($"Erwartete Ausnahme {typeof(T).Name} blieb aus.");
    }

    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("+login")) return FakeSteamCmd(args);
        if (args.Contains("--live-public-portfolio")) return await LivePublicPortfolioAsync();
        if (args.Contains("--check-local-session")) return await CheckLocalSessionAsync();
        if(args.Length==2&&args[0]=="--backup-own-workshop")return await LiveBackupDiagnosticAsync(args[1]);
        RegisterSteamLoginTests();
        var root = Path.Combine(Path.GetTempPath(), "WorkshopStudio-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var store = new WorkspaceStore(Path.Combine(root, "studio"));
        var builder = new PackageBuilder(store);
        var content = Path.Combine(root, "sources", "content");
        Directory.CreateDirectory(content);
        File.WriteAllText(Path.Combine(content, "main.lua"), "return { version = '1' }\n");
        var app = new AppProfile { AppId = 480, Template = GameTemplate.Generic };
        var mod = new ModProject { AppProfileId = app.Id, Name = "Testmod", ContentFolder = content, Description = "Äöü \"Zitat\"\nZeile 2", Version = "1.2.3", WorkshopId = 123, TestWorkshopId = 456, Visibility = 0 };
        var settings = new StudioSettings();
        Task<PackageSnapshot> Build(ModProject? m = null) => builder.BuildAsync(app, m ?? mod, ReleaseChannel.Test, settings, Guid.NewGuid(), null, CancellationToken.None);

        RegisterModSourceTests(root,builder,store);
        RegisterWorkshopTests(root);
        RegisterReleaseUpdaterTests(root);
        RegisterMetadataBackupTests(root);

        Test("VDF: Escape und Roundtrip", () => {
            var vdf = VdfWriter.Build(app, mod, ReleaseChannel.Live, content, "");
            var parsed = VdfWriter.ReadFlat(vdf);
            Assert(parsed["description"] == mod.Description);
            Assert(parsed["contentfolder"] == Path.GetFullPath(content));
            Assert(parsed["changenote"] == "Update 1.2.3");
        });
        Test("VDF: bestehende ID statt Neuanlage", () => Assert(VdfWriter.ReadPublishedId(VdfWriter.Build(app, mod, ReleaseChannel.Live, content, "")) == 123));
        Test("VDF: eigener Testeintrag ist privat", () => {
            var fields = VdfWriter.ReadFlat(VdfWriter.Build(app, mod, ReleaseChannel.Test, content, ""));
            Assert(fields["publishedfileid"] == "456" && fields["visibility"] == "2");
            Assert(fields["title"].EndsWith(" [TEST]"));
        });
        Test("VDF: fehlende ID ist explizit null", () => Assert(VdfWriter.ReadPublishedId("\"workshopitem\" {}") == 0));
        Test("VDF: keine erfundenen Tag-Felder", () => Assert(!VdfWriter.ReadFlat(VdfWriter.Build(app, mod, ReleaseChannel.Live, content, "")).ContainsKey("tags")));
        Test("Globs: Verzeichnisse und Wildcards", () => {
            Assert(PackageBuilder.IsExcluded(".git/config", ".git/", false));
            Assert(PackageBuilder.IsExcluded("nested/obj/cache", "obj/", false));
            Assert(PackageBuilder.IsExcluded("nested/file.tmp", "*.tmp", false));
            Assert(!PackageBuilder.IsExcluded("media/main.lua", "*.tmp\n# Kommentar", false));
        });
        Test("Secrets: Env, Schlüssel und Credential-Ordner", () => {
            foreach (var path in new[] {".env", ".env.production", "x/secrets.json", "x/client.pem", ".ssh/config", ".aws/credentials"})
                Assert(PackageBuilder.IsExcluded(path, "", true), path);
            Assert(!PackageBuilder.IsExcluded("environment.lua", "", true));
        });
        Test("Secrets: Ausschluss bewusst abschaltbar", () => Assert(!PackageBuilder.IsExcluded(".env", "", false)));
        Test("Pfade: Präfix allein genügt nicht", () => {
            Assert(PackageBuilder.IsWithin(Path.Combine(root, "a", "b"), Path.Combine(root, "a")));
            Assert(!PackageBuilder.IsWithin(Path.Combine(root, "abc"), Path.Combine(root, "a")));
        });
        Test("Sicherheit: Workspace als Quelle gesperrt", () => {
            var clone = WorkspaceStore.Clone(mod); clone.ContentFolder = store.Root;
            Throws<InvalidDataException>(() => builder.Validate(app, clone, settings));
        });
        Test("Steam-Namen: keine Argumentinjektion", () => {
            SteamCmdRunner.ValidateUserName("test_user-01");
            foreach (var name in new[] { "", "+quit", "user password", "x\nquit", "x;quit" })
                Throws<InvalidDataException>(() => SteamCmdRunner.ValidateUserName(name));
        });
        Test("HTTPS: nur echte Steam-Domains", () => {
            Assert(SteamService.IsSteamHttps("https://steamcommunity.com/login/home/"));
            Assert(SteamService.IsSteamHttps("https://cdn.akamai.steamstatic.com/a.jpg"));
            foreach (var url in new[] { "http://steamcommunity.com/", "https://steamcommunity.com.evil.invalid/", "https://evil.invalid/steamcommunity.com", "file:///C:/x", "https://steamcommunity.com:8443/" })
                Assert(!SteamService.IsSteamHttps(url), url);
        });
        Test("Redaction: Passwort und Tokens", () => {
            var redactor = new SecretRedactor(); redactor.Add("hunter-secret-123");
            var result = redactor.Redact("hunter-secret-123 access_token=abc123 password=x");
            Assert(!result.Contains("hunter-secret-123") && !result.Contains("abc123") && result.Contains("REDACTED"));
        });
        Test("Redaction: Passwort über zwei Prozess-Chunks", () => {
            var redactor = new SecretRedactor(); redactor.Add("long-password-123");
            var lines = new List<string>(); var safe = new SafeLogBuffer(redactor, lines.Add);
            safe.Append("Echo: long-pass"); Assert(lines.Count == 0);
            safe.Append("word-123\nFinished\n");
            Assert(lines.Count == 2 && lines[0] == "Echo: [REDACTED]" && lines[1] == "Finished");
        });
        Test("Redaction: letzte Zeile ohne Newline", () => {
            var redactor = new SecretRedactor(); redactor.Add("abcdsecret");
            var lines = new List<string>(); var safe = new SafeLogBuffer(redactor, lines.Add);
            safe.Append("abcd"); safe.Append("secret"); safe.Flush();
            Assert(lines.Single() == "[REDACTED]");
        });
        Test("Settings: sichere Defaults", () => {
            WorkspaceStore.ValidateSettings(new StudioSettings());
            Assert(!settings.AutomationEnabled && !settings.AllowLiveAutomation && settings.DefaultChannel == ReleaseChannel.Test);
        });
        Test("Settings: Live-Automation benötigt Freigabe", () => {
            var s = new StudioSettings { AutomationEnabled = true, AutomationChannel = ReleaseChannel.Live };
            Throws<InvalidDataException>(() => WorkspaceStore.ValidateSettings(s));
        });
        Test("Settings: ungültige Zeit, Größe, Skala und Enum", () => {
            foreach (var s in new[] {new StudioSettings { DailyTime = "25:99" }, new StudioSettings { MaxPackageMiB = 0 }, new StudioSettings { UiScale = double.NaN }, new StudioSettings { DefaultChannel = (ReleaseChannel)999 }})
                Throws<InvalidDataException>(() => WorkspaceStore.ValidateSettings(s));
        });
        Test("Settings: Update-Quelle muss HTTPS sein", () => Throws<InvalidDataException>(() => WorkspaceStore.ValidateSettings(new StudioSettings { UpdateManifestUrl = "http://example.invalid/update.json" })));
        Test("Workspace: doppelte AppIDs abweisen", () => {
            var w = new Workspace(); w.Apps.Add(new AppProfile());
            Throws<InvalidDataException>(() => WorkspaceStore.Validate(w));
        });
        Test("Workspace: fremde Mod in Collection abweisen", () => {
            var w = new Workspace { Apps = [app], Mods = [mod], Collections = [new WorkshopCollection { AppProfileId = app.Id, ModProjectIds = [Guid.NewGuid()] }] };
            Throws<InvalidDataException>(() => WorkspaceStore.Validate(w));
        });
        Test("Workspace: keine geheimen Konto-Felder im JSON", () => {
            var json = JsonSerializer.Serialize(new SteamAccount { UserName = "test_user", RememberPassword = true }, WorkspaceStore.JsonOptions);
            Assert(!json.Contains("\"Password\":") && !json.Contains("access_token"));
        });
        AsyncTest("Workspace: Speichern, Laden und .bak", async () => {
            var w = new Workspace { Apps = [app], Mods = [mod] };
            await store.SaveAsync(w); w.Mods[0].Notes = "Version 2"; await store.SaveAsync(w);
            var loaded = await store.LoadAsync();
            Assert(loaded.Mods.Single().Notes == "Version 2" && File.Exists(store.FilePath + ".bak"));
        });
        AsyncTest("Workspace: unterbrochener Upload wird nicht fortgesetzt", async () => {
            var w = new Workspace { Apps = [app], Mods = [mod], Jobs = [new PublishJob { ModId = mod.Id, Status = JobStatus.Uploading }] };
            await store.SaveAsync(w); var loaded = await store.LoadAsync();
            Assert(loaded.Jobs.Single().Status == JobStatus.Interrupted);
        });
        AsyncTest("Paket: identischer Inhalt erzeugt denselben Hash", async () => {
            var a = await Build(); var b = await Build();
            Assert(a.Fingerprint == b.Fingerprint && a.Files.Count > 0);
        });
        AsyncTest("Paket: Inhaltsänderung ändert SHA-256", async () => {
            var a = await Build(); File.WriteAllText(Path.Combine(content, "new.lua"), "return 42"); var b = await Build();
            Assert(a.Fingerprint != b.Fingerprint); File.Delete(Path.Combine(content, "new.lua"));
        });
        AsyncTest("Paket: Metadatenänderung ändert Hash", async () => {
            var a = await Build(); var clone = WorkspaceStore.Clone(mod); clone.Description += " neu"; var b = await Build(clone);
            Assert(a.Fingerprint != b.Fingerprint);
        });
        AsyncTest("Paket: ausgeschlossene Dateien verändern Hash nicht", async () => {
            var a = await Build(); File.WriteAllText(Path.Combine(content, ".env"), "DUMMY=test"); File.WriteAllText(Path.Combine(content, "ignore.tmp"), "test");
            var b = await Build(); Assert(a.Fingerprint == b.Fingerprint && b.Files.All(f => !f.RelativePath.EndsWith(".tmp")));
        });
        AsyncTest("Paket: ausgeschlossener Ordner wird übersprungen", async () => {
            var dir = Path.Combine(content, ".git"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "config"), "dummy");
            var snapshot = await Build(); Assert(snapshot.Files.All(f => !f.RelativePath.StartsWith(".git/")));
        });
        AsyncTest("Paket: vorheriger Abbruch erzeugt kein Uploadpaket", async () => {
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await ThrowsAsync<OperationCanceledException>(() => builder.BuildAsync(app, mod, ReleaseChannel.Test, settings, Guid.NewGuid(), null, cancel.Token));
        });
        AsyncTest("Paket: Größenlimit erzwingen", async () => {
            var small = WorkspaceStore.Clone(settings); small.MaxFileCount = 1;
            File.WriteAllText(Path.Combine(content, "second.lua"), "return 2");
            try { await ThrowsAsync<InvalidDataException>(() => builder.BuildAsync(app, mod, ReleaseChannel.Test, small, Guid.NewGuid(), null, CancellationToken.None)); }
            finally { File.Delete(Path.Combine(content, "second.lua")); }
        });
        AsyncTest("Löschen: nur eigene Staging-Ordner", async () => {
            Throws<InvalidOperationException>(() => builder.DeleteOwnedStage(content));
            Throws<InvalidOperationException>(() => builder.DeleteOwnedStage(store.StagingRoot));
            var snapshot = await Build(); builder.DeleteOwnedStage(snapshot.Root); Assert(!Directory.Exists(snapshot.Root) && Directory.Exists(content));
        });
        AsyncTest("Backup: ZIP wird angelegt", async () => {
            var snapshot = await Build(); builder.Backup(snapshot, mod, ReleaseChannel.Test, 2);
            Assert(Directory.EnumerateFiles(store.BackupRoot, "*.zip", SearchOption.AllDirectories).Any());
        });
        Test("Vorlage: Zomboid 42 und common-Inhalt", () => {
            var dir = TemplateCreator.Create(Path.Combine(root, "templates"), "rp-core", "RP Core", GameTemplate.ProjectZomboid42);
            Assert(File.Exists(Path.Combine(dir, "42", "mod.info")));
            Assert(File.Exists(Path.Combine(dir, "common", "README.txt")));
            Throws<IOException>(() => TemplateCreator.Create(Path.Combine(root, "templates"), "rp-core", "X", GameTemplate.ProjectZomboid42));
        });
        Test("Vorlage: IDs dürfen keine Pfade sein", () => Throws<InvalidDataException>(() => TemplateCreator.Create(root, "../unsafe", "X", GameTemplate.Generic)));
        Test("Collection: IDs je Kanal und dedupliziert", () => {
            var c = new WorkshopCollection { AppProfileId = app.Id, ModProjectIds = [mod.Id], ExternalWorkshopIds = [123, 999] };
            Assert(CollectionExporter.ResolveIds(c, [mod], ReleaseChannel.Live).SequenceEqual(new ulong[] {123, 999}));
            Assert(CollectionExporter.ResolveIds(c, [mod], ReleaseChannel.Test).First() == 456);
        });
        Test("Collection: unveröffentlichte Mods blockieren Export", () => {
            var clone = WorkspaceStore.Clone(mod); clone.WorkshopId = 0;
            Throws<InvalidDataException>(() => CollectionExporter.ResolveIds(new WorkshopCollection { AppProfileId = app.Id, ModProjectIds = [clone.Id] }, [clone], ReleaseChannel.Live));
        });
        Test("Collection: Zomboid-INI trennt Workshop-ID und Mod-ID", () => {
            var clone = WorkspaceStore.Clone(mod); clone.ModIds = "rp-core;rp-jobs";
            var ini = CollectionExporter.ZomboidIni(new WorkshopCollection { AppProfileId = app.Id, ModProjectIds = [clone.Id] }, [clone], ReleaseChannel.Live);
            Assert(ini.Contains("WorkshopItems=123") && ini.Contains("Mods=rp-core;rp-jobs"));
        });

        // Source-level theme contracts: intentionally independent of a Windows desktop.
        // Rendering, interaction and OS-dialog appearance still require the Windows test plan.
        string themeSourceRoot = Path.Combine(AppContext.BaseDirectory, "ThemeSources");
        XNamespace ui = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xa = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Theme() => XDocument.Load(Path.Combine(themeSourceRoot, "Themes", "StudioTheme.xaml")).Root!;
        IEnumerable<XElement> Screens() => Directory.EnumerateFiles(themeSourceRoot, "*.xaml", SearchOption.AllDirectories)
            .Select(file => XDocument.Load(file).Root!);
        XElement ControlStyle(string target) => Theme().Elements(ui + "Style")
            .Single(element => (string?)element.Attribute("TargetType") == target && element.Attribute(xa + "Key") is null);

        Test("Theme: alle Fenster haben expliziten StudioWindow-Stil", () => {
            var screens = Screens().Where(element => element.Name == ui + "Window").ToArray();
            Assert(screens.Length >= 9);
            foreach (var element in screens)
                Assert((string?)element.Attribute("Style") == "{StaticResource StudioWindow}", (string?)element.Attribute(xa + "Class") ?? "Window");
        });
        Test("Theme: alle Seiten haben expliziten StudioView-Stil", () => {
            var screens = Screens().Where(element => element.Name == ui + "UserControl").ToArray();
            Assert(screens.Length >= 10);
            foreach (var element in screens)
                Assert((string?)element.Attribute("Style") == "{StaticResource StudioView}");
        });
        Test("Theme: Fensterhintergrund und Titelleistenanbindung", () => {
            var window = Theme().Elements(ui + "Style").Single(element => (string?)element.Attribute(xa + "Key") == "StudioWindow");
            Assert(window.Elements(ui + "Setter").Any(element => (string?)element.Attribute("Property") == "Background" && (string?)element.Attribute("Value") == "{DynamicResource WindowBrush}"));
            Assert(window.Elements(ui + "Setter").Any(element => (string?)element.Attribute("Property") == "local:WindowTheme.IsEnabled" && (string?)element.Attribute("Value") == "True"));
        });
        Test("Theme: kritische Steuerelemente besitzen eigene Vorlagen", () => {
            foreach (var target in new[] { "PasswordBox", "ComboBox", "ComboBoxItem", "TabControl", "ListBox", "ListBoxItem", "DataGridColumnHeader", "ScrollViewer", "ToolTip", "ContextMenu", "MenuItem" })
                Assert(ControlStyle(target).Elements(ui + "Setter").Any(element => (string?)element.Attribute("Property") == "Template"), target);
        });
        Test("Theme: erforderliche WPF-Vorlagenteile bleiben vorhanden", () => {
            var parts = new Dictionary<string, string[]> {
                ["TextBox"] = ["PART_ContentHost"], ["PasswordBox"] = ["PART_ContentHost"],
                ["ComboBox"] = ["PART_Popup", "PART_EditableTextBox"], ["TabControl"] = ["PART_SelectedContentHost"],
                ["ScrollViewer"] = ["PART_ScrollContentPresenter", "PART_VerticalScrollBar", "PART_HorizontalScrollBar"],
                ["ScrollBar"] = ["PART_Track"], ["DataGridColumnHeader"] = ["PART_LeftHeaderGripper", "PART_RightHeaderGripper"]
            };
            foreach (var entry in parts)
            {
                var names = ControlStyle(entry.Key).Descendants().Attributes(xa + "Name").Select(attribute => attribute.Value).ToHashSet();
                foreach (var name in entry.Value) Assert(names.Contains(name), entry.Key + ": " + name);
            }
        });
        Test("Theme: Referenz-Design verwendet neutrale Anthrazit-Palette", () => {
            var expected = new Dictionary<string, string> { ["WindowBrush"] = "#111416", ["SidebarBrush"] = "#14181B", ["PanelBrush"] = "#1A1F23", ["InputBrush"] = "#121619", ["AccentBrush"] = "#FF781F" };
            foreach (var entry in expected)
                Assert(Theme().Elements(ui + "SolidColorBrush").Any(element => (string?)element.Attribute(xa + "Key") == entry.Key && (string?)element.Attribute("Color") == entry.Value));
        });
        Test("Theme: Text erbt Kontrast statt pauschal heller Vordergrundfarbe", () => {
            Assert(!ControlStyle("TextBlock").Elements(ui + "Setter").Any(element => (string?)element.Attribute("Property") == "Foreground"));
        });
        Test("Theme: WebView2 hat keinen weißen Initialhintergrund", () => {
            XNamespace web = "clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf";
            var browser = XDocument.Load(Path.Combine(themeSourceRoot, "Views", "BrowserWindow.xaml")).Descendants(web + "WebView2").Single();
            Assert((string?)browser.Attribute("DefaultBackgroundColor") == "#0C0E13");
        });

        var failed = 0;
        try {
            Console.WriteLine($"Workshop Studio Selbsttests · {Tests.Count} Tests\nKeine Netzwerkzugriffe. Temporär: {root}\n");
            foreach (var test in Tests) {
                try { await test.Run(); Console.WriteLine("PASS  " + test.Name); }
                catch (Exception ex) { failed++; Console.WriteLine("FAIL  " + test.Name + "\n      " + ex); }
            }
            Console.WriteLine($"\nErgebnis: {Tests.Count - failed}/{Tests.Count} bestanden.");
            return failed == 0 ? 0 : 1;
        } finally {
            try { Directory.Delete(root, true); } catch (Exception ex) { Console.WriteLine("Temporäre Testdaten konnten nicht vollständig gelöscht werden: " + ex.Message); }
        }
    }
}
