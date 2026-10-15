[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ($env:OS -ne 'Windows_NT') { throw 'Dieses Desktop-Buildskript auf Windows ausfuehren.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 10 SDK fehlt. In Visual Studio 2026 .NET-Desktopentwicklung installieren.'
}
function Invoke-Dotnet {
    param([Parameter(Mandatory=$true)][string[]]$DotnetArgs)
    & dotnet @DotnetArgs
    if ($LASTEXITCODE -ne 0) { throw ('dotnet fehlgeschlagen: ' + ($DotnetArgs -join ' ')) }
}
Push-Location $root
try {
    $sdk = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $sdk.StartsWith('10.')) { throw ('.NET SDK 10.x erforderlich. Gefunden: ' + $sdk) }
    Write-Host ('SDK: ' + $sdk)
    Invoke-Dotnet -DotnetArgs @('restore', 'WorkshopStudio.sln')
    Invoke-Dotnet -DotnetArgs @('build', 'WorkshopStudio.sln', '--no-restore', '-c', $Configuration)
    Invoke-Dotnet -DotnetArgs @('run', '--project', 'tests/WorkshopStudio.SelfTest/WorkshopStudio.SelfTest.csproj', '-c', $Configuration, '--no-build')
    $out = Join-Path $root ('artifacts/WorkshopStudio-' + $Runtime)
    $resolvedRoot = [IO.Path]::GetFullPath($root).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $resolvedOut = [IO.Path]::GetFullPath($out)
    if (-not $resolvedOut.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Build-Ausgabe liegt nicht im Projektordner.'
    }
    if ((Test-Path -LiteralPath $out) -and ((Get-Item -LiteralPath $out).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Verknüpfte Build-Ausgabe wird nicht gelöscht.'
    }
    # Only clean the fixed build artifact directory, never source or user application data.
    if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }
    $selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
    Invoke-Dotnet -DotnetArgs @('publish', 'src/WorkshopStudio.Desktop/WorkshopStudio.Desktop.csproj', '-c', $Configuration, '-r', $Runtime, '--self-contained', $selfContained, '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-o', $out)
    Copy-Item -LiteralPath (Join-Path $root 'README_DE.md') -Destination $out
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $out
    Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $out
    $exe = Join-Path $out 'WorkshopStudio.exe'
    if (-not (Test-Path $exe)) { throw 'Publish erfolgreich, aber erwartete WorkshopStudio.exe fehlt.' }
    [xml]$buildProps = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
    $version = [string]$buildProps.Project.PropertyGroup.Version
    @{ version = $version; repository = 'ZUMBYTE-AppSolution/WorkshopStudio' } |
        ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $out 'release-info.json') -Encoding utf8
    $zip = Join-Path $root ('artifacts/WorkshopStudio-' + $version + '-' + $Runtime + '.zip')
    Compress-Archive -Path $out -DestinationPath $zip -Force
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash
    Set-Content -LiteralPath ($zip + '.sha256') -Value ($hash + '  ' + (Split-Path -Leaf $zip)) -Encoding ASCII
    Write-Host ''
    Write-Host 'Build und Selbsttests erfolgreich.' -ForegroundColor Green
    Write-Host ('Programm: ' + $exe)
    Write-Host ('Paket:    ' + $zip)
    Write-Host 'Die Anwendung wurde NICHT automatisch gestartet. Steam-Uploads separat mit privater Test-Mod pruefen.'
} finally { Pop-Location }
