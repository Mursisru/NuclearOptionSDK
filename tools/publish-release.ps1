param(
    [string]$Configuration = "Release",
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$studioOut = Join-Path $root "release\NuclearStudio"
$bridgeOut = Join-Path $root "release\Bridge"
$studioExe = Join-Path $studioOut "NuclearOptionSDK.Studio.exe"

$dotnetArgs = @("-c", $Configuration, "--nologo")
if (-not $Verbose) {
    $dotnetArgs += @("-v", "q", "-clp:ErrorsOnly;Summary")
}

$studioRunning = Get-Process -Name "NuclearOptionSDK.Studio" -ErrorAction SilentlyContinue
if ($studioRunning) {
    Write-Host "WARN: Close Nuclear Studio before publish (files may be locked)." -ForegroundColor Yellow
}

Write-Host "Publishing Nuclear Studio ($Configuration) -> release\NuclearStudio" -ForegroundColor Cyan

Push-Location $root
try {
    # Release publish: StudioUiTrace MUST stay false (no STUDIO_UI_TRACE — см. StudioFeatures.cs).
    dotnet publish (Join-Path $root "src\NuclearOptionSDK.Studio\NuclearOptionSDK.Studio.csproj") `
        @dotnetArgs `
        -o $studioOut `
        --self-contained false `
        -p:PublishSingleFile=false `
        -p:StudioUiTrace=false

    if ($LASTEXITCODE -ne 0) {
        throw "Studio publish failed (exit $LASTEXITCODE)."
    }

    $bridgeBin = Join-Path $root "src\NuclearOptionSDK.Bridge\bin\$Configuration\net48"
    if (Test-Path $bridgeBin) {
        if (Test-Path $bridgeOut) {
            Remove-Item $bridgeOut -Recurse -Force
        }

        New-Item -ItemType Directory -Path $bridgeOut | Out-Null
        Copy-Item (Join-Path $bridgeBin "*.dll") $bridgeOut -Force
    }

    $versionPath = Join-Path $root "src\NuclearOptionSDK.Studio\AppVersion.cs"
    $version = Get-Content $versionPath -Raw
    $ver = "unknown"
    $sub = "?"
    if ($version -match 'ReleaseBase = "([^"]+)"') {
        $ver = $Matches[1]
    }

    if ($version -match 'SubNumber = (\d+)') {
        $sub = $Matches[1]
    }

    $readme = @(
        "Nuclear Studio $ver Build DEV1VP$sub"
        "Published: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
        ""
        "Studio: release\NuclearStudio\NuclearOptionSDK.Studio.exe"
        "Bridge: release\Bridge\"
    )
    Set-Content (Join-Path $root "release\README.txt") -Value $readme -Encoding UTF8

    if (Test-Path $studioExe) {
        Write-Host "OK: $studioExe" -ForegroundColor Green
    }
    else {
        Write-Host "WARN: exe missing (Studio may lock files)." -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}
