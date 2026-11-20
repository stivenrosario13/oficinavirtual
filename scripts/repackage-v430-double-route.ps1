$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Join-Path $root 'outputs/ACTUALIZACION_V430_FTP_SEGURO_20261014171010.zip'
$readme = Join-Path $root 'outputs/INSTRUCCIONES_V430_DOBLE_RUTA.txt'
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$destination = Join-Path $root "outputs/ACTUALIZACION_COMPLETA_V430_DOBLE_RUTA_$stamp.zip"

Add-Type -AssemblyName System.IO.Compression.FileSystem
$inputArchive = [IO.Compression.ZipFile]::OpenRead($source)
$outputArchive = [IO.Compression.ZipFile]::Open($destination, [IO.Compression.ZipArchiveMode]::Create)
try {
    $files = @($inputArchive.Entries | Where-Object { $_.Name -and $_.FullName -ne 'INSTRUCCIONES_V430_FTP_SEGURO.txt' })
    $directories = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($prefix in @('', 'wwwroot/')) {
        foreach ($entry in $files) {
            $name = $prefix + $entry.FullName.Replace('\', '/')
            if ($name -match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/') {
                throw "Archivo privado: $name"
            }
            $parent = $name
            while ($parent.Contains('/')) {
                $parent = $parent.Substring(0, $parent.LastIndexOf('/'))
                [void]$directories.Add($parent + '/')
            }
            $created = $outputArchive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
            $from = $entry.Open()
            $to = $created.Open()
            try { $from.CopyTo($to) } finally { $from.Dispose(); $to.Dispose() }
        }
        $readmeEntry = $outputArchive.CreateEntry($prefix + 'INSTRUCCIONES_V430_DOBLE_RUTA.txt', [IO.Compression.CompressionLevel]::Optimal)
        $from = [IO.File]::OpenRead($readme)
        $to = $readmeEntry.Open()
        try { $from.CopyTo($to) } finally { $from.Dispose(); $to.Dispose() }
    }
    foreach ($directory in ($directories | Sort-Object)) { [void]$outputArchive.CreateEntry($directory) }
} finally { $outputArchive.Dispose(); $inputArchive.Dispose() }

function Get-EntryHash($entry) {
    $stream = $entry.Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
}

$inputArchive = [IO.Compression.ZipFile]::OpenRead($source)
$outputArchive = [IO.Compression.ZipFile]::OpenRead($destination)
try {
    $names = @($outputArchive.Entries.FullName)
    if (($names | Select-Object -Unique).Count -ne $names.Count) { throw 'Entradas duplicadas' }
    foreach ($entry in ($inputArchive.Entries | Where-Object { $_.Name -and $_.FullName -ne 'INSTRUCCIONES_V430_FTP_SEGURO.txt' })) {
        $expected = Get-EntryHash $entry
        foreach ($prefix in @('', 'wwwroot/')) {
            $copy = $outputArchive.GetEntry($prefix + $entry.FullName.Replace('\', '/'))
            if (-not $copy -or (Get-EntryHash $copy) -ne $expected) { throw "Copia incorrecta: $prefix$($entry.FullName)" }
        }
    }
    foreach ($prefix in @('', 'wwwroot/')) {
        foreach ($name in @('web.config', 'BouncyCastle.Crypto.dll', 'RegistroAgencias.SqlServer.V430.dll', 'RegistroAgencias.SqlServer.V430.exe', 'wwwroot/index.html', 'wwwroot/version-web.json', 'wwwroot/release-guard.js', 'INSTRUCCIONES_V430_DOBLE_RUTA.txt')) {
            if (-not $outputArchive.GetEntry($prefix + $name)) { throw "Falta $prefix$name" }
        }
        $reader = [IO.StreamReader]::new($outputArchive.GetEntry($prefix + 'wwwroot/version-web.json').Open())
        try { $version = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($version -notmatch '"release": "V430"') { throw "Versión incorrecta: $prefix" }
    }
    [pscustomobject]@{
        Zip = $destination
        Bytes = (Get-Item -LiteralPath $destination).Length
        Entries = $names.Count
        CopiesVerified = 2
        SHA256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    } | ConvertTo-Json
} finally { $outputArchive.Dispose(); $inputArchive.Dispose() }
