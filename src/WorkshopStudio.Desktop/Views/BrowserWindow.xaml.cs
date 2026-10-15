using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using WorkshopStudio.Core;
using WorkshopStudio.Desktop.Services;
using WorkshopStudio.Desktop.ViewModels;

namespace WorkshopStudio.Desktop.Views;

public partial class BrowserWindow : Window
{
    private readonly string _root, _url;
    private readonly SteamAccount? _scanAccount;
    private readonly SteamService? _scanService;
    private readonly int _scanTimeoutSeconds;
    private CancellationTokenSource? _scanCancel;
    private readonly bool _remember, _clear;
    private readonly DispatcherTimer _sessionTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _closed, _checking, _navigationFailed;
    public ulong SelectedWorkshopId { get; private set; }
    public bool IsSteamWebSignedIn { get; private set; }
    public string WebAccountName { get; private set; } = "";
    public string LastError { get; private set; } = "";
    public PortfolioResult? ScanResult { get; private set; }

    public BrowserWindow(string root, bool remember, string url, string title, bool select, bool clear = false, Guid? accountId = null,
        SteamAccount? scanAccount = null, SteamService? scanService = null, int scanTimeoutSeconds = 30)
    {
        InitializeComponent();
        _root = root; _remember = remember; _url = url; _clear = clear; Title = title;
        _scanAccount = scanAccount; _scanService = scanService;
        _scanTimeoutSeconds = scanTimeoutSeconds;
        SelectButton.Visibility = select ? Visibility.Visible : Visibility.Collapsed;
        ScanButton.Visibility = scanAccount is null ? Visibility.Collapsed : Visibility.Visible;
        ApplyBrowserTheme();
        ThemeService.ThemeChanged += ThemeChanged;
        // Keep Valve cookies inside WebView2. No token copying to SteamCMD and no password scraping.
        Browser.CreationProperties = new CoreWebView2CreationProperties {
            UserDataFolder = Path.Combine(root, "webview"),
            ProfileName = accountId is { } id ? "account_" + id.ToString("N") : "Default",
            IsInPrivateModeEnabled = !remember
        };
        _sessionTimer.Tick += async (_, _) => await CheckWebSessionAsync();
    }

