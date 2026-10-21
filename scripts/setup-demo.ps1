param([string]$SettingsPath)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $SettingsPath) { $SettingsPath = Join-Path $repoRoot '.local\demo-settings.json' }
$SettingsPath = [IO.Path]::GetFullPath($SettingsPath)
if (Test-Path -LiteralPath $SettingsPath) {
    Write-Output "Настройки уже существуют: $SettingsPath"
    return
}
$bytes = New-Object byte[] 64
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$settings = @{
    Jwt = @{ Key = [Convert]::ToBase64String($bytes) }
    Demo = @{ Seed = $true; Swagger = $true; AdminPassword = 'Admin-Library-2026!'; UserPassword = 'User-Library-2026!' }
}
New-Item -ItemType Directory -Path (Split-Path -Parent $SettingsPath) -Force | Out-Null
$settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $SettingsPath -Encoding UTF8
Write-Output "Локальные настройки созданы: $SettingsPath"
Write-Output 'Демонстрационные аккаунты: admin / Admin-Library-2026!; user / User-Library-2026!'
Write-Output 'Ключ JWT сгенерирован случайно. Файл настроек не включается в Git и релиз.'
