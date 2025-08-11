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
            // Create empty piece table for tool stance
            var tableConfig = new PieceTableConfig
            {
                UseCategories = false,
                UseCustomCategories = true,
                CustomCategories = System.Array.Empty<string>(),
                CanRemovePieces = false
            };
            
            paintingPieceTable = new CustomPieceTable("_PaintTable", tableConfig);
            PieceManager.Instance.AddPieceTable(paintingPieceTable);
            
            Jotunn.Logger.LogInfo("🎨 Created empty piece table for painting mallet");
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
                {"item_painting_mallet_desc", "A fine mallet for painting building pieces! Left-click to paint, right-click to cycle colors."},
                {"paint_apply_hint", "Apply Paint"},
                {"paint_cycle_hint", "Cycle Color"},
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
            // Add key hints for the mallet - no custom input registration needed
            KeyHintManager.Instance.AddKeyHint(new KeyHintConfig 
            {
                Item = "TorvaldsMallet",
                ButtonConfigs = new[] 
                {
                    new ButtonConfig { Name = "Attack", HintToken = "$paint_apply_hint" },
                    new ButtonConfig { Name = "Block", HintToken = "$paint_cycle_hint" }
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
        
        // Cycle to next color
        private void CycleColor()
        {
            var colorKeys = VikingColors.Keys.ToList();
            colorIndex = (colorIndex + 1) % colorKeys.Count;
            currentSelectedColor = colorKeys[colorIndex];
            
            var player = Player.m_localPlayer;
            if (player != null)
            {
                player.Message(MessageHud.MessageType.Center, $"🎨 Selected: {currentSelectedColor}");
            }
            
            Jotunn.Logger.LogInfo($"🎨 Cycled to color: {currentSelectedColor}");
        }

        // All highlighting code removed to prevent paint interference

        private void Update()
        {
            // Handle mallet interactions using proper input system
            if (Player.m_localPlayer != null && ZInput.instance != null)
            {
                var rightItem = Player.m_localPlayer.GetRightItem();
                if (rightItem != null && rightItem.m_shared.m_name == "$item_painting_mallet")
                {
                    // Left-click to paint using proper input
                    if (ZInput.GetButtonDown("Attack"))
                    {
                        PaintAtLook();
                    }
                    
                    // Right-click to cycle colors
                    if (ZInput.GetButtonDown("Block"))
                    {
                        CycleColor();
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