using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using Jotunn.Configs;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        
        // ORIGINAL PERFECT COLORS - DO NOT CHANGE THESE AGAIN!
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
        
        // Piece highlighting
        private static Piece currentHoveredPiece = null;
        private static Material originalMaterial = null;
        private static Renderer[] originalRenderers = null;
        
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
                CreatePaintingMallet();
                Jotunn.Logger.LogInfo("✅ Custom painting mallet created successfully!");
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"❌ Error creating custom items: {ex.Message}");
            }
        }
        
        private void CreatePaintingMallet()
        {
            // Clone the hammer prefab directly - simple and works
            var hammerPrefab = PrefabManager.Instance.GetPrefab("Hammer");
            if (hammerPrefab == null)
            {
                Jotunn.Logger.LogError("Could not find Hammer prefab!");
                return;
            }
            
            var malletPrefab = PrefabManager.Instance.CreateClonedPrefab("PaintingMallet", hammerPrefab);
            
            // Configure the item as a simple tool (not a building tool)
            var itemDrop = malletPrefab.GetComponent<ItemDrop>();
            itemDrop.m_itemData.m_shared.m_name = "$item_painting_mallet";
            itemDrop.m_itemData.m_shared.m_description = "$item_painting_mallet_desc";
            itemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Tool;
            
            // Set as a building tool like the hammer
            itemDrop.m_itemData.m_shared.m_buildPieces = null; // Will be set by CreatePaintingPieceTable
            
            // Remove PieceTable to avoid build mode conflicts
            itemDrop.m_itemData.m_shared.m_buildPieces = null;
            
            // CRITICAL: Remove all weapon/attack behavior completely
            itemDrop.m_itemData.m_shared.m_attack.m_attackStamina = 0f; // No stamina cost
            itemDrop.m_itemData.m_shared.m_attack.m_attackAnimation = ""; // No attack animation
            itemDrop.m_itemData.m_shared.m_attack.m_attackRange = 0f; // No attack range
            
            // Remove ALL damage so it's not a weapon
            itemDrop.m_itemData.m_shared.m_damages.m_blunt = 0f;
            itemDrop.m_itemData.m_shared.m_damages.m_slash = 0f;
            itemDrop.m_itemData.m_shared.m_damages.m_pierce = 0f;
            itemDrop.m_itemData.m_shared.m_damages.m_fire = 0f;
            itemDrop.m_itemData.m_shared.m_damages.m_frost = 0f;
            itemDrop.m_itemData.m_shared.m_damages.m_lightning = 0f;
            itemDrop.m_itemData.m_shared.m_damages.m_poison = 0f;
            itemDrop.m_itemData.m_shared.m_damages.m_spirit = 0f;
            
            // Also disable blocking
            itemDrop.m_itemData.m_shared.m_blockPower = 0f;
            itemDrop.m_itemData.m_shared.m_deflectionForce = 0f;
            itemDrop.m_itemData.m_shared.m_timedBlockBonus = 1f;
            
            // Tool properties for proper durability system
            itemDrop.m_itemData.m_shared.m_useDurability = true;
            itemDrop.m_itemData.m_shared.m_maxDurability = 200;
            itemDrop.m_itemData.m_shared.m_durabilityPerLevel = 50f;
            itemDrop.m_itemData.m_shared.m_canBeReparied = true;
            itemDrop.m_itemData.m_shared.m_toolTier = 2;
            
            Jotunn.Logger.LogInfo("🎨 Created simple painting mallet tool without PieceTable");
            
            // Customize appearance to distinguish from regular hammer
            CustomizePaintingMalletAppearance(malletPrefab);
            
            // Add custom item with recipe
            var itemConfig = new ItemConfig();
            itemConfig.AddRequirement(new RequirementConfig("Wood", 10));
            itemConfig.AddRequirement(new RequirementConfig("LeatherScraps", 5));
            itemConfig.AddRequirement(new RequirementConfig("Coal", 1)); // For the paint
            
            ItemManager.Instance.AddItem(new CustomItem(malletPrefab, false, itemConfig));
            
            // Add localization
            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                {"item_painting_mallet", "Torvald's Painting Mallet"},
                {"item_painting_mallet_desc", "A fine mallet for painting building pieces! Left-click to paint, right-click to cycle colors."}
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
                
                // Try to customize the icon if possible
                var itemDrop = malletPrefab.GetComponent<ItemDrop>();
                if (itemDrop?.m_itemData?.m_shared?.m_icons?.Length > 0)
                {
                    // Note: Icon customization might require additional asset work
                    Jotunn.Logger.LogInfo("🎨 Found icon to customize");
                }
                
                Jotunn.Logger.LogInfo("🎨 Customized painting mallet appearance with paint color tint");
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"❌ Error customizing mallet appearance: {ex.Message}");
            }
        }
        
        // No longer creating color pieces - using direct tool approach
        
        // Cycle through available colors
        public static void CycleColor(bool forward)
        {
            var colorKeys = new List<string>(VikingColors.Keys);
            if (forward)
                colorIndex = (colorIndex + 1) % colorKeys.Count;
            else
                colorIndex = (colorIndex - 1 + colorKeys.Count) % colorKeys.Count;
                
            currentSelectedColor = colorKeys[colorIndex];
            Jotunn.Logger.LogInfo($"Selected color: {currentSelectedColor}");
        }
        
        // Apply color to building piece while preserving texture
        public static void ApplyColorToPiece(Piece piece, string colorName)
        {
            if (!VikingColors.TryGetValue(colorName, out Color color))
                return;
                
            // Apply color using MaterialPropertyBlock to preserve original texture
            var renderers = piece.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                var materialPropertyBlock = new MaterialPropertyBlock();
                materialPropertyBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(materialPropertyBlock);
            }
            
            // Store color data on the piece for persistence
            var zdo = piece.GetComponent<ZNetView>()?.GetZDO();
            if (zdo != null)
            {
                zdo.Set("TorvaldsPainters.Color", colorName);
            }
            
            Jotunn.Logger.LogInfo($"Applied {colorName} to piece {piece.name}");
        }
        
        // Try to paint the piece the player is looking at (called from input)
        private void TryPaintPiece()
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
                    player.Message(MessageHud.MessageType.TopLeft, $"🎨 Painted with {currentSelectedColor}!");
                    Jotunn.Logger.LogInfo($"🎨 Painted {piece.name} with {currentSelectedColor}");
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "❌ Cannot paint this object");
                }
            }
        }

        // Clear piece highlighting
        private static void ClearHighlight()
        {
            if (currentHoveredPiece != null && originalRenderers != null)
            {
                // Restore original materials
                for (int i = 0; i < originalRenderers.Length; i++)
                {
                    if (originalRenderers[i] != null && originalRenderers[i].materials.Length > 0)
                    {
                        var materials = originalRenderers[i].materials;
                        for (int j = 0; j < materials.Length; j++)
                        {
                            // Reset any highlight effects - this is simplified but should work
                            originalRenderers[i].materials[j].SetFloat("_Emission", 0f);
                        }
                    }
                }
                
                currentHoveredPiece = null;
                originalRenderers = null;
            }
        }
        
        // DISABLED: Highlighting was contaminating all colors by modifying material.color directly!
        // This was the cause of the yellow tint and broken colors
        private static void HighlightPiece(Piece piece)
        {
            // Highlighting disabled until we can implement it properly
            // The issue was that we were modifying material.color directly
            // and never properly restoring the original values
        }
        
        // DISABLED: Hover system was contaminating colors
        private void UpdatePieceHover()
        {
            // Hover highlighting disabled - was causing color contamination
            // The painting still works without visual feedback
        }

        private void Update()
        {
            // Handle mallet interactions
            if (Player.m_localPlayer != null)
            {
                var rightItem = Player.m_localPlayer.GetRightItem();
                if (rightItem != null && rightItem.m_shared.m_name == "$item_painting_mallet")
                {
                    // DISABLED: Update piece hovering and highlighting - was contaminating colors
                    // UpdatePieceHover();
                    
                    // Right-click to cycle colors  
                    if (Input.GetMouseButtonDown(1))
                    {
                        CycleColor(true);
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"🎨 Selected: {currentSelectedColor}");
                    }
                    
                    // Left-click to paint
                    if (Input.GetMouseButtonDown(0))
                    {
                        TryPaintPiece();
                    }
                }
                else
                {
                    // Clear highlight when not using painting mallet
                    ClearHighlight();
                    if (Hud.instance?.m_hoverName != null)
                    {
                        Hud.instance.m_hoverName.gameObject.SetActive(false);
                    }
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
            if (!string.IsNullOrEmpty(colorName) && TorvaldsPainters.VikingColors.TryGetValue(colorName, out Color color))
            {
                var renderers = __instance.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_Color", color);
                    renderer.SetPropertyBlock(block);
                }
            }
        }
    }
}