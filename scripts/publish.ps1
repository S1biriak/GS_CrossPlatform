param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Версия должна иметь вид 1.0.0.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $repoRoot "artifacts\LibraryApi-$Version-win-x64"
if (Test-Path -LiteralPath $destination) { throw "Каталог уже существует: $destination. Выберите другую версию или явно удалите старый комплект." }
& dotnet publish (Join-Path $repoRoot 'src\LibraryApi\LibraryApi.csproj') -c Release -r win-x64 --self-contained true -p:Version=$Version "-p:RestorePackagesPath=$(Join-Path $repoRoot '.packages')" -o (Join-Path $destination 'app')
if ($LASTEXITCODE -ne 0) { throw 'Публикация не выполнена.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'postman') -Destination $destination -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $destination -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $destination
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\setup-demo.ps1') -Destination $destination
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\start-release.ps1') -Destination $destination
$archive = Join-Path $repoRoot "artifacts\LibraryApi-$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath $archive
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  LibraryApi-$Version-win-x64.zip" | Set-Content -LiteralPath "$archive.sha256" -Encoding ASCII
Write-Output "Сборка: $destination"
Write-Output "Архив: $archive"
