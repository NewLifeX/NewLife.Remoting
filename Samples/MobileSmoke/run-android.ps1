param(
    [String]$Configuration = "Release",
    [Int32]$TimeoutSec = 180
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot/../.."
$project = Join-Path $PSScriptRoot "MobileSmoke.csproj"
$package = "com.newlife.smoke.remoting"

# 项目是 android+ios 双目标，必须把目标框架收窄到单一平台；
# 否则在 Linux/macOS 上只跑一端时，还原期仍会解析另一平台的 workload，缺失时直接 NETSDK1178
dotnet build $project -c $Configuration -f net10.0-android -p:MobileTargetFramework=net10.0-android --nologo
if ($LASTEXITCODE -ne 0) { throw "Android build failed" }

$apk = Get-ChildItem (Join-Path $root "Bin/MobileSmoke") -Recurse -Filter *-Signed.apk |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) {
    $apk = Get-ChildItem (Join-Path $root "Bin/MobileSmoke") -Recurse -Filter *.apk |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}
if (-not $apk) { throw "APK not found" }

adb install -r $apk.FullName | Out-Host
if ($LASTEXITCODE -ne 0) { throw "adb install failed: $($apk.Name)" }
adb logcat -c
adb shell monkey -p $package -c android.intent.category.LAUNCHER 1 | Out-Host

$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    $hit = adb logcat -d -s MobileSmoke:* | Select-String "SMOKE_OK|SMOKE_FAIL" | Select-Object -Last 1
    if ($hit) {
        Write-Host $hit.Line
        if ($hit.Line -match "SMOKE_OK") { exit 0 }
        adb logcat -d -s MobileSmoke:* | Out-Host
        exit 1
    }
    Start-Sleep -Seconds 2
}

adb logcat -d -s MobileSmoke:* | Out-Host
throw "Android smoke timed out"
