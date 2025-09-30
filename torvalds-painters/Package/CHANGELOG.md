# Changelog

All notable changes to Torvald's Affordable Painters will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2025-09-29

### Added
- Server synchronization using Jotunn's IsAdminOnly flags for multiplayer consistency
- Runtime configuration reload via SettingChanged event handlers for immediate config updates


### Changed
- Improved icon loading system using Jotunn AssetUtils instead of Unity's Texture2D.LoadImage
- Enhanced cross-platform compatibility for Linux development environments
- **BREAKING:** Updated PluginGUID from "com.torvald.painters" to "cebero.TorvaldsAffordablePainters" for consistency with Thunderstore branding (will create new config file)
- **BREAKING:** Renamed all color configuration entries to use "RGBMultiplier" suffix (e.g., "Black" → "BlackRGBMultiplier") to clarify that these are multiplicative values, not final colors
- Improved default wood tone progression with better visual distinction and proper driftwood gray effect
- Reduced White paint brightness to prevent blown-out appearance

### Fixed
- Resolved CS1705 assembly version conflicts on Linux .NET builds
- Improved configuration system reliability and responsiveness

## [1.2.2] - 2025-08-25

### Fixed
- Fixed critical crash on game startup caused by RPC registration happening too early
- Moved RPC registration to Game.Start for proper initialization timing

## [1.2.1] - 2025-08-25

### Fixed
- Fixed multiplayer synchronization bug where other players wouldn't see painted colors until they reloaded the area
- Added RPC-based color broadcasting for immediate visual updates across all connected clients
- Improved network efficiency with targeted color sync messages

## [1.2.0] - 2025-08-13

### Added
- Custom paint-splattered mallet icon to distinguish it from regular hammer
- Enhanced debugging capabilities with proper Jötunn logging integration

### Changed
- Significantly cleaned up logging - only essential actions logged at normal level
- Improved icon embedding system for better mod distribution

### Improved
- Better performance with throttled color reapplication system

## [1.1.0] - 2025-08-11

### Added
- Orange and Purple paint colors (13 total colors now)
- Enhanced color palette with improved wood tone progression

### Changed
- Balanced paint color brightness to reduce blowout on some materials
- Stone and marble materials now automatically dim slightly for more realistic appearance

### Improved
- Color picker GUI with two-column layout for better organization

## [1.0.1] - 2025-08-10

### Fixed
- Major painting bug where you could accidentally paint objects behind non-paintable pieces
- Improved user feedback when painting is blocked by objects like workbenches or forges

### Improved
- Enhanced raycasting system for more consistent painting behavior

## [1.0.0] - 2025-08-09

### Added
- Configurable recipe system for server progression
- Individual color customization through config file
- Workbench requirement toggle
- Config validation and error handling

### Improved
- Color picker interface with native Valheim styling
- Better localization support

## [0.1.0] - 2025-08-08

### Added
- Initial release - Torvald opened his first paint shop
- Basic painting functionality with 11 colors
- Painting mallet tool
- Color persistence across sessions
- Multiplayer support
