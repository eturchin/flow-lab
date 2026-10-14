param([switch]$SkipFrontend)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (-not $SkipFrontend) {
        Push-Location frontend
        try {
            if (-not (Test-Path node_modules)) {
                npm ci
                if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
            }
            npm run build
            if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed' }
        } finally { Pop-Location }
    }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    if (-not $env:ConnectionStrings__FlowLab) {
        # Public local-development credentials from the specification, never production.
        $env:ConnectionStrings__FlowLab = 'Host=localhost;Port=5432;Database=flowlab;Username=postgres;Password=postgres'
    }
    dotnet run --project src/FlowLab.Web --no-launch-profile --urls http://localhost:5080
    if ($LASTEXITCODE -ne 0) { throw 'FlowLab stopped with an error' }
} finally { Pop-Location }
