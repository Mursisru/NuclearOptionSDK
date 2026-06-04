# Автопроверка Nuclear Studio: build (с UI trace для smoke) + unit tests + headless smoke + ui-trace.log

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot



Write-Host "=== Nuclear Studio verify ===" -ForegroundColor Cyan



Get-Process -Name "StudioSmoke","NuclearOptionSDK.Studio" -ErrorAction SilentlyContinue |

    Where-Object { $_.Path -like "*StudioSmoke*" -or $_.MainWindowTitle -eq "" } |

    Stop-Process -Force -ErrorAction SilentlyContinue



$studio = Join-Path $root "src\NuclearOptionSDK.Studio\NuclearOptionSDK.Studio.csproj"

$tests = Join-Path $root "tests\NuclearOptionSDK.Studio.Tests\NuclearOptionSDK.Studio.Tests.csproj"

$studioExe = Join-Path $root "src\NuclearOptionSDK.Studio\bin\Release\net8.0\NuclearOptionSDK.Studio.exe"

$smokeOut = Join-Path $root "src\NuclearOptionSDK.Studio\bin\Release\net8.0\smoke-output"

$traceLog = Join-Path $smokeOut "ui-trace.log"



Write-Host "Build Studio (Release + StudioUiTrace for smoke)..." -ForegroundColor Yellow

dotnet build $studio -c Release -p:StudioUiTrace=true

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }



Write-Host "Unit tests..." -ForegroundColor Yellow

dotnet test $tests -c Release

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }



Write-Host "Headless smoke (ui-trace.log, no screenshots)..." -ForegroundColor Yellow

if (-not (Test-Path $studioExe)) {

    Write-Error "Studio exe not found: $studioExe"

    exit 1

}

& $studioExe --smoke

$code = $LASTEXITCODE



if (Test-Path $traceLog) {

    Write-Host "UI trace: $traceLog" -ForegroundColor Green

    $splitLines = Select-String -Path $traceLog -Pattern "split\.(drag|pixel|apply)" | Select-Object -First 8

    foreach ($line in $splitLines) {

        Write-Host "  $($line.Line)"

    }

}



if ($code -ne 0) {

    $fail = Join-Path $smokeOut "failures.txt"

    if (Test-Path $fail) { Get-Content $fail }

    exit $code

}



Write-Host "All checks passed." -ForegroundColor Green

exit 0

