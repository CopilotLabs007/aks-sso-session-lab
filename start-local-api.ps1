$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'backend\SsoSession.Api\SsoSession.Api.csproj'
if (-not (Test-Path $projectPath)) {
    throw "API project was not found at '$projectPath'."
}

Write-Host 'Starting the API with configuration already available to this process.'
Write-Host 'If startup reports a missing setting, provision it through an approved local secret-management route.'

& dotnet run --project $projectPath
exit $LASTEXITCODE