$ErrorActionPreference='Stop'
$taskCredentials=@{}
Get-Content -LiteralPath (Join-Path $PSScriptRoot '../.env.local') | ForEach-Object {if($_ -match '^(ADMIN_EMAIL|ADMIN_PASSWORD)=(.*)$'){$taskCredentials[$Matches[1]]=$Matches[2].Trim().Trim('"').Trim("'")}}
if(!$taskCredentials.ADMIN_EMAIL -or !$taskCredentials.ADMIN_PASSWORD){throw 'No local diagnostic credentials available'}
$taskBody=@{email=$taskCredentials.ADMIN_EMAIL;password=$taskCredentials.ADMIN_PASSWORD}|ConvertTo-Json
try{$taskLogin=Invoke-WebRequest -Uri 'https://realagencias.runasp.net/api/admin/login' -Method Post -ContentType 'application/json' -Body $taskBody -SessionVariable taskSession -SkipHttpErrorCheck -TimeoutSec 30}catch{Write-Output 'Login connection failed';exit 1}
Write-Output "Diagnostic login HTTP $($taskLogin.StatusCode)"
if($taskLogin.StatusCode -ne 200){exit 1}
foreach($taskPath in @('/api/warehouse/fulfillment/inventory','/api/maintenance/movements','/api/maintenance/procurement')){
 $taskResponse=Invoke-WebRequest -Uri ('https://realagencias.runasp.net'+$taskPath) -WebSession $taskSession -SkipHttpErrorCheck -TimeoutSec 45
 Write-Output "$taskPath HTTP $($taskResponse.StatusCode)"
 if($taskResponse.StatusCode -ne 200){try{$taskError=$taskResponse.Content|ConvertFrom-Json;Write-Output ($taskError|Select-Object error,code|ConvertTo-Json -Compress)}catch{Write-Output 'Non-JSON error response'}}
}
