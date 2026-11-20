param(
    [Parameter(Mandatory=$true)][string]$ServerWebConfig,
    [string]$PrivateEnvFile = (Join-Path $PSScriptRoot '../.env.local')
)
$ErrorActionPreference='Stop'
# Run locally against a downloaded copy of the CURRENT server web.config.
# Do not replace it with the generic package file: it may contain hosting settings.
$source=(Resolve-Path -LiteralPath $ServerWebConfig).Path
$secretFile=(Resolve-Path -LiteralPath $PrivateEnvFile).Path
$storageKey=$null
foreach($line in [IO.File]::ReadLines($secretFile)) {
    if($line -match '^\s*STORAGE_KEY\s*=(.*)$') { $storageKey=$Matches[1].Trim().Trim('"', "'") }
}
if([string]::IsNullOrWhiteSpace($storageKey)){throw 'No se encontró STORAGE_KEY en la configuración privada local.'}
$document=[Xml.XmlDocument]::new()
$document.PreserveWhitespace=$true
$document.Load($source)
$nodes=$document.SelectNodes('//system.webServer/aspNetCore')
if($nodes.Count -ne 1){throw 'Se necesita el web.config de la aplicación ASP.NET Core activa; no se modificó ningún archivo.'}
$asp=$nodes[0]
if($asp.HasAttribute('configSource')){throw 'aspNetCore usa un archivo externo; revisar ese archivo antes de configurar.'}
$variables=$asp.SelectSingleNode('environmentVariables')
if($null -eq $variables){$variables=$document.CreateElement('environmentVariables');[void]$asp.AppendChild($variables)}
if($variables.HasAttribute('configSource')){throw 'Las variables usan un archivo externo; revisar ese archivo antes de configurar.'}
foreach($setting in @(@('STORAGE_KEY',$storageKey),@('STORAGE_ENABLED','true'))){
    $existing=$variables.SelectNodes("environmentVariable[@name='$($setting[0])']")
    if($existing.Count -gt 1){throw 'Hay variables de almacenamiento duplicadas; revisar antes de continuar.'}
    $entry=$existing | Select-Object -First 1
    if($null -eq $entry){$entry=$document.CreateElement('environmentVariable');$entry.SetAttribute('name',$setting[0]);[void]$variables.AppendChild($entry)}
    $entry.SetAttribute('value',$setting[1])
}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output=Join-Path $root ('outputs/storage-private-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($output)
Copy-Item -LiteralPath $source -Destination (Join-Path $output 'web.config.original')
$target=Join-Path $output 'web.config'
$document.Save($target)
$verify=[Xml.XmlDocument]::new();$verify.Load($target)
if($verify.SelectSingleNode("//aspNetCore/environmentVariables/environmentVariable[@name='STORAGE_KEY']").GetAttribute('value') -cne $storageKey){throw 'No se pudo verificar la configuración generada.'}
Write-Output "Configuración privada preparada: $target"
Write-Output 'Conserva los ajustes del archivo recibido. Contiene la clave: no compartir ni incluir en un ZIP público.'
Write-Output 'Sustituir únicamente el web.config de la aplicación activa por este archivo y reiniciar el sitio. No subir web.config.original.'
