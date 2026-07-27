$ErrorActionPreference = 'Continue'

$util = 'C:\Program Files\PawnIO\PawnIOUtil.exe'
$modules = 'C:\Users\DEEP\Documents\HWMonitor\src\MmioProbe\bin\Release\net10.0\modules'
$output = 'C:\Users\DEEP\Documents\HWMonitor\artifacts\pawnio-load-details.txt'

New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null

"SIGNED" | Set-Content -LiteralPath $output
& $util test (Join-Path $modules 'IntelMSR-signed.bin') *>> $output
"exit=$LASTEXITCODE" | Add-Content -LiteralPath $output

"UNSIGNED" | Add-Content -LiteralPath $output
& $util test (Join-Path $modules 'IntelMSR-unsigned.amx') *>> $output
"exit=$LASTEXITCODE" | Add-Content -LiteralPath $output
