param(
    [string]$Version = "1.0.0",
    [string]$Dotnet = "",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Dotnet)) {
    if ($env:DOTNET) {
        $Dotnet = $env:DOTNET
    } elseif (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $Dotnet = (Get-Command dotnet).Source
    } elseif (Test-Path "C:\Program Files\dotnet\dotnet.exe") {
        $Dotnet = "C:\Program Files\dotnet\dotnet.exe"
    } else {
        $Dotnet = "dotnet"
    }
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    & $Dotnet --version
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet not found: $Dotnet"
    }

    & $Dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed"
    }

    & $Dotnet csharpier check .
    if ($LASTEXITCODE -ne 0) {
        throw "csharpier check failed; run: dotnet csharpier format ."
    }

    if (-not $SkipTests) {
        & $Dotnet test --solution GhApp.slnx -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "tests failed"
        }
    }

    New-Item -ItemType Directory -Path "bin" -Force | Out-Null
    New-Item -ItemType Directory -Path "artifacts" -Force | Out-Null

    function Publish-App([string]$Rid) {
        $output = Join-Path "artifacts" $Rid
        if (Test-Path $output) {
            Remove-Item $output -Recurse -Force
        }
        & $Dotnet publish "src/GhApp/GhApp.csproj" `
            -c Release `
            -r $Rid `
            --self-contained true `
            -p:Version=$Version `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:DebugType=None `
            -p:DebugSymbols=false `
            -o $output
        if ($LASTEXITCODE -ne 0) {
            throw "publish $Rid failed"
        }
    }

    Publish-App "win-x64"
    Publish-App "linux-x64"
    Publish-App "linux-arm64"

    Copy-Item "artifacts/win-x64/gh-app.exe" "bin/gh-app.exe" -Force
    Copy-Item "artifacts/linux-x64/gh-app" "bin/gh-app-linux-amd64" -Force
    Copy-Item "artifacts/linux-arm64/gh-app" "bin/gh-app-linux-arm64" -Force

    Get-ChildItem bin | Select-Object Name, Length
}
finally {
    Pop-Location
}
