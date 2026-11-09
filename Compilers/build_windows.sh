#!/bin/bash

cd "$(dirname "$0")/.."

VERSION="1.0.0"
OUT_DIR="Shaun White Reborn Windows"
ICON_PATH="Assets/icon.ico"

echo "Compiling Windows build for version $VERSION..."

rm -rf "$OUT_DIR"
mkdir -p "$OUT_DIR"

dotnet publish ShaunWhiteReborn.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:UseAppHost=true -p:DebugType=None -p:Version=$VERSION -p:ApplicationIcon=$ICON_PATH -o "$OUT_DIR"
dotnet publish ShaunWhiteReborn.Launcher/ShaunWhiteReborn.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:UseAppHost=true -p:DebugType=None -p:Version=$VERSION -p:ApplicationIcon=$ICON_PATH -o "$OUT_DIR"

echo "Deleting unnecessary files..."

mkdir -p "$OUT_DIR/temp_keep"

mv "$OUT_DIR/ShaunWhiteReborn.exe" "$OUT_DIR/temp_keep/" 2>/dev/null
mv "$OUT_DIR/ShaunWhiteReborn.Launcher.exe" "$OUT_DIR/temp_keep/" 2>/dev/null
mv "$OUT_DIR/av_libglesv2.dll" "$OUT_DIR/temp_keep/" 2>/dev/null
mv "$OUT_DIR/libHarfBuzzSharp.dll" "$OUT_DIR/temp_keep/" 2>/dev/null
mv "$OUT_DIR/libSkiaSharp.dll" "$OUT_DIR/temp_keep/" 2>/dev/null
cp shaunwhite.pfx "$OUT_DIR/temp_keep/"

find "$OUT_DIR" -mindepth 1 -maxdepth 1 -not -name "temp_keep" -exec rm -rf {} +

mv "$OUT_DIR"/temp_keep/* "$OUT_DIR"/
rmdir "$OUT_DIR/temp_keep"

echo "Windows build compiled. Happy trophy hunting! :)"
