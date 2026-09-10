$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "artifacts\monster-v51"
$zip = "C:\Users\pc\Downloads\SUBIR_WWWROOT_CENTRO_TICKETS_AGENCIAS_V51.zip"
$excludedLocales = "\\(cs|de|es|fr|it|ja|ko|pl|pt-BR|ru|tr|zh-Hans|zh-Hant)\\"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $source -Recurse -File |
        Where-Object { $_.Extension -ne ".pdb" -and $_.FullName -notmatch $excludedLocales } |
        ForEach-Object {
            $entry = $_.FullName.Substring($source.Length).TrimStart([char[]]@("\", "/")).Replace("\", "/")
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
}
finally { $archive.Dispose() }
$check = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $names = $check.Entries.FullName
    [pscustomobject]@{
        Zip = $zip
        SizeMB = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 2)
        Files = $names.Count
        BackendDll = $names -contains "RegistroAgencias.SqlServer.dll"
        WebConfig = $names -contains "web.config"
        Index = $names -contains "wwwroot/index.html"
        ResourceDlls = @($names | Where-Object { $_ -like "*.resources.dll" }).Count
        Version = "2026.08.06.51"
    } | ConvertTo-Json
}
finally { $check.Dispose() }
