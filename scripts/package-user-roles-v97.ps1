$ErrorActionPreference="Stop"
$root=Split-Path -Parent $PSScriptRoot
$source=Join-Path $root "artifacts\monster-v97-user-roles-clean-20260811"
$insideZip=Join-Path $root "artifacts\01_ACTUALIZACION_USUARIOS_ROLES_V97_DENTRO_WWWROOT.zip"
$outsideZip=Join-Path $root "artifacts\02_CREAR_ENCARGADO_SERVICIOS_GENERALES_FUERA_WWWROOT.zip"
$rootFiles=@("web.config","RegistroAgencias.SqlServer.V97.dll","RegistroAgencias.SqlServer.V97.deps.json","RegistroAgencias.SqlServer.V97.runtimeconfig.json","RegistroAgencias.SqlServer.V97.staticwebassets.endpoints.json")
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($path in @($insideZip,$outsideZip)){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}}
$inside=[IO.Compression.ZipFile]::Open($insideZip,[IO.Compression.ZipArchiveMode]::Create)
try{
  foreach($name in $rootFiles){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($inside,(Join-Path $source $name),$name,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}
  $webRoot=Join-Path $source "wwwroot"
  Get-ChildItem -LiteralPath $webRoot -Recurse -File|ForEach-Object{$relative=$_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($inside,$_.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}
}finally{$inside.Dispose()}
$outside=[IO.Compression.ZipFile]::Open($outsideZip,[IO.Compression.ZipArchiveMode]::Create)
try{[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($outside,(Join-Path $root "database-mssql\026_servicios_generales_department_manager.sql"),"026_CREAR_ENCARGADO_SERVICIOS_GENERALES.sql",[IO.Compression.CompressionLevel]::Optimal)|Out-Null}finally{$outside.Dispose()}
Write-Output $insideZip
Write-Output $outsideZip
