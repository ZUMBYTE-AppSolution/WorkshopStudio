using System.Diagnostics;
using WorkshopStudio.Core;

internal static partial class Program
{
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private static void RegisterSteamLoginTests()
    {
        Test("Login: Passwort ohne Zeilenumbruch und über Read-Grenze", () => {
            var detector = new SteamCmdPromptDetector();
            Assert(detector.Append("Cached credentials not found.\r\nPass") is null);
            Assert(detector.Append("word: ") == SteamLoginSignal.Password);
        });
        Test("Login: Guard-Varianten ohne Newline", () => {
            foreach (var prompt in new[] { "Steam Guard code:", "Two-factor code: ", "Auth code:",
                "Please enter the current code from your Steam Guard mobile authenticator app:",
                "Enter your code:", "Email code:", "Code: " })
                Assert(new SteamCmdPromptDetector().Append(prompt) == SteamLoginSignal.GuardCode, prompt);
        });
        Test("Login: schnelle Folge Passwort und Guard wird nicht verschluckt", () => {
            var detector = new SteamCmdPromptDetector();
            Assert(detector.Append("password:") == SteamLoginSignal.Password);
            Assert(detector.Append("Steam Guard code:") == SteamLoginSignal.GuardCode);
            Assert(detector.Append("Steam Guard code:") == SteamLoginSignal.GuardCode);
        });
        Test("Login: mobile Freigabe ist kein Code-Dialog", () => {
            var detector = new SteamCmdPromptDetector();
            Assert(detector.Append("Please confirm this login in the Steam Mobile app.") == SteamLoginSignal.MobileApproval);
        });
        Test("Login: erklärender Steam-Guard-Text fragt noch keinen Code ab", () => {
            Assert(new SteamCmdPromptDetector().Append("This computer has not been authenticated using Steam Guard.\nPlease check your email for the message from Steam.") is null);
        });
        Test("Login: Steam-API OK ist kein Login-Erfolg", () => {
            Assert(!SteamCmdPromptDetector.IsAuthenticated("Loading Steam API...OK\nConnecting anonymously to Steam Public...OK"));
            Assert(SteamCmdPromptDetector.IsAuthenticated("Logging in user 'test' to Steam Public...OK\nWaiting for user info...OK"));
            Assert(SteamCmdPromptDetector.IsAuthenticated("Waiting for user info...\r\nOK"));
        });
        Test("Login: Fehlertexte unterscheidbar", () => {
            Assert(SteamCmdPromptDetector.DescribeFailure("FAILED (InvalidPassword)", 5).Contains("Passwort"));
            Assert(SteamCmdPromptDetector.DescribeFailure("RateLimitExceeded", 5).Contains("begrenzt"));
            Assert(SteamCmdPromptDetector.DescribeFailure("NoConnection", 5).Contains("nicht erreichen"));
            Assert(SteamCmdPromptDetector.DescribeFailure("AccountLogonDenied", 5).Contains("Steam Guard"));
        });
        Test("Terminal: geteilte ANSI-Steuersequenzen entfernen", () => {
            var filter = new TerminalTextFilter();
            Assert(filter.Append("\u001b[3") == "");
            Assert(filter.Append("1mPass\u001b[0mword:") == "Password:");
            Assert(filter.Append("\u001b]0;not-a-password:\u001b") == "");
            Assert(filter.Append("\\OK") == "OK");
        });
        Test("Windows: Argumente mit Leerzeichen, Quotes und Backslash", () => {
            Assert(WindowsCommandLine.Quote(@"C:\my mods\") == "\"C:\\my mods\\\\\"");
            Assert(WindowsCommandLine.Quote("a\"b") == "\"a\\\"b\"");
            Throws<ArgumentException>(() => WindowsCommandLine.Quote("path\nquit"));
        });
        Test("Login: Startargumente enthalten weder Passwort noch Guard", () => {
            var args = SteamCmdRunner.CreateArguments("test_user", null);
            Assert(args.SequenceEqual(new[] { "+@ShutdownOnFailedCommand", "1", "+@NoPromptForPassword", "0", "+login", "test_user", "+quit" }));
            Throws<InvalidDataException>(() => SteamCmdRunner.CreateArguments("anonymous", null));
        });
        Test("Login: Steam3-Konto-ID wird verlustfrei als SteamID64 gelesen", () => {
            Assert(SteamCmdPromptDetector.ReadSteamId64("Account: test SteamID: [U:1:123456789]")==76561198083722517UL);
            Assert(SteamCmdPromptDetector.ReadSteamId64("Waiting for user info...OK") is null);
        });
        Test("Weblogin: originale Steam-Login-Domains erlaubt", () => {
            foreach (var url in new[] { "https://steamcommunity.com/login/home/", "https://login.steampowered.com/jwt/finalizelogin", "https://store.steampowered.com/login/" })
                Assert(SteamService.IsSteamBrowserUrl(url), url);
            foreach (var url in new[] { "https://steamcommunity.com.evil.example/login", "http://steamcommunity.com", "https://steamcommunity.com:1234/", "https://user:pass@steamcommunity.com/", "https://steamusercontent.com/login" })
                Assert(!SteamService.IsSteamBrowserUrl(url), url);
        });

        // Real subprocess/ConPTY regression tests. The child is THIS test executable in fake mode.
        // It never starts SteamCMD, sends network requests or uses a real Steam account.
        var executable = Environment.ProcessPath ?? "";
        if (Path.GetFileNameWithoutExtension(executable) == "dotnet")
        {
            Test("Prozess-Tests benötigen den .NET-Apphost", () => throw new InvalidOperationException("Selbsttests über Build.cmd oder dotnet run ausführen, nicht dotnet <test.dll>. UseAppHost muss aktiviert bleiben."));
            return;
        }
        async Task<SteamCmdResult> Run(string scenario, Func<SecretKind,CancellationToken,Task<string?>> provider,
            TimeSpan? timeout = null, CancellationToken ct = default) => await new SteamCmdRunner().RunAsync(
                executable, scenario, null, timeout ?? TimeSpan.FromSeconds(20), provider, null, ct);
        Task<string?> Credentials(SecretKind kind, CancellationToken _) => Task.FromResult<string?>(kind == SecretKind.Password ? "only-test-password" : "A1B2C");
        AsyncTest("Prozess: Passwort und Guard in echter Konsole", async () => {
            var result = await Run("ws_guard", Credentials);
            Assert(result.ExitCode == 0 && result.LoggedIn, result.Output);
            Assert(!result.Output.Contains("only-test-password") && !result.Output.Contains("A1B2C"));
        });
        AsyncTest("Prozess: Guard von stderr wird gelesen", async () => {
            var result = await Run("ws_stderr", Credentials);
            Assert(result.ExitCode == 0 && result.LoggedIn, result.Output);
        });
        AsyncTest("Prozess: mobile Freigabe benötigt keine Eingabe", async () => {
            var result = await Run("ws_mobile", (_,_) => throw new Exception("Unerwarteter Eingabedialog"));
            Assert(result.LoggedIn, result.Output);
        });
        AsyncTest("Prozess: gespeicherte Sitzung benötigt kein Passwort", async () => {
            var result = await Run("ws_cached", (_,_) => throw new Exception("Unerwarteter Eingabedialog"));
            Assert(result.LoggedIn && result.ExitCode == 0, result.Output);
        });
        AsyncTest("Prozess: ungültiges Passwort ist kein Erfolg", async () => {
            var result = await Run("ws_invalid", Credentials);
            Assert(!result.LoggedIn && result.ExitCode != 0, result.Output);
        });
        AsyncTest("Prozess: Nutzereingabe abbrechen ist kein Timeout", async () => {
            await ThrowsAsync<OperationCanceledException>(() => Run("ws_guard", (_,_) => Task.FromResult<string?>(null)));
        });
        AsyncTest("Prozess: Zeitlimit beendet wartenden Kindprozess", async () => {
            await ThrowsAsync<TimeoutException>(() => Run("ws_wait", Credentials, TimeSpan.FromMilliseconds(750)));
        });
        AsyncTest("Prozess: externer Abbruch bleibt OperationCanceledException", async () => {
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
            await ThrowsAsync<OperationCanceledException>(() => Run("ws_wait", Credentials, ct:cancel.Token));
        });
        AsyncTest("Prozess: Runner nach Abbruch wieder benutzbar", async () => {
            var runner = new SteamCmdRunner();
            await ThrowsAsync<OperationCanceledException>(() => runner.RunAsync(executable,"ws_guard",null,TimeSpan.FromSeconds(20),(_,_)=>Task.FromResult<string?>(null),null,CancellationToken.None));
            Assert(!runner.IsBusy);
            var result = await runner.RunAsync(executable,"ws_cached",null,TimeSpan.FromSeconds(20),Credentials,null,CancellationToken.None);
            Assert(result.LoggedIn && !runner.IsBusy);
        });
    }

    private static int FakeSteamCmd(string[] args)
    {
        var index = Array.IndexOf(args, "+login");
        var scenario = index >= 0 && index+1 < args.Length ? args[index+1] : "";
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        if (scenario == "ws_wait") { Console.WriteLine("Waiting for server..."); Thread.Sleep(TimeSpan.FromMinutes(2)); return 9; }
        Console.WriteLine("Loading Steam API...OK");
        if (scenario is "ws_guard" or "ws_stderr" or "ws_invalid")
        {
            Console.Write("Pass"); Console.Out.Flush(); Thread.Sleep(40);
            Console.Write("word: "); Console.Out.Flush();
            var password = ReadInput();
            if (password != "only-test-password") { Console.WriteLine("FAILED (InvalidPassword)"); return 5; }
            if (scenario == "ws_invalid") { Console.WriteLine("FAILED (InvalidPassword)"); return 5; }
            var writer = scenario == "ws_stderr" ? Console.Error : Console.Out;
            writer.Write("Steam Guard code: "); writer.Flush();
            if (ReadInput() != "A1B2C") { Console.WriteLine("FAILED (TwoFactorCodeMismatch)"); return 5; }
        }
        if (scenario == "ws_mobile") { Console.WriteLine("Please confirm this login in the Steam Mobile app."); Thread.Sleep(60); }
        Console.WriteLine("Logging in user 'test' to Steam Public...OK");
        Console.WriteLine("Waiting for user info...OK");
        if(args.Contains("+info"))Console.WriteLine("Account: test SteamID: [U:1:123456789]");
        Console.Out.Flush();
        return 0;
    }
    private static string ReadInput()
    {
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
        var value = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept:true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
            if (key.Key == ConsoleKey.Backspace && value.Length > 0) value.Length--;
            else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
        }
    }
}
