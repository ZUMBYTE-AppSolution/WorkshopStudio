# Quellen und technische Grundlagen

Recherche: 16. September 2026. Die Dokumentation beschreibt die Grundlage, nicht den Nachweis eines hier bereits ausgeführten Windows-/Steam-Tests.

## Valve / Steam

- Workshop-Implementierung, SteamCMD-Testtool, VDF und rechtliche Freigabe: https://partner.steamgames.com/doc/features/workshop/implementation
- ISteamRemoteStorage mit GetPublishedFileDetails und GetCollectionDetails: https://partner.steamgames.com/doc/webapi/ISteamRemoteStorage
- SteamCMD-Anmeldung und gespeicherte Sitzung: https://partner.steamgames.com/doc/sdk/uploading
- SteamCMD-Bootstrap von Valve: https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip
- Workshop-Vereinbarung: https://steamcommunity.com/sharedfiles/workshoplegalagreement
- Originaler Collection-Editor, Beispiel Zomboid: https://steamcommunity.com/sharedfiles/editcollection/?appid=108600
- Öffentlicher Store-Endpunkt, ohne Stabilitätszusage: https://store.steampowered.com/api/appdetails?appids=108600

## Microsoft / .NET

- .NET-Installation auf Windows und Visual-Studio-Zuordnung: https://learn.microsoft.com/en-us/dotnet/core/install/windows
- .NET-10-Release-Metadaten: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
- WebView2-Paket (im Projekt fest auf 1.0.4191.47 gesetzt): https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4191.47
- WebView2 Evergreen Runtime: https://developer.microsoft.com/en-us/microsoft-edge/webview2/
- WebView2 Benutzerdaten: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder
- WebView2 Browserdaten löschen: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/clear-browsing-data
- Credential Manager CredWriteW: https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew

## Project Zomboid

- Modding-Dokumentation: https://projectzomboid.com/modding/
- Modding-Policy und Rechte an fremden Mods: https://projectzomboid.com/blog/modding-policy/

Die B41-/B42-Strukturvorlagen müssen im tatsächlich verwendeten Spielbuild überprüft werden. Keine fremden Mods, Assets oder Steam-Produktbilder sind im Quellarchiv gebündelt. Steam-Bilder werden erst durch die Anwendung vom Dienst abgerufen.

## Login-Fix 1.0.2 (abgerufen 16.09.2026)

- Microsoft: Creating a Pseudoconsole session: https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session
- Microsoft: CreatePseudoConsole, UTF-8 und Konsolenkanäle: https://learn.microsoft.com/en-us/windows/console/createpseudoconsole
- Microsoft: ClosePseudoConsole und Deadlock-Hinweise: https://learn.microsoft.com/en-us/windows/console/closepseudoconsole
- Valve: Uploading to Steam, Abschnitt Using SteamPipe In A CI/CD Environment (gespeicherte Login-Sitzung): https://partner.steamgames.com/doc/sdk/uploading
- Microsoft: Manage user data folders: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder
- Microsoft: WPF CoreWebView2CreationProperties, ProfileName: https://learn.microsoft.com/ja-jp/dotnet/api/microsoft.web.webview2.wpf.corewebview2creationproperties?view=webview2-dotnet-1.0.3351.48

Die Quellen dokumentieren Plattformfunktionen. Sie sind kein Nachweis, dass die beigefügte Anwendung bereits erfolgreich unter Windows oder gegen Steam getestet wurde.
