#!/bin/bash
# Full K1L0 deploy pipeline: Unity -> Xcode debug & deploy -> Xcode archive & package -> restart kiloworld-api
set -eo pipefail

UNITY="/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity"
PROJECT="/Users/kiloverse/unitykiloverse"
IOS_BUILD="$PROJECT/Builds/iOS"
BUNDLE_ID="com.filowatt.K1L0"
TEAM_ID="7R2746UPX7"
APP_NAME="K1L0"
PM2="/Users/kiloverse/.nvm/versions/node/v22.11.0/bin/pm2"

echo "=== K1L0 Full Deployment Script ==="
date

# Detect connected device
DEVICE_ID=""
PREFERRED_DEVICE_ID="00008140-000A292A0CE0801C"
if xcrun devicectl list devices 2>/dev/null | grep "$PREFERRED_DEVICE_ID" | grep -vi "unavailable" >/dev/null; then
  DEVICE_ID="$PREFERRED_DEVICE_ID"
  echo "  ✓ Using Preferred Device: $DEVICE_ID"
else
  # Find first available iPhone
  FIRST_IPHONE=$(xcrun devicectl list devices 2>/dev/null | grep -i "iphone" | grep -vi "unavailable" | grep -E "connected|available" | awk '{for(i=1;i<=NF;i++) if($i ~ /^[A-F0-9]{8}-/ || $i ~ /^[0-9a-fA-F-]{25,}/) print $i}' | head -1 || true)
  if [ -n "$FIRST_IPHONE" ]; then
    DEVICE_ID="$FIRST_IPHONE"
    echo "  ✓ Falling back to connected iPhone: $DEVICE_ID"
  else
    echo "  ! No connected iPhone detected."
    DEVICE_ID=""
  fi
fi

# 1. Run Unity BuildiOS
USE_UNITY="${K1L0_USE_UNITY:-1}"
if [ "$USE_UNITY" = "1" ]; then
  echo "▸ Step 1: Running Unity batchmode BuildiOS..."
  pkill -9 -f "Unity.*MacOS/Unity" 2>/dev/null || true
  pkill -9 -f "Unity.ILPP|bee_backend|Unity.Licensing|UnityPackageManager|VBCSCompiler" 2>/dev/null || true
  sleep 1
  rm -rf "$PROJECT/Temp" 2>/dev/null || true

  "$UNITY" -batchmode -quit -nographics \
    -projectPath "$PROJECT" \
    -executeMethod CommandLineBuild.BuildiOS \
    -logFile "/tmp/k1l0_unity_build_custom.log" 2>&1

  if grep -q "Build Finished, Result: Success" "/tmp/k1l0_unity_build_custom.log" 2>/dev/null; then
    echo "  ✓ Unity build succeeded!"
  else
    echo "  ✗ Unity build FAILED. See /tmp/k1l0_unity_build_custom.log"
    exit 1
  fi
else
  echo "▸ Step 1: Skipping Unity batchmode BuildiOS (K1L0_USE_UNITY=0)"
fi

# 2. Patch Xcode Project
echo "▸ Step 2: Patching Xcode project..."
# Disable process_symbols
printf '#!/bin/sh\nexit 0\n' > "$IOS_BUILD/process_symbols.sh"
chmod +x "$IOS_BUILD/process_symbols.sh"

# Set Development Team
sed -i "" "s/CYE232ULMR/$TEAM_ID/g" "$IOS_BUILD/Unity-iPhone.xcodeproj/project.pbxproj"

# Remove hardcoded provisioning profile reference
sed -i "" '/PROVISIONING_PROFILE_APP *= *"[0-9A-Fa-f-]*";/d' "$IOS_BUILD/Unity-iPhone.xcodeproj/project.pbxproj" 2>/dev/null || true

# Strip com.apple.developer.location.push from entitlements
if [ -f "$IOS_BUILD/Unity-iPhone.entitlements" ]; then
  cat > "$IOS_BUILD/Unity-iPhone.entitlements" << 'ENTITLEMENTS'
<?xml version="1.0" encoding="utf-8"?>
<plist version="1.0">
  <dict>
    <key>com.apple.developer.applesignin</key>
    <array>
      <string>Default</string>
    </array>
  </dict>
</plist>
ENTITLEMENTS
  echo "  ✓ Stripped location push entitlement, kept Sign in with Apple"
fi

