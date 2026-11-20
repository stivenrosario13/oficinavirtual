param([Parameter(Mandatory)][string]$Source)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sourcePath=(Resolve-Path -LiteralPath $Source).Path
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$target=Join-Path (Split-Path $sourcePath) "ACTUALIZACION_V418_COMPLETA_FORMATO_NORMAL_$stamp.zip"
$inputZip=[IO.Compression.ZipFile]::OpenRead($sourcePath)
$outputZip=[IO.Compression.ZipFile]::Open($target,[IO.Compression.ZipArchiveMode]::Create)
try {
    $folders=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($entry in $inputZip.Entries){
        $name=$entry.FullName
        if($name.StartsWith('/') -or $name.Contains('\') -or $name.Split('/') -contains '..'){throw "Ruta no valida: $name"}
        while($name.Contains('/')){$name=$name.Substring(0,$name.LastIndexOf('/'));if($name){[void]$folders.Add($name+'/')}}
    }
    foreach($folder in ($folders | Sort-Object)){[void]$outputZip.CreateEntry($folder)}
    foreach($entry in ($inputZip.Entries | Where-Object {-not $_.FullName.EndsWith('/')} | Sort-Object FullName)){
        $copy=$outputZip.CreateEntry($entry.FullName,[IO.Compression.CompressionLevel]::Optimal)
        $copy.LastWriteTime=$entry.LastWriteTime
        $reader=$entry.Open();$writer=$copy.Open()
        try{$reader.CopyTo($writer)}finally{$reader.Dispose();$writer.Dispose()}
    }
}finally{$outputZip.Dispose();$inputZip.Dispose()}
$inputZip=[IO.Compression.ZipFile]::OpenRead($sourcePath)
$outputZip=[IO.Compression.ZipFile]::OpenRead($target)
try {
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($entry in $outputZip.Entries){
        $name=$entry.FullName.TrimEnd('/')
        if($name.Contains('/')){
            $parent=$name.Substring(0,$name.LastIndexOf('/'))+'/'
            if(-not $seen.Contains($parent)){throw "Carpeta no creada antes del archivo: $name"}
        }
        [void]$seen.Add($entry.FullName)
        if($entry.FullName.EndsWith('/')){continue}
        $original=$inputZip.GetEntry($entry.FullName)
        if(!$original){throw "Archivo inesperado: $name"}
        $a=$original.Open();$b=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try{
            $hashA=[BitConverter]::ToString($sha.ComputeHash($a))
            $hashB=[BitConverter]::ToString($sha.ComputeHash($b))
            if($hashA -ne $hashB){throw "Contenido diferente: $name"}
        }finally{$a.Dispose();$b.Dispose();$sha.Dispose()}
    }
    foreach($name in @('Microsoft.SqlServer.Server.dll','wwwroot/Microsoft.SqlServer.Server.dll','web.config','wwwroot/web.config','wwwroot/index.html','wwwroot/wwwroot/index.html')){
        if(!$outputZip.GetEntry($name)){throw "Falta $name"}
    }
    if($inputZip.Entries.Count -ne $outputZip.Entries.Count){throw 'Cantidad de entradas diferente'}
    Write-Output "VERIFICADO: $($outputZip.Entries.Count) entradas, carpetas primero, contenido SHA256 identico, doble ruta normal conservada."
}finally{$inputZip.Dispose();$outputZip.Dispose()}
Get-Item -LiteralPath $target | Select-Object FullName,Length | ConvertTo-Json
