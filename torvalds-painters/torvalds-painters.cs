using BepInEx;
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
        public const string PluginVersion = "0.1.0";
        
        // Use this class to add your own localization to the game
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
        
        // Harmony instance for patches
        private Harmony harmony;
        
        // Color values for painting - these work perfectly for the painting system
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
            
            // Initialize Harmony
            harmony = new Harmony(PluginGUID);
            harmony.PatchAll();
            
            // Hook into Jotunn events for custom items
            PrefabManager.OnVanillaPrefabsAvailable += AddCustomItems;
            
            Jotunn.Logger.LogInfo("🎨 Torvald's Affordable Painters ready for business!");
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
                Name = "Torvald's Painting Mallet",
                Description = "Left-click to smack on the paint, right-click to select paint color",
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
            // Create ItemConfig with recipe
            var itemConfig = new ItemConfig
            {
                Name = "$item_painting_mallet",
                Description = "$item_painting_mallet_desc"
            };
            itemConfig.AddRequirement(new RequirementConfig("Wood", 10));
            itemConfig.AddRequirement(new RequirementConfig("LeatherScraps", 5));
            itemConfig.AddRequirement(new RequirementConfig("Coal", 1));
            
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
                {"item_painting_mallet_desc", "A fine mallet for painting building pieces! Left-click to paint, right-click to select color."},
                {"paint_apply_hint", "Apply Paint"},
                {"paint_select_color_hint", "Select Color"},
                {"paint_applied", "🎨 Painted with"},
                {"paint_cannot_paint", "❌ Cannot paint this object"}
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
            
            // Create main panel using relative sizing (percentage of screen)
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float panelWidth = Mathf.Min(screenWidth * 0.4f, 600f); // 40% of screen width, max 600px
            float panelHeight = Mathf.Min(screenHeight * 0.5f, 450f); // 50% of screen height, max 450px
            
            colorPickerPanel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                width: panelWidth,
                height: panelHeight,
                draggable: false);
            
            // Add title with native Valheim styling
            var titleText = GUIManager.Instance.CreateText(
                text: "Select Paint Color",
                parent: colorPickerPanel.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: new Vector2(0f, -panelHeight * 0.08f), // 8% from top
                font: GUIManager.Instance.Norse,
                fontSize: Mathf.RoundToInt(panelHeight * 0.05f), // 5% of panel height
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: panelWidth * 0.8f, // 80% of panel width
                height: panelHeight * 0.08f, // 8% of panel height
                addContentSizeFitter: false);
            
            // Apply native text styling
            GUIManager.Instance.ApplyTextStyle(titleText.GetComponent<Text>(), GUIManager.Instance.ValheimOrange);
            
            // Create color buttons in responsive grid layout based on panel size
            int index = 0;
            int columns = 4;
            int rows = 3;
            
            // Calculate sizes based on panel dimensions for responsiveness
            float availableWidth = panelWidth * 0.85f; // Use 85% of panel width
            float availableHeight = (panelHeight - 120f) * 0.8f; // Reserve space for title and close button
            float buttonSize = Mathf.Min(availableWidth / (columns + 1), availableHeight / (rows + 1)) * 0.6f;
            float spacingX = (availableWidth - (columns * buttonSize)) / (columns + 1);
            float spacingY = (availableHeight - (rows * buttonSize)) / (rows + 1);
            
            float startX = -availableWidth / 2f + spacingX + buttonSize / 2f;
            float startY = (availableHeight / 2f) - spacingY - buttonSize / 2f;
            
            foreach (var colorEntry in VikingColors)
            {
                int row = index / columns;
                int col = index % columns;
                float x = startX + col * (buttonSize + spacingX);
                float y = startY - row * (buttonSize + spacingY);
                
                CreateColorButton(colorPickerPanel, colorEntry.Key, colorEntry.Value, new Vector2(x, y), buttonSize);
                index++;
            }
            
            // Add close button with native Valheim styling
            var closeButton = GUIManager.Instance.CreateButton(
                text: "Close",
                parent: colorPickerPanel.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(0f, panelHeight * 0.1f), // 10% from bottom
                width: panelWidth * 0.25f, // 25% of panel width
                height: panelHeight * 0.08f); // 8% of panel height
            
            // Apply native button styling
            GUIManager.Instance.ApplyButtonStyle(closeButton.GetComponent<Button>());
            closeButton.GetComponent<Button>().onClick.AddListener(CloseColorPicker);
            
            // Block player input while GUI is open
            GUIManager.BlockInput(true);
        }
        
        private void CreateColorButton(GameObject parent, string colorName, Color color, Vector2 position, float size)
        {
            // Create button with native Valheim styling
            var button = GUIManager.Instance.CreateButton(
                text: "",
                parent: parent.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: position,
                width: size,
                height: size);
            
            // Apply native button styling
            GUIManager.Instance.ApplyButtonStyle(button.GetComponent<Button>());
            
            // Set button color with improved visibility
            var image = button.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                // Create bright, distinctive display colors for GUI
                Color displayColor;
                switch (colorName)
                {
                    case "Dark Brown":
                        displayColor = new Color(0.5f, 0.35f, 0.2f, 1f); // Rich dark brown
                        break;
                    case "Medium Brown": 
                        displayColor = new Color(0.7f, 0.5f, 0.3f, 1f); // Medium brown
                        break;
                    case "Natural Wood":
                        displayColor = new Color(0.85f, 0.7f, 0.5f, 1f); // Natural wood
                        break;
                    case "Light Brown":
                        displayColor = new Color(0.9f, 0.8f, 0.6f, 1f); // Light brown
                        break;
                    case "Pale Wood":
                        displayColor = new Color(0.95f, 0.9f, 0.8f, 1f); // Pale wood
                        break;
                    case "Black":
                        displayColor = new Color(0.15f, 0.15f, 0.15f, 1f); // Dark but visible black
                        break;
                    case "White":
                        displayColor = new Color(0.95f, 0.95f, 0.95f, 1f); // Clean white
                        break;
                    case "Red":
                        displayColor = new Color(0.8f, 0.2f, 0.2f, 1f); // Vibrant red
                        break;
                    case "Blue":
                        displayColor = new Color(0.2f, 0.4f, 0.8f, 1f); // Clear blue
                        break;
                    case "Green":
                        displayColor = new Color(0.3f, 0.7f, 0.3f, 1f); // Bright green
                        break;
                    case "Yellow":
                        displayColor = new Color(0.9f, 0.8f, 0.2f, 1f); // Bright yellow
                        break;
                    default:
                        // Fallback - brighten any remaining colors
                        displayColor = new Color(
                            Mathf.Clamp01(color.r * 0.8f),
                            Mathf.Clamp01(color.g * 0.8f), 
                            Mathf.Clamp01(color.b * 0.8f),
                            1f);
                        break;
                }
                image.color = displayColor;
            }
            
            // Add click handler
            button.GetComponent<Button>().onClick.AddListener(() => SelectColor(colorName));
            
            // Add text label with native Valheim styling
            var label = GUIManager.Instance.CreateText(
                text: colorName,
                parent: button.transform,
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                position: new Vector2(0f, -size * 0.4f), // Position relative to button size
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: Mathf.RoundToInt(size * 0.2f), // Font size relative to button size
                color: GUIManager.Instance.ValheimBeige,
                outline: true,
                outlineColor: Color.black,
                width: size * 1.5f, // Width relative to button size
                height: size * 0.4f, // Height relative to button size
                addContentSizeFitter: false);
            
            // Apply native text styling
            GUIManager.Instance.ApplyTextStyle(label.GetComponent<Text>(), GUIManager.Instance.ValheimBeige);
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
                Object.Destroy(colorPickerPanel);
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