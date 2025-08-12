using BepInEx;
using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using Jotunn.Configs;
using Jotunn.GUI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System;

// MaterialMan extension methods for easier usage
public static class MaterialManExtensions
{
    public static MaterialMan.PropertyContainer GetPropertyContainer(
        this MaterialMan materialManager, GameObject gameObject)
    {
        int instanceId = gameObject.GetInstanceID();
        
        if (!materialManager.m_blocks.TryGetValue(instanceId, out MaterialMan.PropertyContainer propertyContainer))
        {
            gameObject.AddComponent<MaterialManNotifier>();
            propertyContainer = new MaterialMan.PropertyContainer(gameObject, materialManager.m_propertyBlock);
            propertyContainer.MarkDirty += materialManager.QueuePropertyUpdate;
            materialManager.m_blocks.Add(instanceId, propertyContainer);
        }
        
        return propertyContainer;
    }
    
    public static MaterialMan.PropertyContainer SetValue<T>(
        this MaterialMan.PropertyContainer propertyContainer, int propertyId, T value)
    {
        propertyContainer.SetValue(propertyId, value);
        return propertyContainer;
    }
}

namespace TorvaldsPainters
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class TorvaldsPainters : BaseUnityPlugin
    {
        public const string PluginGUID = "com.torvald.painters";
        public const string PluginName = "Torvald's Affordable Painters";
        public const string PluginVersion = "1.0.0";
        
        // Configuration entries
        #region Configuration
        
        // Recipe configuration - dynamic materials
        private static ConfigEntry<string> RecipeConfig;
        private static ConfigEntry<bool> RequireWorkbenchConfig;
        
        // Color configuration - Wood tones
        private static ConfigEntry<string> DarkBrownColorConfig;
        private static ConfigEntry<string> MediumBrownColorConfig;
        private static ConfigEntry<string> NaturalWoodColorConfig;
        private static ConfigEntry<string> LightBrownColorConfig;
        private static ConfigEntry<string> PaleWoodColorConfig;
        
        // Color configuration - Paint colors
        private static ConfigEntry<string> BlackColorConfig;
        private static ConfigEntry<string> WhiteColorConfig;
        private static ConfigEntry<string> RedColorConfig;
        private static ConfigEntry<string> BlueColorConfig;
        private static ConfigEntry<string> GreenColorConfig;
        private static ConfigEntry<string> YellowColorConfig;
        
        #endregion
        
        // Use this class to add your own localization to the game
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
        
        // Harmony instance for patches
        private Harmony harmony;
        
        // Color values for painting - loaded from configuration with defaults
        public static readonly Dictionary<string, Color> VikingColors = new Dictionary<string, Color>()
        {
            // Wood shades - Natural Wood in center, 2 darker, 2 lighter
            {"Dark Brown", new Color(0.6f, 0.3f, 0.1f)}, // Darkest wood
            {"Medium Brown", Color.white}, // MATCHES natural wood perfectly - this was the key!
            {"Natural Wood", new Color(1.0f, 1.0f, 1.0f)}, // Base wood color
            {"Light Brown", new Color(1.3f, 1.1f, 0.9f)}, // Lighter wood
            {"Pale Wood", new Color(1.5f, 1.3f, 1.1f)}, // Lightest wood
            
            // Banner colors that looked perfect
            {"Black", new Color(0.1f, 0.1f, 0.1f)}, // Deep black
            {"White", new Color(2.5f, 2.5f, 2.5f)}, // Bright white
            {"Red", new Color(1.5f, 0.2f, 0.2f)}, // Vibrant red
            {"Blue", new Color(0.2f, 0.3f, 1.5f)}, // True blue  
            {"Green", new Color(0.3f, 1.5f, 0.3f)}, // Vibrant green
            {"Yellow", new Color(1.8f, 1.6f, 0.2f)} // Bright yellow
        };
        
        // Current state
        public static string currentSelectedColor = "Natural Wood";
        public static int colorIndex = 2;
        
        // Cached instance for performance
        public static TorvaldsPainters Instance { get; private set; }
        
        // Piece highlighting completely removed to prevent paint interference
        
        // No GUI needed - using simple cycling

        private void Awake()
        {
            // Set singleton instance for performance
            Instance = this;
            
            Jotunn.Logger.LogInfo("🎨 Torvald's Affordable Painters is loading...");
            
            // Initialize configuration
            InitializeConfiguration();
            
            // Initialize colors from configuration
            InitializeColorsFromConfig();
            
            // Initialize Harmony
            harmony = new Harmony(PluginGUID);
            harmony.PatchAll();
            
            // Hook into Jotunn events for custom items
            PrefabManager.OnVanillaPrefabsAvailable += AddCustomItems;
            
            Jotunn.Logger.LogInfo("🎨 Torvald's Affordable Painters ready for business!");
        }
        
        private void InitializeConfiguration()
        {
            // Recipe configuration section - dynamic materials support
            RecipeConfig = Config.Bind("Recipe", "Materials", "Wood:10,LeatherScraps:5,Coal:1",
                new ConfigDescription("Recipe materials in format: Material1:Amount1,Material2:Amount2,etc.\n" +
                "Examples:\n" +
                "- Early game: \"Wood:10,LeatherScraps:5,Coal:1\"\n" +
                "- Mid game: \"FineWood:8,Bronze:2,LeatherScraps:3\"\n" +
                "- Late game: \"BlackMetal:1,Silver:2,LoxPelt:1\"\n" +
                "Use exact Valheim item names (case sensitive)"));
                
            RequireWorkbenchConfig = Config.Bind("Recipe", "RequireWorkbench", true,
                new ConfigDescription("Whether the painting mallet requires a workbench to craft"));
            
            // Wood tone colors configuration
            DarkBrownColorConfig = Config.Bind("Colors.WoodTones", "DarkBrown", "0.6,0.3,0.1",
                new ConfigDescription("RGB color values for Dark Brown (format: R,G,B with values 0.0-3.0)"));
                
            MediumBrownColorConfig = Config.Bind("Colors.WoodTones", "MediumBrown", "1.0,1.0,1.0",
                new ConfigDescription("RGB color values for Medium Brown (format: R,G,B with values 0.0-3.0)"));
                
            NaturalWoodColorConfig = Config.Bind("Colors.WoodTones", "NaturalWood", "1.0,1.0,1.0",
                new ConfigDescription("RGB color values for Natural Wood (format: R,G,B with values 0.0-3.0)"));
                
            LightBrownColorConfig = Config.Bind("Colors.WoodTones", "LightBrown", "1.3,1.1,0.9",
                new ConfigDescription("RGB color values for Light Brown (format: R,G,B with values 0.0-3.0)"));
                
            PaleWoodColorConfig = Config.Bind("Colors.WoodTones", "PaleWood", "1.5,1.3,1.1",
                new ConfigDescription("RGB color values for Pale Wood (format: R,G,B with values 0.0-3.0)"));
            
            // Paint colors configuration
            BlackColorConfig = Config.Bind("Colors.PaintColors", "Black", "0.1,0.1,0.1",
                new ConfigDescription("RGB color values for Black (format: R,G,B with values 0.0-3.0)"));
                
            WhiteColorConfig = Config.Bind("Colors.PaintColors", "White", "2.5,2.5,2.5",
                new ConfigDescription("RGB color values for White (format: R,G,B with values 0.0-3.0)"));
                
            RedColorConfig = Config.Bind("Colors.PaintColors", "Red", "1.5,0.2,0.2",
                new ConfigDescription("RGB color values for Red (format: R,G,B with values 0.0-3.0)"));
                
            BlueColorConfig = Config.Bind("Colors.PaintColors", "Blue", "0.2,0.3,1.5",
                new ConfigDescription("RGB color values for Blue (format: R,G,B with values 0.0-3.0)"));
                
            GreenColorConfig = Config.Bind("Colors.PaintColors", "Green", "0.3,1.5,0.3",
                new ConfigDescription("RGB color values for Green (format: R,G,B with values 0.0-3.0)"));
                
            YellowColorConfig = Config.Bind("Colors.PaintColors", "Yellow", "1.8,1.6,0.2",
                new ConfigDescription("RGB color values for Yellow (format: R,G,B with values 0.0-3.0)"));
            
            Jotunn.Logger.LogInfo("⚙️ Configuration loaded successfully!");
        }
        
        private void InitializeColorsFromConfig()
        {
            try
            {
                // Parse colors from config and update the VikingColors dictionary
                VikingColors["Dark Brown"] = ParseColorFromConfig(DarkBrownColorConfig.Value, new Color(0.6f, 0.3f, 0.1f));
                VikingColors["Medium Brown"] = ParseColorFromConfig(MediumBrownColorConfig.Value, Color.white);
                VikingColors["Natural Wood"] = ParseColorFromConfig(NaturalWoodColorConfig.Value, new Color(1.0f, 1.0f, 1.0f));
                VikingColors["Light Brown"] = ParseColorFromConfig(LightBrownColorConfig.Value, new Color(1.3f, 1.1f, 0.9f));
                VikingColors["Pale Wood"] = ParseColorFromConfig(PaleWoodColorConfig.Value, new Color(1.5f, 1.3f, 1.1f));
                
                VikingColors["Black"] = ParseColorFromConfig(BlackColorConfig.Value, new Color(0.1f, 0.1f, 0.1f));
                VikingColors["White"] = ParseColorFromConfig(WhiteColorConfig.Value, new Color(2.5f, 2.5f, 2.5f));
                VikingColors["Red"] = ParseColorFromConfig(RedColorConfig.Value, new Color(1.5f, 0.2f, 0.2f));
                VikingColors["Blue"] = ParseColorFromConfig(BlueColorConfig.Value, new Color(0.2f, 0.3f, 1.5f));
                VikingColors["Green"] = ParseColorFromConfig(GreenColorConfig.Value, new Color(0.3f, 1.5f, 0.3f));
                VikingColors["Yellow"] = ParseColorFromConfig(YellowColorConfig.Value, new Color(1.8f, 1.6f, 0.2f));
                
                Jotunn.Logger.LogInfo("🎨 Colors loaded from configuration!");
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"❌ Error loading colors from configuration: {ex.Message}");
            }
        }
        
        private Color ParseColorFromConfig(string colorString, Color defaultColor)
        {
            try
            {
                string[] parts = colorString.Split(',');
                if (parts.Length == 3 && 
                    float.TryParse(parts[0], out float r) && 
                    float.TryParse(parts[1], out float g) && 
                    float.TryParse(parts[2], out float b))
                {
                    // Clamp values to reasonable HDR range (0.0 to 3.0)
                    r = Mathf.Clamp(r, 0f, 3f);
                    g = Mathf.Clamp(g, 0f, 3f);
                    b = Mathf.Clamp(b, 0f, 3f);
                    return new Color(r, g, b);
                }
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"⚠️ Invalid color format '{colorString}', using default: {ex.Message}");
            }
            
            return defaultColor;
        }
        
        private void ApplyRecipeFromConfig(ItemConfig itemConfig)
        {
            try
            {
                string recipeString = RecipeConfig.Value;
                Jotunn.Logger.LogInfo($"📋 Parsing recipe: {recipeString}");
                
                if (string.IsNullOrWhiteSpace(recipeString))
                {
                    Jotunn.Logger.LogWarning("⚠️ Recipe config is empty, using default recipe");
                    ApplyDefaultRecipe(itemConfig);
                    return;
                }
                
                string[] materials = recipeString.Split(',');
                int validMaterials = 0;
                
                foreach (string material in materials)
                {
                    string trimmedMaterial = material.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedMaterial))
                        continue;
                        
                    string[] parts = trimmedMaterial.Split(':');
                    if (parts.Length != 2)
                    {
                        Jotunn.Logger.LogWarning($"⚠️ Invalid recipe format: '{trimmedMaterial}' (expected Material:Amount)");
                        continue;
                    }
                    
                    string materialName = parts[0].Trim();
                    if (!int.TryParse(parts[1].Trim(), out int amount) || amount <= 0)
                    {
                        Jotunn.Logger.LogWarning($"⚠️ Invalid amount for material '{materialName}': '{parts[1]}'");
                        continue;
                    }
                    
                    // Basic validation - check if material name looks reasonable
                    if (ValidateMaterialName(materialName))
                    {
                        itemConfig.AddRequirement(new RequirementConfig(materialName, amount));
                        validMaterials++;
                        Jotunn.Logger.LogInfo($"✅ Added recipe requirement: {materialName} x{amount}");
                    }
                    else
                    {
                        Jotunn.Logger.LogWarning($"⚠️ Potentially invalid material name: '{materialName}' (added anyway - check spelling!)");
                        itemConfig.AddRequirement(new RequirementConfig(materialName, amount));
                        validMaterials++;
                    }
                }
                
                if (validMaterials == 0)
                {
                    Jotunn.Logger.LogWarning("⚠️ No valid materials found in recipe config, using default recipe");
                    ApplyDefaultRecipe(itemConfig);
                }
                else
                {
                    Jotunn.Logger.LogInfo($"🎨 Successfully applied recipe with {validMaterials} materials");
                }
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"❌ Error parsing recipe config: {ex.Message}");
                Jotunn.Logger.LogWarning("🔧 Falling back to default recipe");
                ApplyDefaultRecipe(itemConfig);
            }
        }
        
        private bool ValidateMaterialName(string materialName)
        {
            // Basic validation for common Valheim item naming patterns
            if (string.IsNullOrWhiteSpace(materialName))
                return false;
                
            // Check for reasonable material name patterns
            // This is not exhaustive but catches obvious typos
            string[] commonMaterials = {
                "Wood", "Stone", "Flint", "Coal", "Resin",
                "LeatherScraps", "DeerHide", "BoarHide", "WolfHide", "LoxPelt",
                "FineWood", "CoreWood", "ElderBark", "YggdrasilWood",
                "Copper", "Tin", "Bronze", "Iron", "Silver", "BlackMetal",
                "CopperOre", "TinOre", "IronOre", "SilverOre", "BlackMetalOre",
                "Chitin", "Carapace", "Scale_Hide",
                "Crystal", "Obsidian", "Amber", "AmberPearl",
                "GreydwarfEye", "SurtlingCore", "AncientSeed", "WitheredBone",
                "TrophyDeer", "TrophyBoar", "TrophyNeck", "TrophyGreydwarf",
                "Feathers", "Guck", "Bloodbag", "Ooze",
                "Chain", "Nails", "IronNails",
                "LinenThread", "JuteRed", "JuteBlue"
            };
            
            // Check if it's a known material (case-insensitive)
            foreach (string knownMaterial in commonMaterials)
            {
                if (string.Equals(materialName, knownMaterial, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            
            // If not in our list, it might still be valid (modded items, etc.)
            // Just check that it follows reasonable naming conventions
            return materialName.Length >= 2 && materialName.Length <= 50 && 
                   !materialName.Contains(":") && !materialName.Contains(",");
        }
        
        private void ApplyDefaultRecipe(ItemConfig itemConfig)
        {
            // Default recipe as fallback
            itemConfig.AddRequirement(new RequirementConfig("Wood", 10));
            itemConfig.AddRequirement(new RequirementConfig("LeatherScraps", 5));
            itemConfig.AddRequirement(new RequirementConfig("Coal", 1));
            Jotunn.Logger.LogInfo("🔧 Applied default recipe: Wood x10, LeatherScraps x5, Coal x1");
        }
        
        private void AddCustomItems()
        {
            try
            {
                CreatePaintingMalletTable();
                CreatePaintingMallet();
                RegisterInputs();
                Jotunn.Logger.LogInfo("✅ Custom painting mallet created successfully!");
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"❌ Error creating custom items: {ex.Message}");
            }
        }
        
        // Empty piece table for proper tool stance
        private static CustomPieceTable paintingPieceTable;
        
        private void CreatePaintingMalletTable()
        {
            // Create piece table with default "paint mode" piece
            var tableConfig = new PieceTableConfig
            {
                UseCategories = false,
                UseCustomCategories = false,
                CustomCategories = System.Array.Empty<string>(),
                CanRemovePieces = false
            };
            
            paintingPieceTable = new CustomPieceTable("_PaintTable", tableConfig);
            
            // Add a default "Paint Mode" piece to prevent "Nothing to build" message
            var paintModeConfig = new PieceConfig
            {
                Name = "$item_painting_mallet",
                Description = "$item_painting_mallet_desc",
                PieceTable = "_PaintTable",
                Category = "Misc"
            };
            
            // Use piece_repair as base - it's a tool mode piece with no ghost/cost like hammer repair
            var paintModePiece = new CustomPiece("PaintMode", "piece_repair", paintModeConfig);
            PieceManager.Instance.AddPiece(paintModePiece);
            
            PieceManager.Instance.AddPieceTable(paintingPieceTable);
            
            Jotunn.Logger.LogInfo("🎨 Created piece table with default Paint Mode piece");
        }
        
        private void CreatePaintingMallet()
        {
            // Create ItemConfig with dynamic recipe
            var itemConfig = new ItemConfig
            {
                Name = "$item_painting_mallet",
                Description = "$item_painting_mallet_desc"
            };
            
            // Add recipe requirements from configuration - supports any materials
            ApplyRecipeFromConfig(itemConfig);
            
            // Set crafting station requirement based on configuration
            if (RequireWorkbenchConfig.Value)
            {
                itemConfig.CraftingStation = "piece_workbench";
            }
            
            // Create CustomItem using Jötunn approach - clone hammer for visuals
            var mallet = new CustomItem("TorvaldsMallet", "Hammer", itemConfig);
            ItemManager.Instance.AddItem(mallet);
            
            // Configure as proper tool (not weapon)
            var shared = mallet.ItemDrop.m_itemData.m_shared;
            
            // Link to empty piece table for tool stance
            shared.m_buildPieces = paintingPieceTable.PieceTable;
            
            // Tool behavior - no weapon properties
            shared.m_itemType = ItemDrop.ItemData.ItemType.Tool;
            shared.m_useDurability = true;
            shared.m_maxDurability = 200;
            shared.m_durabilityPerLevel = 50f;
            shared.m_canBeReparied = true;
            shared.m_toolTier = 2;
            
            // Remove ALL weapon behavior
            shared.m_damages = new HitData.DamageTypes(); // Zero damage
            shared.m_blockPower = 0f;
            shared.m_timedBlockBonus = 0f;
            shared.m_deflectionForce = 0f;
            
            // No attack animations
            shared.m_attack.m_attackAnimation = "";
            shared.m_attack.m_attackStamina = 0f;
            shared.m_attack.m_attackRange = 0f;
            shared.m_secondaryAttack.m_attackAnimation = "";
            
            Jotunn.Logger.LogInfo("🎨 Created proper painting mallet tool with empty piece table");
            
            // Customize appearance
            CustomizePaintingMalletAppearance(mallet.ItemPrefab);
            
            // Add localization
            AddLocalization();
        }
        
        private void AddLocalization()
        {
            // Add all localization tokens
            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                {"item_painting_mallet", "Torvald's Painting Mallet"},
                {"item_painting_mallet_desc", "A fine mallet from Torvald's Affordable Painters! Left-click to paint building pieces, right-click to select colors."},
                {"paint_apply_hint", "Apply Paint"},
                {"paint_select_color_hint", "Select Color"},
                {"paint_applied", "🎨 Painted with"},
                {"paint_cannot_paint", "❌ Cannot paint this object"},
                {"paint_color_picker_title", "Select Paint Color"},
                {"paint_wood_stains_section", "Wood Stains"},
                {"paint_colors_section", "Paint Colors"},
                {"paint_close_button", "Close"}
            });
        }
        
        private void CustomizePaintingMalletAppearance(GameObject malletPrefab)
        {
            try
            {
                // Color the painting mallet with a distinctive paint-splattered look
                Color paintColor = new Color(0.8f, 0.6f, 0.4f); // Warm brown/orange paint color
                
                // Find and color all renderers in the prefab
                var renderers = malletPrefab.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer.materials != null)
                    {
                        foreach (var material in renderer.materials)
                        {
                            // Tint the material with paint color
                            material.color = Color.Lerp(material.color, paintColor, 0.3f);
                        }
                    }
                }
                
                Jotunn.Logger.LogInfo("🎨 Customized painting mallet appearance with paint color tint");
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"❌ Error customizing mallet appearance: {ex.Message}");
            }
        }
        
        // Color selection now handled by GUI picker
        
        // Apply color to building piece using MaterialMan for proper persistence
        public static void ApplyColorToPiece(Piece piece, string colorName)
        {
            if (!VikingColors.TryGetValue(colorName, out Color color))
                return;
                
            // Use MaterialMan for proper color application that survives highlighting
            if (MaterialMan.instance != null)
            {
                MaterialMan.instance.GetPropertyContainer(piece.gameObject)
                    .SetValue(ShaderProps._Color, color);
            }
            else
            {
                // Fallback to direct MaterialPropertyBlock if MaterialMan unavailable
                var renderers = piece.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    var materialPropertyBlock = new MaterialPropertyBlock();
                    materialPropertyBlock.SetColor("_Color", color);
                    renderer.SetPropertyBlock(materialPropertyBlock);
                }
            }
            
            // Store color data on the piece for persistence
            var zdo = piece.GetComponent<ZNetView>()?.GetZDO();
            if (zdo != null)
            {
                zdo.Set("TorvaldsPainters.Color", colorName);
            }
            
            Jotunn.Logger.LogInfo($"Applied {colorName} to piece {piece.name}");
        }
        
        // Input handling variables
        
        private void RegisterInputs()
        {
            // Add key hints for the mallet
            KeyHintManager.Instance.AddKeyHint(new KeyHintConfig 
            {
                Item = "TorvaldsMallet",
                ButtonConfigs = new[] 
                {
                    new ButtonConfig { Name = "Attack", HintToken = "$paint_apply_hint" },
                    new ButtonConfig { Name = "Block", HintToken = "$paint_select_color_hint" }
                }
            });
            
            Jotunn.Logger.LogInfo("🎨 Registered painting mallet key hints");
        }
        
        // Paint the piece the player is looking at
        public void PaintAtLook()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            
            // Raycast to find what we're looking at
            var camera = Utils.GetMainCamera();
            if (camera == null) return;
            
            RaycastHit hitInfo;
            if (Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition), out hitInfo, 50f))
            {
                // Look for a Piece component
                var piece = hitInfo.collider.GetComponentInParent<Piece>();
                if (piece != null)
                {
                    ApplyColorToPiece(piece, currentSelectedColor);
                    player.Message(MessageHud.MessageType.TopLeft, $"$paint_applied {currentSelectedColor}!");
                    Jotunn.Logger.LogInfo($"🎨 Painted {piece.name} with {currentSelectedColor}");
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "$paint_cannot_paint");
                }
            }
        }
        
        // All highlighting code removed to prevent paint interference
        
        // Custom color picker GUI
        public GameObject colorPickerPanel;
        
        public void ShowColorPicker()
        {
            // If already open, close it
            if (colorPickerPanel != null)
            {
                CloseColorPicker();
                return;
            }
            
            // Simple fixed-size panel that works
            colorPickerPanel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                width: 500f,
                height: 400f,
                draggable: false);
            
            // Big clear title
            var titleText = GUIManager.Instance.CreateText(
                text: Localization.TryTranslate("$paint_color_picker_title"),
                parent: colorPickerPanel.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: new Vector2(0f, -30f),
                font: GUIManager.Instance.Norse,
                fontSize: 28,
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: 400f,
                height: 40f,
                addContentSizeFitter: false);
            
            // Wood Stains section (left)
            CreateSimpleSection(Localization.TryTranslate("$paint_wood_stains_section"), new string[] {"Dark Brown", "Medium Brown", "Natural Wood", "Light Brown", "Pale Wood"}, -120f);
            
            // Paint Colors section (right) 
            CreateSimpleSection(Localization.TryTranslate("$paint_colors_section"), new string[] {"Black", "White", "Red", "Blue", "Green", "Yellow"}, 120f);
            
            // Close button
            var closeButton = GUIManager.Instance.CreateButton(
                text: Localization.TryTranslate("$paint_close_button"),
                parent: colorPickerPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(0f, 30f),
                width: 100f,
                height: 30f);
            closeButton.GetComponent<Button>().onClick.AddListener(CloseColorPicker);
            
            // Block player input while GUI is open
            GUIManager.BlockInput(true);
        }
        
        private void CreateSimpleSection(string sectionTitle, string[] colorNames, float xOffset)
        {
            // Section header - simple and clear
            var headerText = GUIManager.Instance.CreateText(
                text: sectionTitle,
                parent: colorPickerPanel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(xOffset, 120f),
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: 18,
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: 200f,
                height: 25f,
                addContentSizeFitter: false);
            
            // Create buttons - one per color, reasonably sized
            for (int i = 0; i < colorNames.Length; i++)
            {
                var colorButton = GUIManager.Instance.CreateButton(
                    text: colorNames[i],
                    parent: colorPickerPanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(xOffset, 80f - (i * 35f)),
                    width: 160f, // Reasonable width for text
                    height: 30f);  // Reasonable height
                
                // Add click handler
                string colorName = colorNames[i]; // Capture for closure
                colorButton.GetComponent<Button>().onClick.AddListener(() => SelectColor(colorName));
            }
        }
        
        private void SelectColor(string colorName)
        {
            currentSelectedColor = colorName;
            
            // Update color index for consistency
            var colorKeys = VikingColors.Keys.ToList();
            colorIndex = colorKeys.IndexOf(colorName);
            
            // Show selection message
            var player = Player.m_localPlayer;
            if (player != null)
            {
                player.Message(MessageHud.MessageType.Center, $"🎨 Selected: {colorName}");
            }
            
            Jotunn.Logger.LogInfo($"🎨 Color selected from GUI: {colorName}");
            
            // Close the picker
            CloseColorPicker();
        }
        
        public void CloseColorPicker()
        {
            if (colorPickerPanel != null)
            {
                UnityEngine.Object.Destroy(colorPickerPanel);
                colorPickerPanel = null;
            }
            
            // Restore player input
            GUIManager.BlockInput(false);
        }
        
        // Check for Escape key to close color picker
        private void Update()
        {
            // Only check for Escape when color picker is open
            if (colorPickerPanel != null && ZInput.GetKeyDown(KeyCode.Escape))
            {
                CloseColorPicker();
            }
        }
        
        // No need for complex visual copying - using hammer directly

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            PrefabManager.OnVanillaPrefabsAvailable -= AddCustomItems;
            
            // Clean up GUI if open
            if (colorPickerPanel != null)
            {
                CloseColorPicker();
            }
            
            // Clear singleton instance
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
    
    // True event-driven input handling - intercept game's own input system
    [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
    public static class PlayerAttackInputPatch
    {
        static bool Prefix(Player __instance)
        {
            // Only process for local player who can take input
            if (__instance != Player.m_localPlayer || !__instance.TakeInput())
                return true; // Allow normal processing if not local player or input blocked

            // Check if painting mallet is equipped
            var rightItem = __instance.GetRightItem();
            if (rightItem?.m_shared.m_name != "$item_painting_mallet")
                return true; // Not our tool, allow normal weapon processing

            // Use cached instance instead of expensive FindObjectOfType
            if (TorvaldsPainters.Instance == null) return false;

            // If color picker is open, ignore all input except right-click to close
            if (TorvaldsPainters.Instance.colorPickerPanel != null)
            {
                if (ZInput.GetButtonDown("Block") || ZInput.GetButtonDown("JoyButtonB"))
                {
                    TorvaldsPainters.Instance.CloseColorPicker();
                }
                return false; // Block all other input while GUI is open
            }

            // Handle painting mallet input when no GUI is blocking
            if (ZInput.GetButtonDown("Attack") || ZInput.GetButtonDown("JoyButtonX"))
            {
                TorvaldsPainters.Instance.PaintAtLook();
            }
            else if (ZInput.GetButtonDown("Block") || ZInput.GetButtonDown("JoyButtonB"))
            {
                TorvaldsPainters.Instance.ShowColorPicker();
            }

            return false; // Consume input, prevent weapon animations
        }
    }
    
    // Prevent build GUI from opening when painting mallet is equipped
    [HarmonyPatch(typeof(Player), "UpdateBuildGuiInput")]
    public static class PlayerUpdateBuildGuiInputPatch
    {
        static bool Prefix(Player __instance)
        {
            // Check if painting mallet is equipped
            var rightItem = __instance.GetRightItem();
            if (rightItem?.m_shared.m_name == "$item_painting_mallet")
            {
                // Block build GUI updates when our mallet is equipped
                return false;
            }
            
            return true; // Allow normal build GUI for other tools
        }
    }
    
    // Restore painted colors when pieces are loaded
    [HarmonyPatch(typeof(Piece), "Awake")]
    public static class PieceAwakePatch
    {
        static void Postfix(Piece __instance)
        {
            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;
            
            string colorName = nview.GetZDO().GetString("TorvaldsPainters.Color", "");
            if (!string.IsNullOrEmpty(colorName))
            {
                TorvaldsPainters.ApplyColorToPiece(__instance, colorName);
            }
        }
    }
    
    // Restore painted colors after vanilla highlight system clears them
    [HarmonyPatch(typeof(WearNTear), "ResetHighlight")]
    public static class WearNTearResetHighlightPatch
    {
        static void Postfix(WearNTear __instance)
        {
            var piece = __instance.GetComponent<Piece>();
            var nview = __instance.GetComponent<ZNetView>();
            
            if (piece && nview?.IsValid() == true)
            {
                string colorName = nview.GetZDO().GetString("TorvaldsPainters.Color", "");
                if (!string.IsNullOrEmpty(colorName))
                {
                    TorvaldsPainters.ApplyColorToPiece(piece, colorName);
                }
            }
        }
    }
}