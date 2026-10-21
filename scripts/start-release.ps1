param([ValidateRange(1024,65535)][int]$Port = 5080)
$ErrorActionPreference = 'Stop'
$settings = Join-Path $PSScriptRoot '.local\demo-settings.json'
& (Join-Path $PSScriptRoot 'setup-demo.ps1') -SettingsPath $settings
$env:LIBRARY_SETTINGS_FILE = $settings
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$appDirectory = Join-Path $PSScriptRoot 'app'
Write-Output "Swagger: http://localhost:$Port/swagger"
Write-Output 'Остановка: Ctrl+C.'
Push-Location -LiteralPath $appDirectory
try { & (Join-Path $appDirectory 'LibraryApi.exe') --urls "http://localhost:$Port" }
finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw "Приложение завершилось с кодом $LASTEXITCODE" }
