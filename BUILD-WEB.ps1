$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot 'Pyramid.Web\Client')
try {
    npm ci
    if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency installation failed.' }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
} finally { Pop-Location }
Push-Location $PSScriptRoot
try {
    dotnet run --project Pyramid.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Campaign checks failed.' }
    dotnet publish Pyramid.Web -c Release -o artifacts\release
    if ($LASTEXITCODE -ne 0) { throw 'IIS publish failed.' }
    Write-Host 'Server files are in artifacts\release. Read docs/Deployment.md before deploying.'
} finally { Pop-Location }
