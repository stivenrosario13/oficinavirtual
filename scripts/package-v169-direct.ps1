$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "V169_PUBLISH_TEMP"
$web = Join-Path $root "api\wwwroot"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-DirectZip([string]$source, [string]$destination) {
  if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
  $archive = [System.IO.Compression.ZipFile]::Open($destination, [System.IO.Compression.ZipArchiveMode]::Create)
  try {
    Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
      $entryName = $_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
  }
  finally { $archive.Dispose() }
}

New-DirectZip $publish (Join-Path $artifacts "V169_UNICO_EXTRAER_DIRECTAMENTE_EN_CARPETA_WWWROOT.zip")
New-DirectZip $web (Join-Path $artifacts "V169_SOLO_INTERFAZ_EXTRAER_DENTRO_DE_WWWROOT_PUBLICO.zip")

Write-Host "Paquetes directos V169 creados." -ForegroundColor Green
