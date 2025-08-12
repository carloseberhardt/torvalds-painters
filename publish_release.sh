#!/bin/bash

DLL=torvalds-painters/bin/Release/net48/torvalds-painters.dll
PLUGINS=torvalds-painters/Package/plugins
PACKAGE_DIR=torvalds-painters/Package

VERSION=$1

# Check that source files exist and are readable
if [ ! -f "$DLL" ]; then
    echo "Error: $DLL does not exist or is not readable."
    exit 1
fi

# Check that target directory exists and is writable
if [ ! -d "$PLUGINS" ]; then
    echo "Error: $PLUGINS directory does not exist."
    exit 1
fi

# Copy DLL to plugins folder
cp -f "$DLL" "$PLUGINS" || { echo "Error: Failed to copy $DLL"; exit 1; }

# Create zip filename
if [ ! -z "$VERSION" ]; then
    ZIPNAME="torvalds-painters.$VERSION.zip"
else
    ZIPNAME="torvalds-painters.zip"
fi

ZIPDESTINATION="torvalds-painters/bin/Release/$ZIPNAME"

# Create output directory if it doesn't exist
mkdir -p "torvalds-painters/bin/Release"

# Use PowerShell to create the zip since we're on Windows
powershell -Command "Compress-Archive -Path '$PACKAGE_DIR/*' -DestinationPath '$ZIPDESTINATION' -Force"

echo "Successfully created $ZIPDESTINATION"
