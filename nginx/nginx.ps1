[CmdletBinding()]
param(
    [ValidateSet('start', 'stop', 'reload', 'test')]
    [string]$Action = 'start'
)

$ErrorActionPreference = 'Stop'

$command = Get-Command nginx -ErrorAction SilentlyContinue
$nginx = if ($command) { $command.Source } else {
    Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter nginx.exe -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $nginx) {
    throw 'nginx.exe not found. Install it with: winget install --id nginxinc.nginx --exact'
}

# Forward slashes avoid a trailing backslash escaping the closing quote in Windows argument parsing.
$prefix = ($PSScriptRoot -replace '\\', '/') + '/'
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'logs'), (Join-Path $PSScriptRoot 'temp') | Out-Null

$siteIndex = Join-Path $PSScriptRoot '..\frontend\sso-session-ui\dist\sso-session-ui\browser\index.html'
$baseArgs = @('-p', $prefix, '-c', 'conf/nginx.conf')

switch ($Action) {
    'test' { & $nginx @baseArgs -t }
    'reload' { & $nginx @baseArgs -s reload }
    'stop' { & $nginx @baseArgs -s quit }
    'start' {
        if (-not (Test-Path $siteIndex)) {
            Write-Warning 'Angular build not found. Run "npx ng build" in frontend\sso-session-ui first.'
        }
        & $nginx @baseArgs -t
        if ($LASTEXITCODE -ne 0) { throw 'nginx configuration test failed.' }
        Start-Process -FilePath $nginx -ArgumentList $baseArgs -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
        Write-Host 'nginx started on http://localhost:8080'
    }
}
