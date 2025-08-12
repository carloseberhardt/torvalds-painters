# Torvald's Affordable Painters
*"Hammer some paint onto your creations!"*

A Valheim mod that lets you paint building pieces with a variety of colors. Torvald's got you covered with his trusty painting mallet - craft it, equip it, and start adding some color to those drab wooden halls.

## Features

- Paint building pieces with 11 different colors (5 wood tones, 6 paint colors)
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

### Changing the Recipe

You can modify what materials are needed to craft the mallet:

```
# Default recipe
Materials = Wood:10,LeatherScraps:5,Coal:1

# Make it more expensive
Materials = FineWood:8,Bronze:2,TrophyGreydwarf:1

# Late game version
Materials = BlackMetal:1,Silver:2,LoxPelt:1
```

### Custom Colors

Each color can be customized using RGB values (0.0 to 3.0):

```
[Colors.WoodTones]
DarkBrown = 0.6,0.3,0.1
NaturalWood = 1.0,1.0,1.0

[Colors.PaintColors]  
Red = 1.5,0.2,0.2
Blue = 0.2,0.3,1.5
```

Note: You need to restart the game for config changes to take effect.

## Server Administrators

Torvald's business model is flexible - want to make his services more exclusive? Just change the recipe to require fancier materials. Gate it behind silver and black metal if you want painting to be a proper end-game luxury. The recipe system accepts any valid Valheim item names, so get creative.

## Installation

Install with a mod manager or manually place the files in your BepInEx/plugins folder. Requires BepInEx and Jotunn.

## Known Issues

- Config changes require restarting the game
- Very dark building pieces might not show color changes clearly
- Painted pieces briefly revert to normal color when highlighted, but return to painted color afterwards

## Changelog

**v1.0.0**
- Added configurable recipe system
- Added individual color customization
- Added workbench requirement toggle
- Improved color picker interface
- Better config validation and error handling

**v0.1.0**  
- Torvald opened his first paint shop