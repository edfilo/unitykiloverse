#!/bin/bash
set -euo pipefail

SRC="$1"
OUT="$2"
mkdir -p "$OUT"

HEADER='import AVFoundation
import AVKit
import CoreImage
import CoreLocation
import Darwin
import Foundation
import Metal
import MetalKit
import SwiftUI
#if canImport(AppKit)
import AppKit
#endif
'

for file in 01_Root.swift 02_Transmission.swift 03_Home.swift 04_Data.swift; do
  printf '%s\n' "$HEADER" > "$OUT/$file"
done

awk -v out="$OUT" '
function target(line) {
  if (line ~ /^private struct K1L0LoginPermissionGate:/) return "01_Root.swift"
  if (line ~ /^private struct TransmissionTextTransform:/) return "02_Transmission.swift"
  if (line ~ /^private struct StepStatBlock:/) return "03_Home.swift"
  if (line ~ /^private final class K1L0OverlayDataModel:/) return "04_Data.swift"
  return ""
}
BEGIN { file = "00_Core.swift" }
{
  nextFile = target($0)
  if (nextFile != "") {
    file = nextFile
    initialized[file] = 1
  }
  if (!(file in initialized)) initialized[file] = 1
  # Generated Mac chunks form one module, so declarations and members referenced
  # by another chunk must be module-visible. The authoritative iOS source keeps
  # its original access control; only these temporary generated files are widened.
  gsub(/private\(set\)/, "internal(set)")
  gsub(/fileprivate /, "")
  gsub(/private /, "")
  print $0 >> (out "/" file)
}
' "$SRC"

# Core retains the authoritative imports; other chunks receive the compact
# platform-neutral header above.
echo "$OUT/00_Core.swift"
echo "$OUT/01_Root.swift"
echo "$OUT/02_Transmission.swift"
echo "$OUT/03_Home.swift"
echo "$OUT/04_Data.swift"
