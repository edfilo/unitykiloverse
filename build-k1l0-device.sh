#!/bin/bash
# K1L0 iOS build lanes:
#   --swift-only (default): sync changed Swift files, skip Unity/IL2CPP
#   --unity: run CommandLineBuild.BuildiOS, then Xcode
#   --ota: archive and publish instead of device install
# Live render-tuning changes require no build.
set -euo pipefail

PROJECT="/Users/kiloverse/unitykiloverse"
IOS_BUILD="$PROJECT/Builds/iOS"
UNITY="/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity"
TEAM_ID="7R2746UPX7"
BUNDLE_ID="com.filowatt.K1L0"
PREFERRED_DEVICE="00008140-000A292A0CE0801C"
MODE="device"
LANE="swift"

for arg in "$@"; do
  case "$arg" in
    --swift-only|--xcode-only) LANE="swift" ;;
    --unity) LANE="unity" ;;
    --ota) MODE="ota" ;;
    *) echo "Unknown option: $arg"; exit 2 ;;
  esac
done

check_build_lane() {
  local active bridge_active
  active=$(
    for name in Unity xcodebuild il2cpp; do pgrep -x "$name" 2>/dev/null || true; done
  )
  if [ -n "$active" ]; then
    echo "K1L0/Unity build lane already active; refusing a second build:"
    echo "$active"
    exit 3
  fi
  bridge_active=$(curl -s --max-time 2 http://localhost:5055/v2/build-jobs 2>/dev/null \
    | python3 -c 'import json,sys; d=json.load(sys.stdin); print("\n".join(f"{j.get(chr(105)+chr(100))} {j.get(chr(112)+chr(114)+chr(111)+chr(102)+chr(105)+chr(108)+chr(101)+chr(95)+chr(110)+chr(97)+chr(109)+chr(101))} {j.get(chr(115)+chr(116)+chr(97)+chr(116)+chr(117)+chr(115))}" for j in d.get("jobs",[]) if j.get("status") in ("queued","running","building")))' 2>/dev/null || true)
  if [ -n "$bridge_active" ]; then
    echo "TedFred build lane already active; refusing a second build:"
    echo "$bridge_active"
    exit 3
  fi
}

sync_swift() {
  local src dst changed=0
  mkdir -p "$IOS_BUILD/Libraries/Plugins/iOS"
  while IFS= read -r src; do
    dst="$IOS_BUILD/Libraries/Plugins/iOS/$(basename "$src")"
    if [ ! -f "$dst" ] || ! cmp -s "$src" "$dst"; then
      cp -p "$src" "$dst"
      echo "Copied changed Swift: $(basename "$src")"
      changed=$((changed + 1))
    fi
  done < <(find "$PROJECT/Assets/Plugins/iOS" -maxdepth 1 -type f -name '*.swift' | sort)
  if [ "$changed" -eq 0 ]; then
    echo "Swift overlay already synchronized; preserving timestamps."
  fi
}

patch_xcode_export() {
  printf '#!/bin/sh\nexit 0\n' > "$IOS_BUILD/process_symbols.sh"
  chmod +x "$IOS_BUILD/process_symbols.sh"
  sed -i '' 's/CYE232ULMR/7R2746UPX7/g' "$IOS_BUILD/Unity-iPhone.xcodeproj/project.pbxproj"
  sed -i '' '/PROVISIONING_PROFILE_APP *= *"[0-9A-Fa-f-]*";/d' "$IOS_BUILD/Unity-iPhone.xcodeproj/project.pbxproj" || true
  if [ -f "$IOS_BUILD/Unity-iPhone.entitlements" ]; then
    /usr/libexec/PlistBuddy -c 'Delete :com.apple.developer.location.push' "$IOS_BUILD/Unity-iPhone.entitlements" 2>/dev/null || true
  fi
}

device_id() {
  if xcrun devicectl list devices 2>/dev/null | grep "$PREFERRED_DEVICE" | grep -vi unavailable >/dev/null; then
    echo "$PREFERRED_DEVICE"
    return
  fi
  xcrun devicectl list devices 2>/dev/null | grep -i iphone | grep -vi unavailable \
    | awk '{for(i=1;i<=NF;i++) if($i ~ /^[A-F0-9]{8}-/ || $i ~ /^[0-9A-Fa-f-]{25,}$/) {print $i; exit}}'
}

