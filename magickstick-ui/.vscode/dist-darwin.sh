#!/bin/bash

# dist-darwin: Build and package macOS universal binary
# This script builds the Wails app for macOS universal architecture,
# signs it, and creates a distributable zip file with checksum.

set -e  # Exit on any error

echo "🚀 Starting dist-darwin build process..."

# Step 1: Build the Wails app for macOS universal
echo "📦 Building Wails app for darwin/universal..."
wails build -platform darwin/universal

# Step 2: Code sign the app (using ad-hoc signing with -)
echo "🔐 Code signing the app..."
codesign --force --deep -s - build/bin/magicstick-ui.app

# Step 3: Create dist directory
echo "📁 Creating dist directory..."
mkdir -p dist

# Step 4: Create zip archive
echo "🗜️  Creating zip archive..."
ditto -c -k --keepParent build/bin/magicstick-ui.app dist/magicstick-ui.zip

# Step 5: Generate SHA256 checksum
echo "🔍 Generating SHA256 checksum..."
shasum -a 256 dist/magicstick-ui.zip > dist/magicstick-ui.zip.sha256

echo "✅ dist-darwin build completed successfully!"
echo "📦 Output files:"
echo "   - dist/magicstick-ui.zip"
echo "   - dist/magicstick-ui.zip.sha256"
