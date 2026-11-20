$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'BloomCobrosAgent.csproj'
$output = Join-Path $PSScriptRoot 'dist\win-x64'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $output
Write-Host "Aplicación publicada en: $output" -ForegroundColor Green
