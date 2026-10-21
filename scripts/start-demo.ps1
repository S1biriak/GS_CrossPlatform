param(
    [ValidateRange(1024,65535)][int]$Port = 5080,
    [string]$PublishedDirectory
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'setup-demo.ps1')
$env:LIBRARY_SETTINGS_FILE = Join-Path $repoRoot '.local\demo-settings.json'
$env:ASPNETCORE_ENVIRONMENT = 'Production'
Write-Output "API: http://localhost:$Port/api/v1/books"
Write-Output "Swagger: http://localhost:$Port/swagger"
Write-Output 'Остановка: Ctrl+C.'
if ($PublishedDirectory) {
    $directory = (Resolve-Path -LiteralPath $PublishedDirectory).Path
    $executable = Join-Path $directory 'LibraryApi.exe'
    if (-not (Test-Path -LiteralPath $executable)) { throw "Не найден $executable" }
    Push-Location -LiteralPath $directory
    try { & $executable --urls "http://localhost:$Port" } finally { Pop-Location }
} else {
    & dotnet run --project (Join-Path $repoRoot 'src\LibraryApi') --no-launch-profile -- --urls "http://localhost:$Port"
}
if ($LASTEXITCODE -ne 0) { throw "Приложение завершилось с кодом $LASTEXITCODE" }
