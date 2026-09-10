$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "V168_PUBLISH_TEMP"
$web = Join-Path $root "api\wwwroot"
$migration = Join-Path $root "database-mssql\036_chat_message_deletion_transition_progress.sql"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-ZipFromDirectory([string]$source, [string]$destination, [string]$prefix = "") {
  if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
  $archive = [System.IO.Compression.ZipFile]::Open($destination, [System.IO.Compression.ZipArchiveMode]::Create)
  try {
    Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
      $relative = $_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
      $entry = if ($prefix) { "$prefix/$relative" } else { $relative }
      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
  }
  finally { $archive.Dispose() }
}

function New-OrganizedUpdate([string]$destination) {
  if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
  $archive = [System.IO.Compression.ZipFile]::Open($destination, [System.IO.Compression.ZipArchiveMode]::Create)
  try {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $migration, "01_EJECUTAR_EN_SQL_SERVER/036_CHAT_MENSAJES_ETAPAS.sql", [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    Get-ChildItem -LiteralPath $publish -Recurse -File | ForEach-Object {
      $relative = $_.FullName.Substring($publish.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, "02_SUBIR_DENTRO_DE_WWWROOT/$relative", [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $entry = $archive.CreateEntry("LEEME_PRIMERO.txt")
    $writer = New-Object System.IO.StreamWriter($entry.Open(), [System.Text.Encoding]::UTF8)
    try {
      $writer.Write("ACTUALIZACION V168`r`n`r`n1. Ejecuta el SQL de la carpeta 01 una sola vez.`r`n2. Deten la aplicacion con app_offline.htm si tu proveedor lo requiere.`r`n3. Sube TODO el contenido de 02 dentro de la carpeta /wwwroot del hosting, conservando la subcarpeta wwwroot.`r`n4. Confirma que RegistroAgencias.SqlServer.V155.dll y System.Configuration.ConfigurationManager.dll quedaron en /wwwroot.`r`n5. Retira app_offline.htm y limpia la cache del navegador.`r`n")
    }
    finally { $writer.Dispose() }
  }
  finally { $archive.Dispose() }
}

New-ZipFromDirectory $web (Join-Path $artifacts "V168_SOLO_FRONTEND_DENTRO_WWWROOT_PUBLICO_SIN_DLL.zip")
New-ZipFromDirectory $web (Join-Path $artifacts "V168_SOLO_FRONTEND_DESDE_RAIZ_MONSTER_SIN_DLL.zip") "wwwroot"
New-ZipFromDirectory $publish (Join-Path $artifacts "V168_BACKEND_Y_FRONTEND_EXTRAER_DENTRO_WWWROOT_CON_DLL.zip")
New-OrganizedUpdate (Join-Path $artifacts "V168_ACTUALIZACION_COMPLETA_CHAT_Y_ETAPAS.zip")

Write-Host "Paquetes V168 creados y verificados." -ForegroundColor Green
