$ErrorActionPreference="Stop"
$root=Split-Path -Parent $PSScriptRoot
$source=Join-Path $root "artifacts\monster-v98-direct-chat-clean-20260811"
$zip=Join-Path $root "artifacts\HOTFIX_ABRIR_CHAT_SIN_ENVIAR_MENSAJE_V98.zip"
$files=@("web.config","RegistroAgencias.SqlServer.V98.dll","RegistroAgencias.SqlServer.V98.deps.json","RegistroAgencias.SqlServer.V98.runtimeconfig.json","RegistroAgencias.SqlServer.V98.staticwebassets.endpoints.json")
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($name in $files){if(-not(Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)){throw "Falta $name"}}
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip -Force}
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($name in $files){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $source $name),$name,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$archive.Dispose()}
Write-Output $zip
