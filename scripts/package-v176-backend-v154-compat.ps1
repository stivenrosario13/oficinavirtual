$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "V176_BACKEND_V154_COMPAT_TEMP"
$offlineZip = Join-Path $artifacts "V176_00_DETENER_APLICACION.zip"
$backendZip = Join-Path $artifacts "V176_01_BACKEND_COMPATIBLE_V154.zip"
$backendMonstaZip = Join-Path $artifacts "V176_01B_BACKEND_V154_COMPATIBLE_MONSTA.zip"
$backendSwitchZip = Join-Path $artifacts "V176_02_CAMBIAR_A_DLL_NUEVA_MINIMO.zip"
$offlineFile = Join-Path $root "deploy\app_offline.htm"
$instructions = Join-Path $root "deploy\INSTRUCCIONES_BACKEND_V176_COMPAT_V154.txt"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($zipPath in @($offlineZip,$backendZip,$backendMonstaZip,$backendSwitchZip)) {
  if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
}

$archive = [System.IO.Compression.ZipFile]::Open($offlineZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$offlineFile,"app_offline.htm",[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}
finally { $archive.Dispose() }

$archive = [System.IO.Compression.ZipFile]::Open($backendZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  Get-ChildItem -LiteralPath $publish -Recurse -File |
    Where-Object { $_.FullName -notlike "*\wwwroot\*" -and $_.Name -ne "web.config" } |
    ForEach-Object {
      $relative = $_.FullName.Substring($publish.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$relative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$instructions,"LEEME_INSTALACION_V176.txt",[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $publish "web.config"),"web.config",[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}
finally { $archive.Dispose() }

$archive = [System.IO.Compression.ZipFile]::Open($backendMonstaZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  Get-ChildItem -LiteralPath $publish -Recurse -File |
    Where-Object { $_.FullName -notlike "*\wwwroot\*" -and $_.Name -notin @("web.config","Microsoft.SqlServer.Server.dll") } |
    ForEach-Object {
      $relative = $_.FullName.Substring($publish.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$relative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$instructions,"LEEME_INSTALACION_V176.txt",[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $publish "web.config"),"web.config",[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}
finally { $archive.Dispose() }

$v176Publish = Join-Path $artifacts "V176_PUBLISH_TEMP"
$archive = [System.IO.Compression.ZipFile]::Open($backendSwitchZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($name in @(
    "RegistroAgencias.SqlServer.V176.dll",
    "RegistroAgencias.SqlServer.V176.deps.json",
    "RegistroAgencias.SqlServer.V176.runtimeconfig.json",
    "RegistroAgencias.SqlServer.V176.exe"
  )) {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $v176Publish $name),$name,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $v176Publish "web.config"),"web.config",[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}
finally { $archive.Dispose() }

Write-Host "Paquetes V176 compatibles con el backend V154 creados." -ForegroundColor Green
