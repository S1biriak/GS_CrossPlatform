param(
    [string]$Owner = 'S1biriak',
    [string]$Repository = 'GS_CrossPlatform',
    [string]$Version = '1.0.0'
)
$ErrorActionPreference = 'Stop'
if ($Owner -notmatch '^[A-Za-z0-9-]+$' -or $Repository -notmatch '^[A-Za-z0-9_.-]+$' -or $Version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Недопустимое имя аккаунта, репозитория или версии.'
}
$repoRoot = Split-Path -Parent $PSScriptRoot
$archive = Join-Path $repoRoot "artifacts\LibraryApi-$Version-win-x64.zip"
$notes = Join-Path $repoRoot 'docs\RELEASE_NOTES.md'
foreach ($path in @($archive, "$archive.sha256", $notes)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Не готов файл публикации: $path" }
}
Push-Location -LiteralPath $repoRoot
try {
    if ((git status --porcelain)) { throw 'Перед публикацией рабочее дерево должно быть закоммичено.' }
    $commit = (git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Не найден коммит.' }
    # Use the existing Git sign-in. Credentials are captured only in memory and
    # are never printed, stored in a file, or placed in a remote URL.
    $env:GCM_INTERACTIVE = 'never'
    $credentialLines = ((@('protocol=https', 'host=github.com', "username=$Owner", '') -join "`n") | git credential fill)
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось получить существующий вход GitHub из Git Credential Manager.' }
    $tokenLine = $credentialLines | Where-Object { $_.StartsWith('password=') } | Select-Object -First 1
    if (-not $tokenLine) { throw 'В Git Credential Manager отсутствует действующий вход GitHub.' }
    $headers = @{ Authorization = 'Bearer ' + $tokenLine.Substring(9); Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28'; 'User-Agent' = 'LibraryApi-Lab-Publisher' }
    $account = Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers $headers
    if ($account.login -ine $Owner) { throw 'Аккаунт Git не совпадает с ожидаемым владельцем.' }
    $api = "https://api.github.com/repos/$Owner/$Repository"
    $remote = "https://github.com/$Owner/$Repository.git"
    try { $repo = Invoke-RestMethod -Uri $api -Headers $headers }
    catch {
        if ([int]$_.Exception.Response.StatusCode -ne 404) { throw }
        $repo = Invoke-RestMethod -Uri 'https://api.github.com/user/repos' -Headers $headers -Method Post -ContentType 'application/json; charset=utf-8' -Body (@{
            name = $Repository; description = 'Library Web API lab: ASP.NET Core 9, SQLite, LINQ, JWT, Postman and release build'; private = $false; auto_init = $false
        } | ConvertTo-Json)
    }
    if ($repo.owner.login -ine $Owner -or $repo.name -ine $Repository) { throw 'Ответ GitHub не соответствует целевому репозиторию.' }
    if ((git remote) -contains 'origin') {
        $origin = (git remote get-url origin).Trim()
        if ($origin -ne $remote) { throw 'У origin другой адрес. Автоматическая замена запрещена.' }
    } else { git remote add origin $remote }
    git push -u origin main
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось отправить main; принудительная отправка не выполняется.' }
    $tag = "v$Version"
    git tag $tag $commit
    if ($LASTEXITCODE -ne 0) { throw "Не удалось создать тег $tag; существующие теги не изменяются." }
    git push origin $tag
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось отправить тег.' }
    $release = Invoke-RestMethod -Uri "$api/releases" -Headers $headers -Method Post -ContentType 'application/json; charset=utf-8' -Body (@{
        tag_name = $tag; target_commitish = $commit; name = "Library API $tag";
        body = [IO.File]::ReadAllText($notes); draft = $false; prerelease = $false
    } | ConvertTo-Json)
    $uploadBase = $release.upload_url -replace '\{.*$', ''
    $files = @($archive, "$archive.sha256",
        (Join-Path $repoRoot 'postman\LibraryApi.postman_collection.json'),
        (Join-Path $repoRoot 'postman\LibraryApi.local.postman_environment.json'))
    foreach ($file in $files) {
        $name = [Uri]::EscapeDataString([IO.Path]::GetFileName($file))
        Invoke-RestMethod -Uri "${uploadBase}?name=$name" -Headers $headers -Method Post -ContentType 'application/octet-stream' -InFile $file | Out-Null
    }
    Write-Output "Репозиторий: $($repo.html_url)"
    Write-Output "Релиз: $($release.html_url)"
} finally {
    $credentialLines = $null
    $tokenLine = $null
    $headers = $null
    Pop-Location
}
