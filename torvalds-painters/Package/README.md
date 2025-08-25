# Torvald's Affordable Painters
*"Hammer some paint onto your creations!"*

A Valheim mod that lets you paint building pieces with a variety of colors. Torvald's got you covered with his trusty painting mallet - craft it, equip it, and start adding some color to those drab wooden halls.

Now, before you start thinking old Torvald's going to let you paint your longhouse hot pink or neon green - think again! I'm a respectable craftsman, not some crazed alchemist. My colors are carefully chosen to complement what the gods have already blessed us with in this realm. You'll find wood tones that actually look like *wood*, and paint colors that won't make the neighbors think you've been sampling too much fermented honey. This isn't about turning Valheim into some garish carnival - it's about giving your builds the subtle character they deserve, using colors that actually belong in a Viking's world.

![a painted barn](https://raw.githubusercontent.com/carloseberhardt/torvalds-painters/refs/heads/main/torvalds-painters/screen.png)

## Features

- Paint building pieces with 13 different colors (5 wood tones, 8 paint colors)
- Custom paint-splattered mallet icon to easily distinguish from regular hammer
- Left-click to paint, right-click to open color selector
- Colors persist across game sessions and work in multiplayer
- Configurable recipe system - server admins can change what materials are required
- All colors can be customized through the config file
- Requires workbench by default (can be disabled in config)

## How to Use

1. Craft Torvald's Painting Mallet at a workbench (default recipe: 10 Wood, 5 Leather Scraps, 1 Coal)
2. Equip it like any other tool - no more swinging swords at your walls
3. Left-click on building pieces to give them a proper paint job
4. Right-click to browse Torvald's color selection and pick your favorite

## Configuration

After running the mod once, you'll find a config file at `BepInEx/config/com.torvald.painters.cfg`.

### Recipe Configuration

```ini
[Recipe]
# Materials needed to craft the painting mallet
Materials = Wood:10,LeatherScraps:5,Coal:1
# Whether the mallet requires a workbench to craft
RequireWorkbench = true
```

**Recipe Examples:**
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

### Color Customization

All 13 colors are individually configurable using RGB values (0.0 to 3.0):

```ini
[Colors.WoodTones]
DarkBrown = 0.65,0.35,0.20
MediumBrown = 0.80,0.60,0.45
NaturalWood = 1.0,1.0,1.0
LightBrown = 1.15,1.05,0.90
PaleWood = 1.30,1.15,1.00

[Colors.PaintColors]
Black = 0.1,0.1,0.1
White = 2.0,2.0,2.0
Red = 1.5,0.2,0.2
Blue = 0.25,0.35,1.40
Green = 0.30,1.30,0.30
Yellow = 1.60,1.40,0.25
Orange = 1.5,0.9,0.25
Purple = 1.2,0.5,1.4
```

### Advanced Filtering Options

Control what objects can be painted:

```ini
[Filtering]
# Building categories that can be painted (flags: Building, Furniture, Crafting, Misc, All)
AllowedCategories = Building, Furniture
# Functional groups to exclude (flags: Stations, Production, Storage, Beds, PortalsWard, Transport, etc.)
ExcludedFunctional = Stations, Production, Storage, Beds, PortalsWard, Transport
# Material types that can be painted (flags: Wood, Stone, Metal, Marble, All)
AllowedMaterials = Wood, Stone, Marble
# Specific prefabs to always allow (comma-separated)
WhitelistPrefabs = 
# Specific prefabs to always block (comma-separated, overrides whitelist)
BlacklistPrefabs = 
```

### Debug Options

```ini
[Debug]
# Logging level (None=0, Basic=1, Detailed=2, Debug=3)
LogLevel = Basic
# Maximum painting distance in meters
MaxPaintDistance = 8.0
# Require build permission to paint objects
RequireBuildPermission = true
```

**Note:** You need to restart the game for config changes to take effect.

## Server Administrators

Torvald's business model is flexible - want to make his services more exclusive? Just change the recipe to require fancier materials. Gate it behind silver and black metal if you want painting to be a proper end-game luxury. The recipe system accepts any valid Valheim item names, so get creative.

## Installation

Install with a mod manager or manually place the files in your BepInEx/plugins folder. Requires BepInEx and Jotunn.