    private async Task<string> ReadSteamPageAsync(Uri url, CancellationToken ct)
    {
        if (url.Scheme != "https" || url.Host != "steamcommunity.com") throw new InvalidDataException("Nur Steam Community darf gelesen werden.");
        var core = Browser.CoreWebView2 ?? throw new InvalidOperationException("Steam-Webfenster ist noch nicht bereit.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_scanTimeoutSeconds));
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(object? sender, CoreWebView2NavigationCompletedEventArgs args) => done.TrySetResult(args.IsSuccess);
        core.NavigationCompleted += Completed;
        try {
            using var registration = timeout.Token.Register(() => done.TrySetCanceled(timeout.Token));
            core.Navigate(url.AbsoluteUri);
            if (!await done.Task) throw new IOException("Steam-Seite konnte nicht geladen werden: " + url.AbsolutePath);
            var current = Browser.Source;
            if (current is null || current.Host != "steamcommunity.com" || current.AbsolutePath.Contains("/login/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Steam-Websitzung ist abgelaufen. Bitte erneut anmelden.");
            var encoded = await core.ExecuteScriptAsync("document.documentElement.outerHTML").WaitAsync(timeout.Token);
            var html = JsonSerializer.Deserialize<string>(encoded) ?? "";
            if (html.Length > 4_000_000) throw new InvalidDataException("Steam-Seite überschreitet die Größenbegrenzung.");
            return html;
        } finally { core.NavigationCompleted -= Completed; }
    }
    private async Task<ulong> ReadSignedInSteamIdAsync(CancellationToken ct)
    {
        // /my/ resolves inside the authenticated Steam browser to the current profile.
        // Never accept a profile ID merely typed or stored locally as proof of login.
        var html=await ReadSteamPageAsync(new Uri("https://steamcommunity.com/my/"),ct);
        var source=Browser.Source?.AbsoluteUri??"";
        var match=Regex.Match(source,@"/profiles/(?<id>7656119\d{10})(?:/|\?|$)",RegexOptions.IgnoreCase);
        if(!match.Success)match=Regex.Match(html,"""["']steamid["']\s*:\s*["'](?<id>7656119\d{10})["']""",RegexOptions.IgnoreCase);
        if(match.Success&&ulong.TryParse(match.Groups["id"].Value,out var id))return id;
        throw new InvalidDataException("SteamID64 der angemeldeten Websitzung konnte nicht geprüft werden.");
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await TryScanAsync(true);
    public async Task<PortfolioResult?> TryScanAsync(bool closeOnSuccess)
    {
        if (_scanAccount is null || _scanService is null || _scanCancel is not null) return null;
        _scanCancel = new CancellationTokenSource();
        ScanButton.IsEnabled = false; CancelScanButton.Visibility = Visibility.Visible;
        try {
            await CheckWebSessionAsync();
            if (!IsSteamWebSignedIn) throw new InvalidOperationException("Zuerst im Steam-Webfenster anmelden. Ein offenes Fenster ist noch kein nachgewiesenes Konto.");
            var id = await ReadSignedInSteamIdAsync(_scanCancel.Token);
            if (_scanAccount.VerifiedWebSteamId64 != 0 && _scanAccount.VerifiedWebSteamId64 != id)
                throw new InvalidOperationException("Webkonto und gespeicherte Kontozuordnung stimmen nicht überein. Bitte das richtige Steam-Konto öffnen.");
            var scanner = new WorkshopPortfolio();
            var progress = new Progress<PortfolioProgress>(p => StatusText.Text = $"{p.Phase}: {p.Pages} Seiten · {p.Found} eigene Einträge gelesen …");
            ScanResult = await scanner.ScanAsync(id, ReadSteamPageAsync, _scanService.EnrichWorkshopItemsAsync, progress, _scanCancel.Token);
            _scanAccount.VerifiedWebSteamId64 = id; _scanAccount.WebIdentityCheckedAt = DateTimeOffset.UtcNow;
            StatusText.Text = ScanResult.Report.Summary;
            if(closeOnSuccess)DialogResult = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or OperationCanceledException or System.Runtime.InteropServices.COMException) {
            LastError = ex.Message; StatusText.Text = LastError;
        }
        finally { _scanCancel?.Dispose(); _scanCancel = null; ScanButton.IsEnabled = true; CancelScanButton.Visibility = Visibility.Collapsed; }
        return ScanResult;
    }
    private void CancelScan_Click(object sender, RoutedEventArgs e) => _scanCancel?.Cancel();

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(_root, "webview"));
            await Browser.EnsureCoreWebView2Async();
            if (_closed) return;
            var core = Browser.CoreWebView2;
            ApplyBrowserTheme();
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, args) => args.Cancel = true;
            core.NavigationStarting += (_, args) => {
                if (args.Uri == "about:blank") return;
                if (!SteamService.IsSteamBrowserUrl(args.Uri)) {
                    args.Cancel = true;
                    StatusText.Text = "Externe Navigation blockiert. Anmeldedaten nur auf der originalen Steam-Seite eingeben.";
                }
            };
            core.NewWindowRequested += (_, args) => {
                args.Handled = true;
                if (SteamService.IsSteamBrowserUrl(args.Uri)) core.Navigate(args.Uri);
            };
            core.NavigationCompleted += async (_, args) => {
                if (_closed) return;
                Address.Text = Browser.Source?.AbsoluteUri ?? "";
                _navigationFailed = !args.IsSuccess;
                if (!args.IsSuccess) {
                    LastError = "Steam-Seite konnte nicht geladen werden: " + args.WebErrorStatus;
                    StatusText.Text = LastError + ". Neu laden oder Standardbrowser verwenden.";
                    return;
                }
                LastError = "";
                await CheckWebSessionAsync();
            };
            core.ProcessFailed += (_, args) => {
                if (!_closed) ShowBrowserError("Steam-Webfenster wurde beendet (" + args.ProcessFailedKind + "). Fenster schließen und erneut öffnen.");
            };
            if (_clear) {
                await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);
                if (!_closed) DialogResult = true;
                return;
            }
            if (!SteamService.IsSteamBrowserUrl(_url)) throw new InvalidDataException("Nur originale Steam-HTTPS-Seiten sind im Steam-Browser erlaubt.");
            Address.Text = _url;
            Browser.Visibility = Visibility.Visible;
            StartupPanel.Visibility = Visibility.Collapsed;
            core.Navigate(_url);
            _sessionTimer.Start();
        }
        catch (WebView2RuntimeNotFoundException) {
            if (!_closed) ShowBrowserError("Microsoft Edge WebView2 Runtime fehlt. Über 'WebView2 installieren' die originale Microsoft-Seite öffnen. Alternativ Steam im Standardbrowser öffnen.");
        }
        catch (Exception ex) { if (!_closed) ShowBrowserError(ex.Message); }
    }

    private void ShowBrowserError(string message)
    {
        LastError = message;
        _navigationFailed = true;
        StatusText.Text = message;
        StartupText.Text = message;
        StartupProgress.Visibility = Visibility.Collapsed;
        StartupPanel.Visibility = Visibility.Visible;
        Browser.Visibility = Visibility.Collapsed;
    }

    private async Task CheckWebSessionAsync()
    {
        if (_closed || _checking || _navigationFailed || Browser.CoreWebView2 is null ||
            !Uri.TryCreate(Browser.Source?.AbsoluteUri, UriKind.Absolute, out var uri) ||
            uri.Host != "steamcommunity.com") return;
        _checking = true;
        try
        {
            // UI status only: read the visible account menu, never password fields, cookies or login tokens.
            var json = await Browser.CoreWebView2.ExecuteScriptAsync("""
                (() => {
                    const menu = document.getElementById('account_pulldown');
                    return { signedIn: !!menu, name: menu ? menu.textContent.trim().slice(0, 96) : '' };
                })()
                """);
            if (_closed) return;
            using var parsed = JsonDocument.Parse(json);
            var data = parsed.RootElement;
            if (data.ValueKind != JsonValueKind.Object) return;
            IsSteamWebSignedIn = data.TryGetProperty("signedIn", out var value) && value.ValueKind == JsonValueKind.True;
            WebAccountName = data.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "";
            StatusText.Text = IsSteamWebSignedIn
                ? "Steam-Webkonto angemeldet: " + WebAccountName + " · " + (_remember ? "Websitzung wird gespeichert" : "Private Sitzung") + " · Upload-Anmeldung bleibt separat."
                : "Steam-Webkonto nicht angemeldet. QR-Code oder Konto und Steam Guard direkt auf der Steam-Seite verwenden.";
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or System.Runtime.InteropServices.COMException) {
            // A navigation can replace the document while checking. Retry on the next tick.
        }
        finally { _checking = false; }
    }

    private void ThemeChanged(object? sender, EventArgs e) => ApplyBrowserTheme();
    private void ApplyBrowserTheme()
    {
        if (Application.Current.TryFindResource("WindowBrush") is System.Windows.Media.SolidColorBrush brush) {
            var c = brush.Color;
            Browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, c.R, c.G, c.B);
        }
        if (Browser.CoreWebView2 is { } core)
            core.Profile.PreferredColorScheme = ThemeService.IsLight ? CoreWebView2PreferredColorScheme.Light : CoreWebView2PreferredColorScheme.Dark;
    }
    private void Window_Closed(object? sender, EventArgs e)
    {
        _scanCancel?.Cancel();
        _closed = true;
        _sessionTimer.Stop();
        ThemeService.ThemeChanged -= ThemeChanged;
        Browser.Dispose();
    }
    private void Back_Click(object sender, RoutedEventArgs e) { if (Browser.CoreWebView2?.CanGoBack == true) Browser.CoreWebView2.GoBack(); }
    private void Reload_Click(object sender, RoutedEventArgs e) {
        if (Browser.CoreWebView2 is null) { StatusText.Text = "WebView2 installieren und Fenster erneut öffnen."; return; }
        _navigationFailed = false;
        Browser.Visibility = Visibility.Visible; StartupPanel.Visibility = Visibility.Collapsed;
        Browser.CoreWebView2.Reload();
    }
    private void Runtime_Click(object sender, RoutedEventArgs e) => DesktopDialogs.OpenUrl("https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download");
    private void External_Click(object sender, RoutedEventArgs e)
    {
        try {
            var url = Browser.Source?.AbsoluteUri ?? _url;
            if (SteamService.IsSteamBrowserUrl(url)) DesktopDialogs.OpenUrl(url);
            StatusText.Text = "Steam im Standardbrowser geöffnet. Dessen Anmeldung wird nicht in WebView2 oder SteamCMD übernommen.";
        } catch (Exception ex) { DesktopDialogs.Error(ex); }
    }
    private void Select_Click(object sender, RoutedEventArgs e)
    {
        try { SelectedWorkshopId = MainViewModel.ParseWorkshopId(Browser.Source?.AbsoluteUri ?? ""); DialogResult = true; }
        catch (Exception ex) { DesktopDialogs.Error(ex); }
    }
    public static Task<bool> ClearSessionAsync(string root, Guid? accountId = null) => Task.FromResult(
        new BrowserWindow(root, true, "https://steamcommunity.com/", "Websitzung löschen", false, true, accountId) { Owner = DesktopDialogs.Owner }.ShowDialog() == true);
}
