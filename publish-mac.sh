#!/usr/bin/env bash
# Builds HexManiacAdvance.app (self-contained, no .NET install needed to run it).
# Usage: ./publish-mac.sh [osx-arm64|osx-x64]
set -euo pipefail

RID="${1:-osx-arm64}"
NAME="HexManiacAdvance"
OUT="artifacts/mac"
APP="$OUT/$NAME.app"

rm -rf "$OUT"
dotnet publish src/HexManiac.Avalonia/HexManiac.Avalonia.csproj \
  -c Release -r "$RID" --self-contained true \
  -p:UseAppHost=true -o "$OUT/publish"

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT/publish/." "$APP/Contents/MacOS/"

VERSION="$(git describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || echo 0.0.0)"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>$NAME</string>
  <key>CFBundleDisplayName</key><string>HexManiacAdvance</string>
  <key>CFBundleIdentifier</key><string>io.github.hexmaniacadvance.mac</string>
  <key>CFBundleExecutable</key><string>$NAME</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>${VERSION:-0.0.0}</string>
  <key>CFBundleVersion</key><string>${VERSION:-0.0.0}</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>CFBundleDocumentTypes</key>
  <array>
    <dict>
      <key>CFBundleTypeName</key><string>Game Boy Advance ROM</string>
      <key>CFBundleTypeExtensions</key><array><string>gba</string></array>
      <key>CFBundleTypeRole</key><string>Editor</string>
    </dict>
  </array>
</dict>
</plist>
PLIST

# Ad-hoc signature so Apple Silicon will launch it locally.
# For sharing with others you'll need a Developer ID signature and notarization.
codesign --force --deep --sign - "$APP"
echo "Built $APP"
