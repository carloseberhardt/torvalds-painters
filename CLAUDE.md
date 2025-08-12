# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is "Torvald's Affordable Painters" - a Valheim mod built with the Jötunn modding framework. The mod adds a configurable painting system that allows players to color building pieces using a custom painting mallet tool. Features dynamic recipe configuration and customizable colors perfect for server progression balancing.

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

### Main Plugin Class: `TorvaldsPainters` (torvalds-painters.cs:45)
- **BepInEx Plugin**: Entry point with GUID `com.torvald.painters`
- **Jötunn Integration**: Uses PrefabManager and ItemManager for custom content
- **Harmony Patches**: Minimal patching approach for color persistence
- **Configuration System**: Comprehensive BepInEx.Configuration integration

### Core Systems

#### Configuration System (torvalds-painters.cs:58-200)
- **BepInEx.Configuration**: Uses ConfigEntry fields for all settings
- **Dynamic Recipe**: String-based recipe parsing supporting any Valheim materials
- **Color Customization**: RGB configuration for all 11 colors (0.0-3.0 range)
- **Validation**: Material name validation against 50+ known items
- **Error Handling**: Graceful fallbacks with detailed logging
- **Format Examples**:
  - Recipe: `"Wood:10,LeatherScraps:5,Coal:1"`
  - Colors: `"1.5,0.2,0.2"` for RGB values

#### Color System (torvalds-painters.cs:80-100)
- **Configuration-Driven**: `VikingColors` dictionary loaded from BepInEx config with defaults
- **Wood Tones**: 5 shades from dark to pale brown (center is "Natural Wood")  
- **Paint Colors**: 6 vibrant colors (black, white, red, blue, green, yellow)
- **RGB Configuration**: Each color individually configurable via config file (0.0-3.0 range)
- **HDR Support**: Values > 1.0 for vibrant appearance in game's lighting
- **Validation**: Robust parsing with fallback to defaults on invalid values

#### Painting Mallet Tool (torvalds-painters.cs:280-320)
- **Proper CustomItem**: Uses Jötunn's CustomItem approach with Hammer cloning for visuals
- **Tool Stance**: Links to empty CustomPieceTable for proper tool behavior (no punch/block animations)
- **Input System**: Left-click paints, right-click opens color palette GUI
- **Dynamic Recipe**: Configurable materials via string parsing (default: Wood:10,LeatherScraps:5,Coal:1)
- **Workbench Crafting**: Optional workbench requirement (configurable)
- **Material Validation**: Validates against 50+ known Valheim items with graceful fallbacks

#### Color Application (torvalds-painters.cs:250-280)
- **MaterialPropertyBlock**: Preserves original textures while tinting
- **ZDO Persistence**: Stores color data in `TorvaldsPainters.Color` ZDO field
- **Network Sync**: Colors persist and sync across multiplayer sessions

#### Restoration System (Harmony Patches)
- **Piece.Awake Patch**: Restores painted colors when pieces load
- **WearNTear.ResetHighlight Patch**: Re-applies colors after highlight clearing
- **Automatic**: No manual intervention needed for color persistence

### Project Structure
- **Main Plugin**: `torvalds-painters/torvalds-painters.cs` - All mod logic
- **Unity Assets**: `JotunnModStubUnity/` - For creating custom assets (optional)
- **Packaging**: `torvalds-painters/Package/` - ThunderStore release structure
- **Assembly References**: Managed via `Environment.props` and MSBuild targets

### Dependencies
- **.NET Framework 4.8**: Target framework
- **JotunnLib 2.24.3**: Core modding framework
- **BepInEx**: Valheim modding platform with Configuration system
- **Harmony**: Runtime method patching

### Important Design Decisions
- **Configuration-First**: All customization through BepInEx config system
- **Dynamic Recipe Parsing**: Flexible string-based material specification
- **Server Admin Friendly**: Easy progression gating through recipe modification
- **Proper Tool Implementation**: Uses Jötunn CustomItem + empty PieceTable for authentic tool stance
- **Native GUI**: Uses Jötunn's wood panel system for color picker that matches game aesthetics
- **Proper Localization**: Uses Jötunn's translation system with tokens
- **MaterialPropertyBlock**: Avoids material cloning issues while preserving textures
- **Minimal Patches**: Only patches Piece.Awake and WearNTear.ResetHighlight for color restoration
- **HDR Colors**: Uses values > 1.0 for vibrant appearance in game's lighting

### v1.0.0 Features Added
- **BepInEx Configuration**: Comprehensive config system for recipes and colors
- **Dynamic Recipe System**: Parse any "Material:Amount" combinations from config
- **Color Customization**: All 11 colors individually configurable via RGB values
- **Server Admin Tools**: Perfect for progression gating and server theming
- **Config Validation**: Robust parsing with material name validation
- **Workbench Toggle**: Optional workbench crafting requirement
- **Native Color Picker GUI**: Wood panel with 11 color buttons in grid layout
- **Proper Tool Stance**: No more weapon animations when using the mallet
- **KeyHints Integration**: HUD shows proper controls when mallet is equipped
- **Localization Support**: All text uses Jötunn's translation system

## Development Notes
- Color values are now configuration-driven with defaults matching the original carefully calibrated values
- The painting system works without visual feedback to prevent color corruption issues
- Post-build events handle deployment automatically via PowerShell scripts
- Configuration changes require game restart (typical BepInEx pattern)
- Recipe system supports any valid Valheim item names for maximum flexibility
- Material validation includes 50+ common items but allows unknown names for modded content
- Server admins can easily customize progression by editing config file

## Server Administration Examples
```ini
# Early game (default)
Materials = Wood:10,LeatherScraps:5,Coal:1

# Mid-game progression 
Materials = FineWood:8,Bronze:2,TrophyGreydwarf:1

# Late-game luxury
Materials = BlackMetal:1,Silver:2,LoxPelt:1

# Custom themed server
Materials = YggdrasilWood:5,Crystal:3,SurtlingCore:2
```