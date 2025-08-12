# Torvald's Affordable Painters - Development Project

A Valheim mod project built with Jötunn that adds a painting system for building pieces.

## Project Structure

```
├── torvalds-painters/              # Main mod project
│   ├── torvalds-painters.cs        # Primary mod implementation
│   ├── torvalds-painters.csproj    # Project file with dependencies
│   ├── Package/                    # Thunderstore distribution files
│   │   ├── manifest.json           # Mod metadata for Thunderstore
│   │   ├── README.md               # User-facing documentation
│   │   ├── icon.png                # Mod icon (256x256)
│   │   └── plugins/                # Built mod files
│   └── Properties/                 # Assembly info and build configs
├── JotunnModStubUnity/             # Unity project (optional asset creation)
├── Environment.props               # Valheim install path configuration
├── publish.ps1                     # Build automation script
└── CLAUDE.md                       # Development notes and architecture
```

## Building

### Prerequisites
- .NET Framework 4.8
- Valheim installed with BepInEx and Jötunn
- Set `VALHEIM_INSTALL` path in `Environment.props`

### Debug Build
```bash
dotnet build --configuration Debug
```
Automatically deploys to your local Valheim installation.

### Release Build
```bash
dotnet build --configuration Release
```
Creates a Thunderstore-ready package in the `Package/` folder.

## Development Notes

This project uses:
- **BepInEx** for mod loading and configuration
- **Jötunn** framework for Valheim integration
- **Harmony** for minimal runtime patching (color persistence + input handling)
- **Advanced raycasting system** with proper occlusion handling
- **Policy-based filtering** for paintable objects with configurable rules
- **Custom configuration system** for dynamic recipes and colors

Key architectural decisions and recent bug fixes are documented in `CLAUDE.md`.

## The Mod

For user documentation, installation instructions, and mod details, see:
- **User README**: `torvalds-painters/README.md` 
- **Thunderstore**: Search for "Torvald's Affordable Painters"

## License

This project includes the standard license terms in the LICENSE file.