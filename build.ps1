param(
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$sdk = Join-Path $PSScriptRoot ".tools\dotnet\dotnet.exe"
$installer = Join-Path $PSScriptRoot ".tools\dotnet-install.ps1"

if (-not (Test-Path $sdk)) {
    New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot ".tools") | Out-Null
    Invoke-WebRequest -UseBasicParsing "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer
    & $installer -Channel 8.0 -InstallDir (Join-Path $PSScriptRoot ".tools\dotnet") -NoPath
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

if ($Publish) {
    & $sdk publish (Join-Path $PSScriptRoot "MajsoulReview.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o (Join-Path $PSScriptRoot "publish") `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false
} else {
    & $sdk build (Join-Path $PSScriptRoot "MajsoulReview.csproj") -c Release
}

exit $LASTEXITCODE
