$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot 'Pyramid.Web')
try {
    dotnet run -- --urls http://localhost:5188 --environment Development --Pyramid:PathBase /pyramid
} finally { Pop-Location }
