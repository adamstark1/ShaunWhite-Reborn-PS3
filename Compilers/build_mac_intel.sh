#!/bin/bash

cd "$(dirname "$0")/.."

VERSION="1.0.0"

echo "Compiling macOS Intel (osx-x64) build for version $VERSION..."

rm -rf "Shaun White Reborn.app" ./temp_backend ./temp_launcher

dotnet publish ShaunWhiteReborn.csproj -c Release -r osx-x64 --self-contained true -p:UseAppHost=true -p:Version=$VERSION -o ./temp_backend
dotnet publish ShaunWhiteReborn.Launcher/ShaunWhiteReborn.Launcher.csproj -c Release -r osx-x64 --self-contained true -p:UseAppHost=true -p:Version=$VERSION -o ./temp_launcher

mkdir -p "Shaun White Reborn.app/Contents/MacOS"
mkdir -p "Shaun White Reborn.app/Contents/Backend"
mkdir -p "Shaun White Reborn.app/Contents/Resources"

cp -R ./temp_launcher/* "Shaun White Reborn.app/Contents/MacOS/"
cp -R ./temp_backend/* "Shaun White Reborn.app/Contents/Backend/"
cp shaunwhite.pfx "Shaun White Reborn.app/Contents/Backend/"
cp Assets/AppIcon.icns "Shaun White Reborn.app/Contents/Resources/"

chmod +x "Shaun White Reborn.app/Contents/MacOS/ShaunWhiteReborn.Launcher"
chmod +x "Shaun White Reborn.app/Contents/Backend/ShaunWhiteReborn"

cat > "Shaun White Reborn.app/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>ShaunWhiteReborn.Launcher</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleName</key>
    <string>Shaun White Reborn</string>
    <key>CFBundleDisplayName</key>
    <string>Shaun White Reborn</string>
    <key>CFBundleIdentifier</key>
    <string>com.adamstark.shaunwhitereborn</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
</dict>
</plist>
EOF

rm -rf ./temp_backend ./temp_launcher
xattr -cr "Shaun White Reborn.app"

codesign --force --sign - "Shaun White Reborn.app/Contents/Backend/ShaunWhiteReborn"
codesign --force --sign - "Shaun White Reborn.app/Contents/MacOS/ShaunWhiteReborn.Launcher"
codesign --force --deep --sign - "Shaun White Reborn.app"

xattr -cr "Shaun White Reborn.app"
touch "Shaun White Reborn.app"

echo "MacOS Intel build compiled. Happy trophy hunting! :)"