using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using Jotunn.Configs;
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
        
        // Display colors for icons - clamped to 0-1 range for proper visual display
        public static readonly Dictionary<string, Color> DisplayColors = new Dictionary<string, Color>()
        {
            // Brown shades that actually look brown in icons
            {"Dark Brown", new Color(0.4f, 0.2f, 0.1f)}, 
            {"Medium Brown", new Color(0.6f, 0.4f, 0.2f)}, 
            {"Natural Wood", new Color(0.8f, 0.6f, 0.4f)}, 
            {"Light Brown", new Color(0.9f, 0.7f, 0.5f)}, 
            {"Pale Wood", new Color(0.95f, 0.8f, 0.6f)}, 
            
            // Banner colors - clamped to 0-1 for icons
            {"Black", new Color(0.1f, 0.1f, 0.1f)}, 
            {"White", new Color(1.0f, 1.0f, 1.0f)}, 
            {"Red", new Color(0.8f, 0.2f, 0.2f)}, 
            {"Blue", new Color(0.2f, 0.3f, 0.8f)}, 
            {"Green", new Color(0.3f, 0.8f, 0.3f)}, 
            {"Yellow", new Color(0.9f, 0.8f, 0.2f)} 
        };
        
        // Current state
        public static string currentSelectedColor = "Natural Wood";
        public static int colorIndex = 2;
        
        // Piece highlighting completely removed to prevent paint interference
        
        // No GUI needed - using simple cycling

        private void Awake()
        {
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
            // Create piece table with custom Paint Colors category
            var tableConfig = new PieceTableConfig
            {
                UseCategories = false,
                UseCustomCategories = true,
                CustomCategories = new string[] { "PaintColors" },
                CanRemovePieces = false
            };
            
            paintingPieceTable = new CustomPieceTable("_PaintTable", tableConfig);
            PieceManager.Instance.AddPieceTable(paintingPieceTable);
            
            // Create and add color swatch pieces
            CreateColorSwatchPieces();
            
            Jotunn.Logger.LogInfo("🎨 Created paint table with color swatch pieces");
        }
        
        // Create color swatch pieces for the paint table
        private void CreateColorSwatchPieces()
        {
            try
            {
                // Create a simple colored cube prefab for each color
                foreach (var colorEntry in VikingColors)
                {
                    string colorName = colorEntry.Key;
                    Color color = colorEntry.Value;
                    
                    // Create a simple cube prefab programmatically (but disable its colliders/physics)
                    var cubePrefab = CreateColoredCubePrefab(colorName, color);
                    
                    // Create a simple colored icon using display colors
                    var displayColor = DisplayColors.TryGetValue(colorName, out Color dispColor) ? dispColor : color;
                    var icon = CreateColoredIcon(displayColor);
                    
                    // Create piece config - make it non-placeable
                    var pieceConfig = new PieceConfig
                    {
                        Name = $"$paint_swatch_{colorName.ToLower().Replace(" ", "_")}",
                        Description = $"$paint_swatch_{colorName.ToLower().Replace(" ", "_")}_desc",
                        PieceTable = "_PaintTable", 
                        Category = "PaintColors",
                        CraftingStation = "",
                        Icon = icon
                    };
                    
                    // No requirements - these are free "tools" not actual buildable pieces
                    
                    // Create custom piece using our programmatic cube prefab
                    var colorSwatch = new CustomPiece(cubePrefab, false, pieceConfig);
                    
                    // Store the color name for later identification
                    var colorInfo = cubePrefab.AddComponent<ColorSwatchInfo>();
                    colorInfo.colorName = colorName;
                    
                    // Disable the piece's ability to be placed by removing placement requirements
                    var piece = cubePrefab.GetComponent<Piece>();
                    if (piece != null)
                    {
                        piece.m_canBeRemoved = false; // Can't be removed
                        piece.m_groundOnly = false;   // Not restricted to ground
                        piece.m_groundPiece = false;  // Not a ground piece
                        piece.m_allowedInDungeons = false; // Can't be placed in dungeons
                        piece.m_onlyInTeleportArea = false;
                        piece.m_allowRotatedOverlap = false;
                        piece.m_noInWater = true;     // Can't be placed in water
                        piece.m_noClipping = true;    // No clipping allowed - makes it harder to place
                    }
                    
                    PieceManager.Instance.AddPiece(colorSwatch);
                    Jotunn.Logger.LogInfo($"🎨 Created color swatch piece for {colorName}");
                }
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"❌ Error creating color swatch pieces: {ex.Message}");
            }
        }
        
        // Create a simple colored cube prefab programmatically
        private GameObject CreateColoredCubePrefab(string colorName, Color color)
        {
            // Create a cube GameObject
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"ColorSwatch_{colorName.Replace(" ", "_")}";
            
            // Scale it to be small and cube-like
            cube.transform.localScale = Vector3.one * 0.5f;
            
            // Create a colored material using a shader that exists in Valheim
            var material = new Material(Shader.Find("ToonDeferredShading2017"));
            if (material.shader == null)
            {
                // Fallback to a basic shader if ToonDeferredShading2017 not found
                material = new Material(Shader.Find("Legacy Shaders/Diffuse"));
            }
            if (material.shader == null)
            {
                // Last resort fallback
                material = new Material(Shader.Find("Unlit/Color"));
            }
            
            material.color = color;
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            
            // Apply the material
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.material = material;
            
            // Add required Piece component
            var piece = cube.AddComponent<Piece>();
            piece.m_name = $"$paint_swatch_{colorName.ToLower().Replace(" ", "_")}";
            piece.m_description = $"$paint_swatch_{colorName.ToLower().Replace(" ", "_")}_desc";
            
            // Add other required components
            cube.AddComponent<ZNetView>();
            
            // Remove collider since this won't be placed as a real building piece
            var collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            
            // Don't destroy on load
            Object.DontDestroyOnLoad(cube);
            
            return cube;
        }
        
        // Create a simple colored icon sprite
        private Sprite CreateColoredIcon(Color color)
        {
            try
            {
                // Create a small texture with the solid color
                int size = 64; // Icon size
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                
                // Fill the texture with the solid color
                var pixels = new Color[size * size];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = color;
                }
                texture.SetPixels(pixels);
                texture.Apply();
                
                // Create sprite from texture
                var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                
                return sprite;
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"❌ Error creating colored icon: {ex.Message}");
                return null;
            }
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
                {"item_painting_mallet_desc", "A fine mallet for painting building pieces! Left-click to paint, select colors from build menu."},
                {"paint_apply_hint", "Apply Paint"},
                {"paint_applied", "🎨 Painted with"},
                {"paint_cannot_paint", "❌ Cannot paint this object"},
                
                // Color swatch piece names and descriptions
                {"paint_swatch_dark_brown", "Dark Brown Paint"},
                {"paint_swatch_dark_brown_desc", "Select dark brown color for painting"},
                {"paint_swatch_medium_brown", "Medium Brown Paint"},
                {"paint_swatch_medium_brown_desc", "Select medium brown color for painting"},
                {"paint_swatch_natural_wood", "Natural Wood Paint"},
                {"paint_swatch_natural_wood_desc", "Select natural wood color for painting"},
                {"paint_swatch_light_brown", "Light Brown Paint"},
                {"paint_swatch_light_brown_desc", "Select light brown color for painting"},
                {"paint_swatch_pale_wood", "Pale Wood Paint"},
                {"paint_swatch_pale_wood_desc", "Select pale wood color for painting"},
                {"paint_swatch_black", "Black Paint"},
                {"paint_swatch_black_desc", "Select black color for painting"},
                {"paint_swatch_white", "White Paint"},
                {"paint_swatch_white_desc", "Select white color for painting"},
                {"paint_swatch_red", "Red Paint"},
                {"paint_swatch_red_desc", "Select red color for painting"},
                {"paint_swatch_blue", "Blue Paint"},
                {"paint_swatch_blue_desc", "Select blue color for painting"},
                {"paint_swatch_green", "Green Paint"},
                {"paint_swatch_green_desc", "Select green color for painting"},
                {"paint_swatch_yellow", "Yellow Paint"},
                {"paint_swatch_yellow_desc", "Select yellow color for painting"},
                
                // Custom category name
                {"PaintColors", "Paint Colors"}
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
            // Add key hints for the mallet - only paint hint needed now
            KeyHintManager.Instance.AddKeyHint(new KeyHintConfig 
            {
                Item = "TorvaldsMallet",
                ButtonConfigs = new[] 
                {
                    new ButtonConfig { Name = "Attack", HintToken = "$paint_apply_hint" }
                    // Removed Block/right-click hint since we now use build menu for color selection
                }
            });
            
            Jotunn.Logger.LogInfo("🎨 Registered painting mallet key hints");
        }
        
        // Paint the piece the player is looking at
        private void PaintAtLook()
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
        
        // Color cycling removed - now handled by build menu selection

        // All highlighting code removed to prevent paint interference

        private void Update()
        {
            // Handle mallet interactions using proper input system
            if (Player.m_localPlayer != null && ZInput.instance != null)
            {
                var rightItem = Player.m_localPlayer.GetRightItem();
                if (rightItem != null && rightItem.m_shared.m_name == "$item_painting_mallet")
                {
                    // Check for color swatch selection in build menu
                    CheckForColorSwatchSelection(Player.m_localPlayer);
                    
                    // Check if the player can take input (no menus open, not typing in chat, etc.)
                    if (!Player.m_localPlayer.TakeInput())
                    {
                        return; // Don't process painting input when menus are open
                    }
                    
                    // Left-click to paint using proper input
                    if (ZInput.GetButtonDown("Attack"))
                    {
                        PaintAtLook();
                    }
                    
                    // Right-click color cycling removed - now use build menu to select colors
                }
            }
        }
        
        // Check if a color swatch is currently selected in the build pieces
        private void CheckForColorSwatchSelection(Player player)
        {
            if (player.m_buildPieces != null)
            {
                try
                {
                    // Get the currently selected piece index
                    var selectedPiece = player.m_buildPieces.GetSelectedPiece();
                    if (selectedPiece != null)
                    {
                        var colorInfo = selectedPiece.GetComponent<ColorSwatchInfo>();
                        if (colorInfo != null && colorInfo.colorName != currentSelectedColor)
                        {
                            // A color swatch is selected and it's different from current
                            currentSelectedColor = colorInfo.colorName;
                            
                            // Update color index for consistency
                            var colorKeys = VikingColors.Keys.ToList();
                            colorIndex = colorKeys.IndexOf(colorInfo.colorName);
                            
                            // Show color selection message
                            player.Message(MessageHud.MessageType.Center, $"🎨 Selected: {colorInfo.colorName}");
                            
                            Jotunn.Logger.LogInfo($"🎨 Color selected from build menu: {colorInfo.colorName}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    // Ignore errors - this is just a monitoring function
                    Jotunn.Logger.LogDebug($"Color selection check error: {ex.Message}");
                }
            }
        }
        
        // No need for complex visual copying - using hammer directly

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            PrefabManager.OnVanillaPrefabsAvailable -= AddCustomItems;
        }
    }
    
    // No longer need complex patches - using direct approach
    
    // Using direct approach - no complex patches needed
    
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
    
    // Component to store color information on color swatch pieces
    public class ColorSwatchInfo : MonoBehaviour
    {
        public string colorName;
    }
    
    // Go back to the PlacePiece approach but add logging to see what's happening
    
    // Enhanced PlacePiece patch with better color selection detection
    [HarmonyPatch(typeof(Player), "PlacePiece")]
    public static class PlayerPlacePiecePatch
    {
        static bool Prefix(Player __instance, Piece piece)
        {
            Jotunn.Logger.LogInfo($"🎨 PlacePiece called with piece: {piece?.name ?? "null"}");
            
            // Check if this is a color swatch piece
            var colorInfo = piece?.GetComponent<ColorSwatchInfo>();
            if (colorInfo != null)
            {
                Jotunn.Logger.LogInfo($"🎨 Found color swatch piece: {colorInfo.colorName}");
                
                // This is a color swatch - set the color immediately
                TorvaldsPainters.currentSelectedColor = colorInfo.colorName;
                
                // Update color index for consistency
                var colorKeys = TorvaldsPainters.VikingColors.Keys.ToList();
                TorvaldsPainters.colorIndex = colorKeys.IndexOf(colorInfo.colorName);
                
                // Show color selection message
                __instance.Message(MessageHud.MessageType.Center, $"🎨 Selected: {colorInfo.colorName}");
                
                Jotunn.Logger.LogInfo($"🎨 Successfully selected color: {colorInfo.colorName}");
                
                // Prevent actual piece placement
                return false;
            }
            else
            {
                Jotunn.Logger.LogInfo($"🎨 PlacePiece: Not a color swatch piece");
            }
            
            // Allow normal placement for non-color-swatch pieces
            return true;
        }
    }
    
    // Prevent ghost cubes by intercepting UpdatePlacementGhost
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    public static class PlayerUpdatePlacementGhostPatch
    {
        static bool Prefix(Player __instance)
        {
            // If the placement ghost is a color swatch, destroy it immediately
            if (__instance.m_placementGhost != null)
            {
                var colorInfo = __instance.m_placementGhost.GetComponent<ColorSwatchInfo>();
                if (colorInfo != null)
                {
                    // This is a color swatch ghost - destroy it
                    Object.DestroyImmediate(__instance.m_placementGhost);
                    __instance.m_placementGhost = null;
                    return false; // Skip the normal ghost update
                }
            }
            return true; // Allow normal ghost update for non-color-swatch pieces
        }
    }
    
    // The placement ghost issue should be resolved by clearing m_placementGhost in PlacePiece patch
}