check_build_lane
echo "K1L0 lane=$LANE mode=$MODE"

if [ "$LANE" = "unity" ]; then
  echo "Running Unity export; preserving Library, Temp, Builds/iOS, and DerivedData caches."
  "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
    -executeMethod CommandLineBuild.BuildiOS -logFile /tmp/k1l0-unity-export.log
  grep -q 'Build Finished, Result: Success' /tmp/k1l0-unity-export.log
else
  echo "Skipping Unity and IL2CPP for Swift-only lane."
  sync_swift
fi

patch_xcode_export

DEVICE=""
if [ "$MODE" = "device" ]; then
  DEVICE=$(device_id)
  if [ -z "$DEVICE" ]; then
    echo "No physical iPhone available; automatically pivoting to OTA."
    MODE="ota"
  fi
fi

if [ "$MODE" = "device" ]; then
  XCODE_LOG="/tmp/k1l0-incremental-device-build.log"
  xcodebuild -project "$IOS_BUILD/Unity-iPhone.xcodeproj" -scheme Unity-iPhone \
    -configuration Debug -destination "id=$DEVICE" -allowProvisioningUpdates \
    DEVELOPMENT_TEAM="$TEAM_ID" CODE_SIGN_STYLE=Automatic build > "$XCODE_LOG" 2>&1
  APP=$(find "$HOME/Library/Developer/Xcode/DerivedData" -path '*/Build/Products/Debug-iphoneos/K1L0.app' -print0 \
    | xargs -0 ls -td 2>/dev/null | head -1)
  test -n "$APP"
  xcrun devicectl device install app --device "$DEVICE" "$APP"
  xcrun devicectl device process launch --device "$DEVICE" "$BUNDLE_ID"
  echo "BUILD SUCCEEDED; installed and launched on $DEVICE"
  echo "Log: $XCODE_LOG"
else
  ARCHIVE="/tmp/k1l0_archive/K1L0.xcarchive"
  OTA="/tmp/tedfred_ota/k1l0/latest"
  BUILD_NUMBER="$(date +%Y%m%d%H%M)"
  mkdir -p /tmp/k1l0_archive "$OTA"
  XCODE_LOG="/tmp/k1l0-incremental-archive.log"
  xcodebuild -project "$IOS_BUILD/Unity-iPhone.xcodeproj" -scheme Unity-iPhone \
    -configuration Release -archivePath "$ARCHIVE" -allowProvisioningUpdates \
    DEVELOPMENT_TEAM="$TEAM_ID" CODE_SIGN_STYLE=Automatic \
    CURRENT_PROJECT_VERSION="$BUILD_NUMBER" archive > "$XCODE_LOG" 2>&1
  rm -rf /tmp/k1l0_payload
  mkdir -p /tmp/k1l0_payload/Payload
  cp -R "$ARCHIVE/Products/Applications/K1L0.app" /tmp/k1l0_payload/Payload/
  (cd /tmp/k1l0_payload && zip -qr "$OTA/app.ipa" Payload)
  /usr/libexec/PlistBuddy -c 'Clear dict' "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c 'Add :items array' "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c 'Add :items:0 dict' "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c 'Add :items:0:assets array' "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c 'Add :items:0:assets:0 dict' "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c 'Add :items:0:assets:0:kind string software-package' "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c 'Add :items:0:assets:0:url string https://tunnel.kilo.gallery/ota/k1l0/latest/app.ipa' "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c 'Add :items:0:metadata dict' "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c 'Add :items:0:metadata:bundle-identifier string com.filowatt.K1L0' "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c "Add :items:0:metadata:bundle-version string $BUILD_NUMBER" "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c 'Add :items:0:metadata:kind string software' "$OTA/manifest.plist"
  /usr/libexec/PlistBuddy -c 'Add :items:0:metadata:title string K1L0' "$OTA/manifest.plist"
  curl -fsS -X GET https://tunnel.kilo.gallery/ota/k1l0/latest/manifest.plist >/dev/null
  test -s "$OTA/app.ipa"
  echo 'Install link: https://tunnel.kilo.gallery/ota/k1l0/latest/'
  echo "ARCHIVE SUCCEEDED; log: $XCODE_LOG"
fi
