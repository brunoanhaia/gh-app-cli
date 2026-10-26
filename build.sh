#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

dotnet_exe="${DOTNET:-dotnet}"
version="${VERSION:-1.0.0}"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_UI_LANGUAGE=en-US

"$dotnet_exe" --version
"$dotnet_exe" tool restore
"$dotnet_exe" csharpier check .
"$dotnet_exe" test --solution GhApp.slnx -c Release

mkdir -p bin artifacts

publish_app() {
    local rid="$1"
    local output="artifacts/$rid"
    rm -rf "$output"
    "$dotnet_exe" publish src/GhApp/GhApp.csproj \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:Version="$version" \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:DebugType=None \
        -p:DebugSymbols=false \
        -o "$output"
}

publish_app win-x64
publish_app linux-x64
publish_app linux-arm64

cp artifacts/win-x64/gh-app.exe bin/gh-app.exe
cp artifacts/linux-x64/gh-app bin/gh-app-linux-amd64
cp artifacts/linux-arm64/gh-app bin/gh-app-linux-arm64
chmod +x bin/gh-app-linux-amd64 bin/gh-app-linux-arm64

ls -l bin
