$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "artifacts\monster-v96-private-chat-clean-20260810"
$zip = Join-Path $root "artifacts\CHAT_DIRECTO_PRIVADO_ZIP_CORREGIDO_V96.zip"
$rootFiles = @(
    "web.config",
    "RegistroAgencias.SqlServer.V96.dll",
    "RegistroAgencias.SqlServer.V96.deps.json",
    "RegistroAgencias.SqlServer.V96.runtimeconfig.json",
    "RegistroAgencias.SqlServer.V96.staticwebassets.endpoints.json"
)
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($name in $rootFiles){if(-not(Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)){throw "Falta $name"}}
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip -Force}
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
  foreach($name in $rootFiles){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $source $name),$name,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}
  $webRoot=Join-Path $source "wwwroot"
  Get-ChildItem -LiteralPath $webRoot -Recurse -File | ForEach-Object {
    $relative=$_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null
  }
}finally{$archive.Dispose()}
Write-Output $zip
