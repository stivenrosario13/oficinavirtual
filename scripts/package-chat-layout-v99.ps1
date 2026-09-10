$ErrorActionPreference="Stop"
$root=Split-Path -Parent $PSScriptRoot
$source=Join-Path $root "artifacts\monster-v99-chat-private-layout-clean-20260811"
$zip=Join-Path $root "artifacts\CHAT_COMPLETO_PRIVADO_PROFESIONAL_V99.zip"
$files=@("web.config","RegistroAgencias.SqlServer.V99.dll","RegistroAgencias.SqlServer.V99.deps.json","RegistroAgencias.SqlServer.V99.runtimeconfig.json","RegistroAgencias.SqlServer.V99.staticwebassets.endpoints.json")
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($name in $files){if(-not(Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)){throw "Falta $name"}}
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip -Force}
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
 foreach($name in $files){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $source $name),$name,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}
 $webRoot=Join-Path $source "wwwroot"
 Get-ChildItem -LiteralPath $webRoot -Recurse -File|ForEach-Object{$relative=$_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}
}finally{$archive.Dispose()}
Write-Output $zip
