#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="$ROOT/Samples/MobileSmoke/MobileSmoke.csproj"
CONFIGURATION="${CONFIGURATION:-Release}"

# 模拟器与宿主同架构：arm64 主机用 iossimulator-arm64，x64 主机用 iossimulator-x64
if [ "$(uname -m)" = "arm64" ]; then
  DEFAULT_RUNTIME="iossimulator-arm64"
else
  DEFAULT_RUNTIME="iossimulator-x64"
fi
RUNTIME="${RUNTIME:-$DEFAULT_RUNTIME}"

BUNDLE_ID="com.newlife.smoke.remoting"
TIMEOUT="${TIMEOUT:-180}"
ARTIFACTS="$ROOT/Bin/MobileSmoke/artifacts"

mkdir -p "$ARTIFACTS"

# 项目是 android+ios 双目标，必须把目标框架收窄到单一平台；
# 否则 macOS 上还原期仍会解析 Android workload，缺失时直接 NETSDK1178
dotnet build "$PROJECT" -c "$CONFIGURATION" -f net10.0-ios \
  -p:MobileTargetFramework=net10.0-ios -p:RuntimeIdentifier="$RUNTIME" --nologo

APP="$(find "$ROOT/Bin/MobileSmoke" -maxdepth 6 -name "*.app" -print -quit)"
if [ -z "$APP" ]; then
  echo "iOS app bundle not found."
  exit 1
fi

UDID="$(xcrun simctl list devices available | awk -F '[()]' '/iPhone/ {print $2; exit}')"
if [ -z "$UDID" ]; then
  echo "No iPhone simulator available."
  exit 1
fi

xcrun simctl boot "$UDID" 2>/dev/null || true
xcrun simctl bootstatus "$UDID" -b
xcrun simctl install "$UDID" "$APP"
xcrun simctl terminate "$UDID" "$BUNDLE_ID" 2>/dev/null || true
xcrun simctl launch "$UDID" "$BUNDLE_ID"

RESULT=""
DEADLINE=$((SECONDS + TIMEOUT))
while [ "$SECONDS" -lt "$DEADLINE" ]; do
  DATA_DIR="$(xcrun simctl get_app_container "$UDID" "$BUNDLE_ID" data 2>/dev/null || true)"
  if [ -n "$DATA_DIR" ]; then
    RESULT="$(find "$DATA_DIR" -name smoke.json -print -quit 2>/dev/null || true)"
    if [ -n "$RESULT" ]; then break; fi
  fi
  sleep 1
done

if [ -z "$RESULT" ]; then
  echo "smoke.json not found within ${TIMEOUT}s."
  xcrun simctl spawn "$UDID" log show --last 2m --style compact --predicate 'process == "MobileSmoke"' > "$ARTIFACTS/ios-log.txt" 2>&1 || true
  cat "$ARTIFACTS/ios-log.txt" || true
  exit 1
fi

cp "$RESULT" "$ARTIFACTS/ios-smoke.json"
cat "$RESULT"
grep -q '"fail":0' "$RESULT"
echo "iOS smoke passed."