# 3. Direct Device Install (Debug build)
if [ -n "$DEVICE_ID" ]; then
  echo "▸ Step 3: Compiling for device ($DEVICE_ID) in Debug mode..."
  XCODE_LOG="/tmp/k1l0_xcode_build_debug.log"
  rm -f "$XCODE_LOG"

  xcodebuild -project "$IOS_BUILD/Unity-iPhone.xcodeproj" \
    -scheme Unity-iPhone -configuration Debug \
    -sdk iphoneos -arch arm64 \
    -allowProvisioningUpdates \
    ONLY_ACTIVE_ARCH=YES \
    CODE_SIGN_STYLE=Automatic \
    PROVISIONING_PROFILE_SPECIFIER="" \
    PROVISIONING_PROFILE="" \
    DEVELOPMENT_TEAM=$TEAM_ID \
    CODE_SIGN_IDENTITY="Apple Development" \
    CODE_SIGN_ALLOW_ENTITLEMENTS_MODIFICATION=YES \
    build > "$XCODE_LOG" 2>&1 || true

  if grep -q "BUILD SUCCEEDED" "$XCODE_LOG"; then
    echo "  ✓ Xcode debug build succeeded!"
    
    # Find built app
    APP=$(find ~/Library/Developer/Xcode/DerivedData -path "*/Debug-iphoneos/$APP_NAME.app" -maxdepth 5 2>/dev/null | head -1)
    if [ -n "$APP" ]; then
      echo "  ✓ Found app: $APP"
      echo "  ▸ Installing to device $DEVICE_ID..."
      xcrun devicectl device install app --device "$DEVICE_ID" "$APP"
      echo "  ▸ Launching com.filowatt.K1L0..."
      xcrun devicectl device process launch --device "$DEVICE_ID" "$BUNDLE_ID"
      echo "  ✓ Device deploy complete!"
    else
      echo "  ✗ Could not find $APP_NAME.app in DerivedData"
      exit 1
    fi
  else
    echo "  ✗ Xcode debug build FAILED. See $XCODE_LOG"
    grep -E "error:" "$XCODE_LOG" | head -10
    exit 1
  fi
else
  echo "▸ Step 3: Skipping direct device install (no device detected)"
fi

# 4. Xcode Release Archive & OTA Packaging
echo "▸ Step 4: Compiling for OTA in Release mode..."
ARCHIVE="/tmp/k1l0_archive/K1L0.xcarchive"
rm -rf "$ARCHIVE" 2>/dev/null
mkdir -p /tmp/k1l0_archive
XCODE_ARCHIVE_LOG="/tmp/k1l0_xcode_archive.log"

xcodebuild -project "$IOS_BUILD/Unity-iPhone.xcodeproj" \
  -scheme Unity-iPhone -configuration Release \
  -archivePath "$ARCHIVE" \
  -allowProvisioningUpdates \
  CODE_SIGN_STYLE=Automatic \
  PROVISIONING_PROFILE_SPECIFIER="" \
  PROVISIONING_PROFILE="" \
  DEVELOPMENT_TEAM=$TEAM_ID \
  CODE_SIGN_IDENTITY="Apple Development" \
  CODE_SIGN_ALLOW_ENTITLEMENTS_MODIFICATION=YES \
  archive > "$XCODE_ARCHIVE_LOG" 2>&1 || true

if [ -d "$ARCHIVE" ]; then
  echo "  ✓ Xcode release archive succeeded!"
else
  echo "  ✗ Xcode release archive FAILED. See $XCODE_ARCHIVE_LOG"
  grep -E "error:" "$XCODE_ARCHIVE_LOG" | head -10
  exit 1
fi

# Package IPA manually
echo "  ▸ Packaging IPA..."
rm -rf /tmp/ota_manual_pack 2>/dev/null
mkdir -p /tmp/ota_manual_pack/Payload
cp -R "$ARCHIVE/Products/Applications/$APP_NAME.app" /tmp/ota_manual_pack/Payload/
cd /tmp/ota_manual_pack
zip -qr app.ipa Payload/

# Copy to OTA directory
echo "  ▸ Copying to OTA directory..."
mkdir -p /tmp/tedfred_ota/k1l0/latest/
cp app.ipa /tmp/tedfred_ota/k1l0/latest/app.ipa

# Generate manifest
cat > /tmp/tedfred_ota/k1l0/latest/manifest.plist <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>items</key>
  <array>
    <dict>
      <key>assets</key>
      <array>
        <dict>
          <key>kind</key><string>software-package</string>
          <key>url</key><string>https://tunnel.kilo.gallery/ota/k1l0/latest/app.ipa</string>
        </dict>
      </array>
      <key>metadata</key>
      <dict>
        <key>bundle-identifier</key><string>com.filowatt.K1L0</string>
        <key>bundle-version</key><string>1.0</string>
        <key>kind</key><string>software</string>
        <key>title</key><string>K1L0</string>
      </dict>
    </dict>
  </array>
</dict>
</plist>
PLIST

echo "  ✓ OTA assets packaged successfully!"

# 5. Restart kiloworld-api
echo "▸ Step 5: Restarting kiloworld-api..."
"$PM2" restart kiloworld-api
echo "  ✓ restarted kiloworld-api"

# 6. Verification
echo "▸ Step 6: Verifying URL..."
curl -sI -X GET https://tunnel.kilo.gallery/ota/k1l0/latest/manifest.plist | head -n 10

echo "=== Deployment Pipeline Complete ==="
