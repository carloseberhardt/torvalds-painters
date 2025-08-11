# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is "Torvald's Affordable Painters" - a Valheim mod built with the Jötunn modding framework. The mod adds a painting system that allows players to color building pieces using a custom painting mallet tool.

## Development Commands

### Building
- **Debug Build**: `dotnet build --configuration Debug` or use Visual Studio
- **Release Build**: `dotnet build --configuration Release`

### Publishing
- **Debug Deployment**: Uses PowerShell script `publish.ps1` (auto-runs on build)
- **Manual Debug Deployment**: `./publish_debug.sh` (Linux/Mac) 
- **Release Packaging**: `./publish_release.sh [version]` (Linux/Mac)

### Environment Setup
- Set `VALHEIM_INSTALL` path in `Environment.props` (currently: `E:\steamlibrary\steamapps\common\Valheim`)
- Debug builds auto-deploy to `%VALHEIM_INSTALL%\BepInEx\plugins`
- Release builds create ThunderStore-ready packages

## Architecture & Key Components

### Main Plugin Class: `TorvaldsPainters` (torvalds-painters.cs:16)
- **BepInEx Plugin**: Entry point with GUID `com.torvald.painters`
- **Jötunn Integration**: Uses PrefabManager and ItemManager for custom content
- **Harmony Patches**: Minimal patching approach for color persistence

### Core Systems

#### Color System (torvalds-painters.cs:29-45)
- **Predefined Palette**: `VikingColors` dictionary with 11 carefully tuned colors
- **Wood Tones**: 5 shades from dark to pale brown (center is "Natural Wood")  
- **Banner Colors**: 6 vibrant colors (black, white, red, blue, green, yellow)
- **Color Values**: Use Color components > 1.0 for brightness (HDR-like effect)

#### Painting Mallet Tool (torvalds-painters.cs:116-174)
- **Proper CustomItem**: Uses Jötunn's CustomItem approach with Hammer cloning for visuals
- **Tool Stance**: Links to empty CustomPieceTable for proper tool behavior (no punch/block animations)
- **Input System**: Left-click paints, right-click opens color palette GUI
- **Recipe**: Wood(10) + LeatherScraps(5) + Coal(1)

#### Color Application (torvalds-painters.cs:210-232)
- **MaterialPropertyBlock**: Preserves original textures while tinting
- **ZDO Persistence**: Stores color data in `TorvaldsPainters.Color` ZDO field
- **Network Sync**: Colors persist and sync across multiplayer sessions

#### Restoration System (torvalds-painters.cs:352-372)
- **Piece.Awake Patch**: Restores painted colors when pieces load
- **Automatic**: No manual intervention needed for color persistence

### Project Structure
- **Main Plugin**: `torvalds-painters/torvalds-painters.cs` - All mod logic
- **Unity Assets**: `JotunnModStubUnity/` - For creating custom assets (optional)
- **Packaging**: `torvalds-painters/Package/` - ThunderStore release structure
- **Assembly References**: Managed via `Environment.props` and MSBuild targets

### Dependencies
- **.NET Framework 4.8**: Target framework
- **JotunnLib 2.24.3**: Core modding framework
- **BepInEx**: Valheim modding platform
- **Harmony**: Runtime method patching

### Important Design Decisions
- **Proper Tool Implementation**: Uses Jötunn CustomItem + empty PieceTable for authentic tool stance
- **Native GUI**: Uses Jötunn's wood panel system for color picker that matches game aesthetics
- **Input Manager Integration**: Proper input handling with KeyHints instead of raw Input calls
- **MaterialPropertyBlock**: Avoids material cloning issues while preserving textures
- **Minimal Patches**: Only patches Piece.Awake for color restoration
- **HDR Colors**: Uses values > 1.0 for vibrant appearance in game's lighting

### New Features Added
- **Native Color Picker GUI**: Wood panel with 11 color buttons in grid layout
- **Proper Tool Stance**: No more weapon animations when using the mallet
- **KeyHints Integration**: HUD shows proper controls when mallet is equipped
- **Localization Support**: All text uses tokens for translation support

## Development Notes
- The mod uses a streamlined approach compared to typical Jötunn mods
- Color values are carefully calibrated - avoid changing the `VikingColors` dictionary
- The painting system works without visual feedback to prevent color corruption issues
- Post-build events handle deployment automatically via PowerShell scripts