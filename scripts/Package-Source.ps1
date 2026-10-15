[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifacts=Join-Path $root 'artifacts'
[IO.Directory]::CreateDirectory($artifacts) | Out-Null
[xml]$props=Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$version=[string]$props.Project.PropertyGroup.Version
$zip=Join-Path $artifacts ('WorkshopStudio-'+$version+'-Quellprojekt.zip')
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip -Force}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try {
    $files=Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object {
        $relative=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/')
        $relative -notmatch '(^|/)(?:\.git|bin|obj|artifacts|work|\.vs|\.idea|node_modules)/' -and
        $relative -notmatch '\.(?:tmp|bak)$'
    }
    foreach($file in $files){
        $relative=[IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,'WorkshopStudio/'+$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {$archive.Dispose()}
$hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash
Set-Content -LiteralPath ($zip+'.sha256') -Value ($hash+'  '+(Split-Path -Leaf $zip)) -Encoding ASCII
Write-Host ('Quellpaket: '+$zip)
Write-Host ('Dateien: '+$files.Count)
