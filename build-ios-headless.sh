#!/bin/bash

# Configuration
UNITY_PATH="/Applications/Unity/Hub/Editor/Unity/Contents/MacOS/Unity"
PROJECT_PATH="/Users/kiloverse/unity/kiloverse"
BUILD_PATH="$PROJECT_PATH/Builds/iOS_Project"
IPA_PATH="$PROJECT_PATH/Builds/IPA"
SCHEME="Unity-iPhone"

# --- Code Signing Configuration ---
# Replace with your Apple Team ID (e.g., ABC123DEFG)
TEAM_ID="YOUR_TEAM_ID_HERE"
# ----------------------------------

echo "========================================"
echo "      Starting Kiloverse iOS Build      "
echo "========================================"

# 1. Check if Unity executable is reachable
if [ ! -x "$UNITY_PATH" ]; then
    echo "Error: Unity executable not found at $UNITY_PATH"
    echo "Please ensure your Unity drive is mounted or update UNITY_PATH in this script."
    exit 1
fi

# 2. Run Unity Headless Build (Generate Xcode Project)
echo "Step 1: Generating Xcode Project from Unity..."
"$UNITY_PATH" \
  -batchmode \
  -nographics \
  -projectPath "$PROJECT_PATH" \
  -executeMethod HeadlessBuilder.BuildIOS \
  -customBuildPath "$BUILD_PATH" \
  -quit

if [ $? -ne 0 ]; then
    echo "Error: Unity build failed. Check the logs."
    exit 1
  fi

echo "Xcode Project generated at: $BUILD_PATH"

# 3. Build & Archive with xcodebuild
echo "Step 2: Archiving Xcode Project..."
cd "$BUILD_PATH"

# Clean build
xcodebuild clean -project Unity-iPhone.xcodeproj -scheme "$SCHEME" -configuration Release

# Create Archive with Automatic Signing
xcodebuild archive \
  -project Unity-iPhone.xcodeproj \
  -scheme "$SCHEME" \
  -configuration Release \
  -archivePath "$IPA_PATH/kiloverse.xcarchive" \
  DEVELOPMENT_TEAM="$TEAM_ID" \
  ALLOW_PROVISIONING_DEVICE_REGISTRATION=YES

if [ $? -ne 0 ]; then
    echo "Error: Xcode archive failed."
    exit 1
fi

echo "Archive created at: $IPA_PATH/kiloverse.xcarchive"

# 4. Export IPA (Requires exportOptions.plist)
if [ -f "$PROJECT_PATH/exportOptions.plist" ]; then
    echo "Step 3: Exporting IPA..."
    mkdir -p "$IPA_PATH/Output"
    xcodebuild -exportArchive \
      -archivePath "$IPA_PATH/kiloverse.xcarchive" \
      -exportOptionsPlist "$PROJECT_PATH/exportOptions.plist" \
      -exportPath "$IPA_PATH/Output" \
      -allowProvisioningUpdates
    
    if [ $? -eq 0 ]; then
        echo "IPA exported successfully to: $IPA_PATH/Output"
    else
        echo "Error: IPA export failed."
    fi
else
    echo "Skipping Step 3: No exportOptions.plist found in $PROJECT_PATH."
    echo "You can manually export the IPA from the .xcarchive in Xcode."
fi

echo "========================================"
echo "          Build Process Complete        "
echo "========================================"
