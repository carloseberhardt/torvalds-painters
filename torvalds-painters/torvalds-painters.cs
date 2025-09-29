using BepInEx;
using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using Jotunn.Configs;
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
    // Configuration manager attributes for server sync and GUI display
    public class ConfigurationManagerAttributes
    {
        public bool? IsAdminOnly { get; set; }
        public bool? IsAdvanced { get; set; }
        public int? Order { get; set; }
        public bool? Browsable { get; set; }
        public string Category { get; set; }
        public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer { get; set; }
    }

    // Component for scoped ESC key handling when color picker is open
    public class PickerCloser : MonoBehaviour
    {
        public System.Action OnClose;

        void Update()
        {
            if (ZInput.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        public void Close()
        {
            OnClose?.Invoke();
            GUIManager.BlockInput(false);     // Ensure input is unblocked
            Destroy(gameObject);              // Destroy the panel (and this component)
        }
    }

    // Log level enum for debugging
    public enum LogLevel
    {
        None = 0,     // No debug logging
        Basic = 1,    // Basic operations and errors
        Detailed = 2, // Detailed operations and validation
        Debug = 3     // Everything including raycast details
    }

    // Flag-based enums for the new filtering system
    [Flags]
    public enum BuildCategory
    {
        None = 0,
        Building = 1 << 0,   // walls/floors/roofs/doors/rails
        Furniture = 1 << 1,   // décor, item/armor stands, signs, rugs, banners
        Crafting = 1 << 2,   // stations + extensions
        Misc = 1 << 3,   // portals/wards/carts/boats/markers
        All = Building | Furniture | Crafting | Misc
    }

    [Flags]
    public enum FunctionalBucket
    {
        None = 0,
        Stations = 1 << 0, // CraftingStation + StationExtension
        Production = 1 << 1, // Smelter/Fermenter/CookingStation/Windmill/SpinningWheel/etc.
        Storage = 1 << 2, // Container
        Beds = 1 << 3, // Bed
        PortalsWard = 1 << 4, // TeleportWorld, ward
        Lighting = 1 << 5, // Fireplace/Light
        SignsStands = 1 << 6, // Sign, ItemStand, ArmorStand
        Transport = 1 << 7, // Cart, Ship
        Defenses = 1 << 8, // Ballista/spikes/stakewalls (may require name fallback)
        All = ~0
    }

    [Flags]
    public enum MaterialBucket
    {
        Unknown = 0,
        Wood = 1 << 0,
        Stone = 1 << 1,
        Metal = 1 << 2, // iron/steel/etc.
        Marble = 1 << 3, // black marble (Mistlands)
        All = ~0
    }

    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class TorvaldsPainters : BaseUnityPlugin
    {
        public const string PluginGUID = "com.torvald.painters";
        public const string PluginName = "Torvald's Affordable Painters";
        public const string PluginVersion = "1.3.0";

        // Configuration entries
        #region Configuration

        // Recipe configuration - dynamic materials
        private static ConfigEntry<string> RecipeConfig;
        private static ConfigEntry<bool> RequireWorkbenchConfig;

        // Color configuration - Wood tones
        private static ConfigEntry<Color> DarkBrownColorConfig;
        private static ConfigEntry<Color> MediumBrownColorConfig;
        private static ConfigEntry<Color> NaturalWoodColorConfig;
        private static ConfigEntry<Color> LightBrownColorConfig;
        private static ConfigEntry<Color> PaleWoodColorConfig;

        // Color configuration - Paint colors
        private static ConfigEntry<Color> BlackColorConfig;
        private static ConfigEntry<Color> WhiteColorConfig;
        private static ConfigEntry<Color> RedColorConfig;
        private static ConfigEntry<Color> BlueColorConfig;
        private static ConfigEntry<Color> GreenColorConfig;
        private static ConfigEntry<Color> YellowColorConfig;
        private static ConfigEntry<Color> OrangeColorConfig;
        private static ConfigEntry<Color> PurpleColorConfig;

        // Debug configuration
        private static ConfigEntry<LogLevel> LogLevelConfig;
        private static ConfigEntry<float> MaxPaintDistanceConfig;
        private static ConfigEntry<bool> RequireBuildPermissionConfig;

        // Flag-based filtering configuration
        private static ConfigEntry<BuildCategory> AllowedCategoriesConfig;
        private static ConfigEntry<FunctionalBucket> ExcludedFunctionalConfig;
        private static ConfigEntry<MaterialBucket> AllowedMaterialsConfig;

        // Name override configuration
        private static ConfigEntry<string> WhitelistPrefabsConfig;
        private static ConfigEntry<string> BlacklistPrefabsConfig;

        #endregion

        // Use this class to add your own localization to the game
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        // Harmony instance for patches
        private Harmony harmony;

        // Color values for painting - loaded from configuration
        public static readonly Dictionary<string, Color> VikingColors = new Dictionary<string, Color>();

        // Current state
        public static string currentSelectedColor = "Natural Wood";
        public static int colorIndex = 2;

        // Cached instance for performance
        public static TorvaldsPainters Instance { get; private set; }

        // Throttle cache for highlight-driven re-applies (keyed by instance id)
        internal static readonly Dictionary<int, (string color, float t)> _lastApply = new Dictionary<int, (string color, float t)>();
        internal const float HighlightReapplyMinInterval = 0.25f; // seconds

        // Proper layer mask for building pieces in Valheim
        private static readonly int PaintPieceMask = LayerMask.GetMask(
            "piece",          // Standard placed build pieces
            "piece_nonsolid", // Non-solid child colliders of pieces
            "Default",        // Some pieces use default layer
            "Default_small",  // Smaller default layer objects
            "static_solid"    // Static solid objects that might be paintable
        );

        // Reusable buffer to avoid GC allocations
        private static readonly RaycastHit[] _hitsBuf = new RaycastHit[16];

        // Cached name sets for performance
        private static HashSet<string> _whitelist, _blacklist;

        // Cached category IDs resolved via Jötunn API for robustness across game versions
        private static HashSet<int> _buildingCats;
        private static int _craftingCat, _furnitureCat, _miscCat;
        private static bool _categoriesInitialized = false;

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

            // Hook into Jotunn events for custom items and category initialization
            PrefabManager.OnVanillaPrefabsAvailable += AddCustomItems;
            PrefabManager.OnVanillaPrefabsAvailable += InitializeCategoryMapping;

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
                "Use exact Valheim item names (case sensitive)",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RequireWorkbenchConfig = Config.Bind("Recipe", "RequireWorkbench", true,
                new ConfigDescription("Whether the painting mallet requires a workbench to craft",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Wood tone colors configuration
            DarkBrownColorConfig = Config.Bind("Colors.WoodTones", "DarkBrown", new Color(0.65f, 0.35f, 0.20f),
                new ConfigDescription("Color for Dark Brown wood stain",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            MediumBrownColorConfig = Config.Bind("Colors.WoodTones", "MediumBrown", new Color(0.80f, 0.60f, 0.45f),
                new ConfigDescription("Color for Medium Brown wood stain",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            NaturalWoodColorConfig = Config.Bind("Colors.WoodTones", "NaturalWood", new Color(1.0f, 1.0f, 1.0f),
                new ConfigDescription("Color for Natural Wood (default)",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            LightBrownColorConfig = Config.Bind("Colors.WoodTones", "LightBrown", new Color(1.15f, 1.05f, 0.90f),
                new ConfigDescription("Color for Light Brown wood stain",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            PaleWoodColorConfig = Config.Bind("Colors.WoodTones", "PaleWood", new Color(1.30f, 1.15f, 1.00f),
                new ConfigDescription("Color for Pale Wood stain",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Paint colors configuration
            BlackColorConfig = Config.Bind("Colors.PaintColors", "Black", new Color(0.1f, 0.1f, 0.1f),
                new ConfigDescription("Color for Black paint",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WhiteColorConfig = Config.Bind("Colors.PaintColors", "White", new Color(2.0f, 2.0f, 2.0f),
                new ConfigDescription("Color for White paint",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RedColorConfig = Config.Bind("Colors.PaintColors", "Red", new Color(1.5f, 0.2f, 0.2f),
                new ConfigDescription("Color for Red paint",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            BlueColorConfig = Config.Bind("Colors.PaintColors", "Blue", new Color(0.25f, 0.35f, 1.40f),
                new ConfigDescription("Color for Blue paint",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            GreenColorConfig = Config.Bind("Colors.PaintColors", "Green", new Color(0.30f, 1.30f, 0.30f),
                new ConfigDescription("Color for Green paint",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            YellowColorConfig = Config.Bind("Colors.PaintColors", "Yellow", new Color(1.60f, 1.40f, 0.25f),
                new ConfigDescription("Color for Yellow paint",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            OrangeColorConfig = Config.Bind("Colors.PaintColors", "Orange", new Color(1.5f, 0.9f, 0.25f),
                new ConfigDescription("Color for Orange paint",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            PurpleColorConfig = Config.Bind("Colors.PaintColors", "Purple", new Color(1.2f, 0.5f, 1.4f),
                new ConfigDescription("Color for Purple paint",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Debug configuration section
            LogLevelConfig = Config.Bind("Debug", "LogLevel", LogLevel.Basic,
                new ConfigDescription("Debug logging level: None (0), Basic (1), Detailed (2), Debug (3)\n" +
                "- None: No debug logging\n" +
                "- Basic: Basic operations and errors only\n" +
                "- Detailed: Detailed operations and validation info\n" +
                "- Debug: Everything including raycast details"));

            MaxPaintDistanceConfig = Config.Bind("Debug", "MaxPaintDistance", 8.0f,
                new ConfigDescription("Maximum distance for painting objects (meters)", 
                    new AcceptableValueRange<float>(1f, 20f),
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RequireBuildPermissionConfig = Config.Bind("Debug", "RequireBuildPermission", true,
                new ConfigDescription("Require build permission to paint objects (recommended for multiplayer)",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            // Flag-based filtering configuration section
            AllowedCategoriesConfig = Config.Bind("Filtering", "AllowedCategories",
                BuildCategory.Building | BuildCategory.Furniture,
                new ConfigDescription("Which vanilla hammer categories are paintable.\n" +
                "Valid flags: Building, Furniture, Crafting, Misc, All"));

            ExcludedFunctionalConfig = Config.Bind("Filtering", "ExcludedFunctional",
                FunctionalBucket.Stations | FunctionalBucket.Production |
                FunctionalBucket.Storage | FunctionalBucket.Beds |
                FunctionalBucket.PortalsWard | FunctionalBucket.Transport,
                new ConfigDescription("Functional groups to exclude even if their category is allowed.\n" +
                "Valid flags: Stations, Production, Storage, Beds, PortalsWard, Lighting, SignsStands, Transport, Defenses, All, None"));

            AllowedMaterialsConfig = Config.Bind("Filtering", "AllowedMaterials",
                MaterialBucket.Wood | MaterialBucket.Stone | MaterialBucket.Marble,
                new ConfigDescription("Material families that are paintable.\n" +
                "Valid flags: Wood, Stone, Metal, Marble, All"));

            WhitelistPrefabsConfig = Config.Bind("Filtering", "WhitelistPrefabs", "",
                new ConfigDescription("Comma-separated prefab names to always allow painting.\n" +
                "These override all other filtering rules.\n" +
                "Example: piece_chest,portal_wood"));

            BlacklistPrefabsConfig = Config.Bind("Filtering", "BlacklistPrefabs", "",
                new ConfigDescription("Comma-separated prefab names to always exclude from painting.\n" +
                "These override all other filtering rules (including whitelist).\n" +
                "Example: piece_workbench,piece_forge"));

            Jotunn.Logger.LogInfo("⚙️ Configuration loaded successfully!");

            // Build name sets and setup change handlers
            RebuildNameSets();
            WhitelistPrefabsConfig.SettingChanged += (_, __) => RebuildNameSets();
            BlacklistPrefabsConfig.SettingChanged += (_, __) => RebuildNameSets();
            
            // Add color config change handlers for runtime reloading
            DarkBrownColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            MediumBrownColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            NaturalWoodColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            LightBrownColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            PaleWoodColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            BlackColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            WhiteColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            RedColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            BlueColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            GreenColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            YellowColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            OrangeColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
            PurpleColorConfig.SettingChanged += (_, __) => InitializeColorsFromConfig();
        }

        private void InitializeColorsFromConfig()
        {
            // Load colors from config directly - BepInEx handles Color serialization
            VikingColors["Dark Brown"] = DarkBrownColorConfig.Value;
            VikingColors["Medium Brown"] = MediumBrownColorConfig.Value;
            VikingColors["Natural Wood"] = NaturalWoodColorConfig.Value;
            VikingColors["Light Brown"] = LightBrownColorConfig.Value;
            VikingColors["Pale Wood"] = PaleWoodColorConfig.Value;

            VikingColors["Black"] = BlackColorConfig.Value;
            VikingColors["White"] = WhiteColorConfig.Value;
            VikingColors["Red"] = RedColorConfig.Value;
            VikingColors["Blue"] = BlueColorConfig.Value;
            VikingColors["Green"] = GreenColorConfig.Value;
            VikingColors["Yellow"] = YellowColorConfig.Value;
            VikingColors["Orange"] = OrangeColorConfig.Value;
            VikingColors["Purple"] = PurpleColorConfig.Value;

            Jotunn.Logger.LogInfo("🎨 Colors loaded from configuration!");
        }



        // Name set management for whitelist/blacklist
        private static void RebuildNameSets()
        {
            _whitelist = BuildNameSet(WhitelistPrefabsConfig.Value);
            _blacklist = BuildNameSet(BlacklistPrefabsConfig.Value);
            Jotunn.Logger.LogDebug($"Rebuilt name sets: {_whitelist.Count} whitelist, {_blacklist.Count} blacklist");
        }

        // Initialize category IDs - runtime discovery approach since enum values vary across versions
        private static void InitializeCategoryMapping()
        {
            if (_categoriesInitialized)
                return;

            try
            {
                // Discover all available piece categories at runtime
                var allCategories = System.Enum.GetValues(typeof(Piece.PieceCategory)).Cast<Piece.PieceCategory>().ToList();
                Jotunn.Logger.LogDebug($"Available piece categories: {string.Join(", ", allCategories)}");

                // Map standard categories by name
                _miscCat = GetCategoryByName(allCategories, "Misc");
                _craftingCat = GetCategoryByName(allCategories, "Crafting");
                _furnitureCat = GetCategoryByName(allCategories, "Furniture");

                // Find all building-related categories
                _buildingCats = new HashSet<int>();
                foreach (var category in allCategories)
                {
                    var name = category.ToString();
                    if (name.IndexOf("Building", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.Equals("Building", StringComparison.OrdinalIgnoreCase))
                    {
                        _buildingCats.Add((int)category);
                    }
                }

                // Fallback: if no building categories found, use ordinal 0 (often the first category)
                if (_buildingCats.Count == 0)
                {
                    _buildingCats.Add(0);
                }

                Jotunn.Logger.LogDebug($"Category mapping: Misc={_miscCat}, Crafting={_craftingCat}, Furniture={_furnitureCat}, Building=[{string.Join(",", _buildingCats)}]");
                _categoriesInitialized = true;
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Failed to initialize category mapping: {ex.Message}");
                // Set minimal safe defaults if initialization fails
                _miscCat = 0;
                _craftingCat = 1;
                _furnitureCat = 2;
                _buildingCats = new HashSet<int> { 3 };
                _categoriesInitialized = true;
            }
        }

        // Helper to find category ID by name
        private static int GetCategoryByName(List<Piece.PieceCategory> categories, string targetName)
        {
            foreach (var category in categories)
            {
                if (category.ToString().Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return (int)category;
                }
            }
            // Return 0 as fallback
            return 0;
        }

        private static HashSet<string> BuildNameSet(string csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(csv)) return set;
            foreach (var tok in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                set.Add(tok.Trim());
            return set;
        }

        // Piece classification system
        public struct PieceAttributes
        {
            public string Prefab;               // stable id
            public BuildCategory Category;      // Piece.m_category -> BuildCategory
            public FunctionalBucket Functional; // component presence -> FunctionalBucket
            public MaterialBucket Material;     // WearNTear.m_materialType -> MaterialBucket
            public bool HasRenderer;
            public bool HasValidZNV;
            public bool ComfortGiving;          // piece.m_comfort > 0
        }

        // Get stable prefab name that's consistent across instances
        private static string GetPrefabNameStable(Piece piece)
        {
            var nview = piece.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                try
                {
                    return nview.GetPrefabName();
                }
                catch
                {
                    // Fall through to backup method
                }
            }
            return Utils.GetPrefabName(piece.gameObject); // This strips "(Clone)" suffix
        }

        // Map Valheim piece categories to our BuildCategory flags using runtime category resolution
        private static BuildCategory MapCategory(Piece piece)
        {
            // Ensure categories are initialized
            if (!_categoriesInitialized)
            {
                Jotunn.Logger.LogError("Category mapping not initialized - using fallback");
                return BuildCategory.None;
            }

            // Use the runtime category ID from the piece
            int categoryId = (int)piece.m_category;

            // Check against cached category IDs
            if (_buildingCats.Contains(categoryId))
                return BuildCategory.Building;
            if (categoryId == _craftingCat)
                return BuildCategory.Crafting;
            if (categoryId == _furnitureCat)
                return BuildCategory.Furniture;
            if (categoryId == _miscCat)
                return BuildCategory.Misc;

            // Log unknown categories for debugging
            Jotunn.Logger.LogDebug($"Unknown piece category ID {categoryId} for piece '{piece.name}'");
            return BuildCategory.None;
        }

        // Detect functional components and map to FunctionalBucket flags
        private static FunctionalBucket DetectFunctionalBuckets(Piece piece)
        {
            var go = piece.gameObject;
            FunctionalBucket f = FunctionalBucket.None;

            if (go.GetComponentInChildren<CraftingStation>() || go.GetComponentInChildren<StationExtension>()) f |= FunctionalBucket.Stations;
            if (go.GetComponentInChildren<Smelter>() || go.GetComponentInChildren<Fermenter>() ||
                go.GetComponentInChildren<CookingStation>() || go.GetComponentInChildren<Windmill>()) f |= FunctionalBucket.Production;
            if (go.GetComponentInChildren<Container>()) f |= FunctionalBucket.Storage;
            if (go.GetComponentInChildren<Bed>()) f |= FunctionalBucket.Beds;
            if (go.GetComponentInChildren<TeleportWorld>() || HasWardComponent(go)) f |= FunctionalBucket.PortalsWard;
            if (go.GetComponentInChildren<Fireplace>() || go.GetComponentInChildren<Light>()) f |= FunctionalBucket.Lighting;
            if (go.GetComponentInChildren<Sign>() || go.GetComponentInChildren<ItemStand>() ||
                go.GetComponentInChildren<ArmorStand>()) f |= FunctionalBucket.SignsStands;
            if (go.GetComponentInChildren<Ship>() || go.GetComponentInChildren<Vagon>()) f |= FunctionalBucket.Transport;
            if (MightBeDefense(piece)) f |= FunctionalBucket.Defenses;

            return f;

            bool HasWardComponent(GameObject g)
            {
                // Ward component name can vary by version/mod; adjust if needed
                return g.GetComponentInChildren<PrivateArea>() != null;
            }
            bool MightBeDefense(Piece p)
            {
                var name = GetPrefabNameStable(p);
                return name.IndexOf("stake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("spike", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("ballista", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        // Type-name checking utility for future-proofing against unknown component types
        // This avoids compile-time coupling to rarely used or version-specific component types
        private static bool HasComponentNamed(GameObject go, string typeName)
        {
            var comps = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                var comp = comps[i];
                if (comp != null)
                {
                    var type = comp.GetType();
                    if (type != null && type.Name.Equals(typeName, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        // Map WearNTear material types to our MaterialBucket flags
        private static MaterialBucket MapMaterialBucket(Piece piece)
        {
            var wnt = piece.GetComponent<WearNTear>();
            if (wnt != null)
            {
                switch (wnt.m_materialType.ToString())
                {
                    case "Wood": return MaterialBucket.Wood;
                    case "Stone": return MaterialBucket.Stone;
                    case "Iron": return MaterialBucket.Metal;
                    case "Marble": return MaterialBucket.Marble;     // Mistlands black marble
                    case "BlackMarble": return MaterialBucket.Marble;     // alt name in some versions
                    default: return MaterialBucket.Unknown;
                }
            }
            return MaterialBucket.Unknown;
        }

        // Classify a piece and return all its attributes
        private static PieceAttributes Classify(Piece piece)
        {
            return new PieceAttributes
            {
                Prefab = GetPrefabNameStable(piece),
                Category = MapCategory(piece),
                Functional = DetectFunctionalBuckets(piece),
                Material = MapMaterialBucket(piece),
                HasRenderer = piece.GetComponentInChildren<MeshRenderer>() != null
                           || piece.GetComponentInChildren<SkinnedMeshRenderer>() != null,
                HasValidZNV = piece.GetComponent<ZNetView>()?.IsValid() == true,
                ComfortGiving = piece.m_comfort > 0
            };
        }

        // Policy engine - determines if a piece can be painted based on flags and configuration
        public static bool IsPaintableByPolicy(Piece piece, Vector3 hitPos, out string reason)
        {
            reason = "";

            if (piece == null)
            {
                reason = "Not a building piece";
                return false;
            }

            var attrs = Classify(piece);
            var cats = AllowedCategoriesConfig.Value;
            var funEx = ExcludedFunctionalConfig.Value;
            var mats = AllowedMaterialsConfig.Value;

            Jotunn.Logger.LogDebug($"Policy check: '{attrs.Prefab}' Category={attrs.Category} Functional={attrs.Functional} Material={attrs.Material}");

            // Blacklist hard-stop (overrides everything)
            if (_blacklist.Contains(attrs.Prefab))
            {
                reason = "Excluded by blacklist configuration";
                Jotunn.Logger.LogDebug($"Policy: '{attrs.Prefab}' blacklisted");
                return false;
            }

            // Whitelist beats everything else
            if (_whitelist.Contains(attrs.Prefab))
            {
                Jotunn.Logger.LogDebug($"Policy: '{attrs.Prefab}' whitelisted - allowing");
                return FinalChecks(piece, attrs, hitPos, ref reason);
            }

            // Category allow-list
            if ((cats & attrs.Category) == 0)
            {
                reason = $"Category '{attrs.Category}' not allowed";
                Jotunn.Logger.LogDebug($"Policy: '{attrs.Prefab}' category '{attrs.Category}' not in allowed categories '{cats}'");
                return false;
            }

            // Functional exclusions
            if ((attrs.Functional & funEx) != 0)
            {
                var excludedFunctions = attrs.Functional & funEx;
                reason = $"Functional group excluded: {excludedFunctions}";
                Jotunn.Logger.LogDebug($"Policy: '{attrs.Prefab}' has excluded functional components: {excludedFunctions}");
                return false;
            }

            // Material families (Unknown passes only if allowed)
            if (attrs.Material != MaterialBucket.Unknown && (mats & attrs.Material) == 0)
            {
                reason = $"Material '{attrs.Material}' not allowed";
                Jotunn.Logger.LogDebug($"Policy: '{attrs.Prefab}' material '{attrs.Material}' not in allowed materials '{mats}'");
                return false;
            }

            Jotunn.Logger.LogDebug($"Policy: '{attrs.Prefab}' passed all policy checks");
            return FinalChecks(piece, attrs, hitPos, ref reason);
        }

        // Final technical checks for paintability
        private static bool FinalChecks(Piece piece, PieceAttributes attrs, Vector3 hitPos, ref string reason)
        {
            if (!attrs.HasRenderer)
            {
                reason = "Object has no renderers to paint";
                Jotunn.Logger.LogDebug($"FinalChecks: '{attrs.Prefab}' has no renderers");
                return false;
            }
            if (!attrs.HasValidZNV)
            {
                reason = "Object cannot sync in multiplayer";
                Jotunn.Logger.LogDebug($"FinalChecks: '{attrs.Prefab}' missing/invalid ZNetView");
                return false;
            }
            if (RequireBuildPermissionConfig.Value && !PrivateArea.CheckAccess(hitPos))
            {
                reason = "No build permission for this area";
                Jotunn.Logger.LogDebug($"FinalChecks: '{attrs.Prefab}' denied by PrivateArea at {hitPos}");
                return false;
            }

            Jotunn.Logger.LogDebug($"FinalChecks: '{attrs.Prefab}' passed all final checks");
            return true;
        }


        // Enhanced raycast helper with proper Valheim layer targeting
        private static GameObject GetPaintableObjectAtLook(out string errorMessage, out float distance)
        {
            errorMessage = "";
            distance = 0f;

            var player = Player.m_localPlayer;
            if (!player)
            {
                errorMessage = "No local player found";
                Jotunn.Logger.LogError("GetPaintableObjectAtLook: No local player found");
                return null;
            }

            var camera = Utils.GetMainCamera();
            if (!camera)
            {
                errorMessage = "No main camera found";
                Jotunn.Logger.LogError("GetPaintableObjectAtLook: No main camera found");
                return null;
            }

            // Create ray from camera through mouse position
            var ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            Jotunn.Logger.LogDebug($"GetPaintableObjectAtLook: Ray origin={ray.origin}, direction={ray.direction}");

            float maxDistance = MaxPaintDistanceConfig.Value;

            // Use proper named layer mask for building pieces
            Jotunn.Logger.LogDebug($"GetPaintableObjectAtLook: Using PaintPieceMask for layers: piece, piece_nonsolid, Default, Default_small, static_solid");

            // Use NonAlloc to avoid GC pressure, ignore triggers
            int n = Physics.RaycastNonAlloc(ray, _hitsBuf, maxDistance, PaintPieceMask, QueryTriggerInteraction.Ignore);

            if (n <= 0)
            {
                errorMessage = "No objects found in range";
                Jotunn.Logger.LogDebug($"GetPaintableObjectAtLook: No raycast hits found (maxDistance={maxDistance})");
                return null;
            }

            Jotunn.Logger.LogDebug($"GetPaintableObjectAtLook: Found {n} raycast hits");

            // Sort the slice [0..n) by distance
            System.Array.Sort(_hitsBuf, 0, n, Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

            // Check each hit to find a paintable object
            for (int i = 0; i < n; i++)
            {
                var hit = _hitsBuf[i];
                distance = hit.distance;

                Jotunn.Logger.LogDebug($"GetPaintableObjectAtLook: Hit {i}: object='{hit.collider.name}', distance={hit.distance:F2}m, layer={hit.collider.gameObject.layer} ({LayerMask.LayerToName(hit.collider.gameObject.layer)})");

                // Walk up to find the Piece component (hit might be a child collider)
                var piece = hit.collider.GetComponentInParent<Piece>();
                if (!piece)
                {
                    Jotunn.Logger.LogDebug($"GetPaintableObjectAtLook: Hit {i} '{hit.collider.name}' has no Piece component in parent");
                    continue;
                }

                Jotunn.Logger.LogDebug($"GetPaintableObjectAtLook: Found Piece component on '{piece.name}'");

                // Use new policy engine for validation
                string reason;
                if (IsPaintableByPolicy(piece, hit.point, out reason))
                {
                    Jotunn.Logger.LogDebug($"GetPaintableObjectAtLook: Found paintable piece '{piece.name}' at {hit.distance:F2}m");
                    return piece.gameObject;
                }
                else
                {
                    Jotunn.Logger.LogDebug($"GetPaintableObjectAtLook: Piece '{piece.name}' rejected by policy: {reason}");
                    errorMessage = reason; // Tell user why the front piece can't be painted
                    return null;           // Do not scan behind it - treat as occluder
                }
            }

            // No paintable objects found
            errorMessage = n == 1
                ? "Object cannot be painted"
                : $"None of the {n} objects found can be painted";
            Jotunn.Logger.LogDebug($"GetPaintableObjectAtLook: {errorMessage}");

            return null;
        }

        // Convert technical error messages to user-friendly messages
        private static string GetUserFriendlyErrorMessage(string technicalError)
        {
            if (string.IsNullOrEmpty(technicalError))
                return "$paint_cannot_paint";

            // Convert common technical errors to user-friendly messages
            if (technicalError.Contains("No objects found in range"))
                return "❌ No objects in range to paint";

            if (technicalError.Contains("Not a building piece"))
                return "❌ Can only paint building pieces";

            if (technicalError.Contains("too far"))
                return "❌ Too far away to paint";

            if (technicalError.Contains("No build permission"))
                return "❌ No permission to paint here";

            if (technicalError.Contains("exclusion list"))
                return "❌ This object is excluded from painting";

            if (technicalError.Contains("Functional pieces"))
                return "❌ Cannot paint functional objects";

            if (technicalError.Contains("This type of object cannot be painted"))
                return "❌ This object type cannot be painted";

            if (technicalError.Contains("Object has no renderers"))
                return "❌ Object cannot be painted";

            if (technicalError.Contains("cannot sync in multiplayer"))
                return "❌ Object not compatible with multiplayer painting";

            if (technicalError.Contains("None of the") && technicalError.Contains("objects found can be painted"))
                return "❌ No paintable objects found";

            if (technicalError.StartsWith("Object '") && technicalError.Contains("cannot be painted"))
                return "❌ Selected object cannot be painted";

            // Default fallback
            return "$paint_cannot_paint";
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

            Jotunn.Logger.LogDebug("🎨 Created piece table with default Paint Mode piece");
        }

        private void CreatePaintingMallet()
        {
            // Clone the hammer prefab first (like dye vat does)
            var cloned = PrefabManager.Instance.CreateClonedPrefab("TorvaldsMallet", "Hammer");
            if (!cloned)
            {
                Jotunn.Logger.LogError("Could not clone hammer for painting mallet");
                return;
            }


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

            // Create CustomItem from the pre-tinted cloned prefab
            var mallet = new CustomItem(cloned, true, itemConfig);
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

            Jotunn.Logger.LogDebug("🎨 Created proper painting mallet tool with empty piece table");

            // Customize appearance with embedded icon
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
        
        // RPC handler - applies color locally without touching the ZDO
        internal static void OnRpcSetColor(long sender, ZDOID id, string colorName)
        {
            // Skip on dedicated servers - this is purely visual
            if (ZNet.instance != null && ZNet.instance.IsDedicated())
                return;
                
            // Find the live instance
            GameObject go = null;
            if (ZNetScene.instance != null)
            {
                go = ZNetScene.instance.FindInstance(id);
            }
            
            if (go == null)
            {
                Jotunn.Logger.LogDebug($"TP_SetColor: Instance not found for ZDOID {id}");
                return;
            }
            
            var piece = go.GetComponentInParent<Piece>();
            if (piece == null)
            {
                Jotunn.Logger.LogDebug($"TP_SetColor: No Piece component found on {go.name}");
                return;
            }
            
            // Apply color visually only (persistToZdo: false prevents re-writing ZDO)
            ApplyColorToPiece(piece, colorName, persistToZdo: false, logOnApply: false);
            Jotunn.Logger.LogDebug($"TP_SetColor: Applied {colorName} to {piece.name} from RPC");
        }

        private static Sprite LoadPngSpriteFromFile(string fileName, float ppu = 100f)
        {
            // Use Jotunn's AssetUtils to load sprite from file - avoids Unity ImageConversionModule dependency
            var sprite = Jotunn.Utils.AssetUtils.LoadSpriteFromFile(fileName, new Vector2(0.5f, 0.5f));
            return sprite;
        }

        private void CustomizePaintingMalletAppearance(GameObject malletPrefab)
        {
            var drop = malletPrefab.GetComponent<ItemDrop>();
            if (drop != null)
            {
                var shared = drop.m_itemData.m_shared;

                // Debug: List all embedded resources to find the correct name
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var resources = asm.GetManifestResourceNames();
                // Resource discovery logging
                Jotunn.Logger.LogDebug($"Available embedded resources: {string.Join(", ", resources)}");
                Jotunn.Logger.LogDebug($"Number of resources found: {resources.Length}");

                // Load custom icon from file using Jotunn AssetUtils
                var customIcon = LoadPngSpriteFromFile("torvalds-painters/mallet.png", 100f);
                if (customIcon != null)
                {
                    shared.m_icons = new[] { customIcon };
                    Jotunn.Logger.LogDebug("🎨 Applied embedded custom icon for painting mallet");
                }
                else
                {
                    Jotunn.Logger.LogWarning("⚠️ Embedded icon not found; leaving default hammer icon.");
                }
            }
        }

        // Apply color to building piece; optionally persist and/or log
        public static void ApplyColorToPiece(Piece piece, string colorName, bool persistToZdo = true, bool logOnApply = true)
        {
            if (!VikingColors.TryGetValue(colorName, out Color color))
                return;

            // Apply subtle dimming for stone/marble materials to prevent plasticky appearance
            var wnt = piece.GetComponent<WearNTear>();
            if (wnt && (wnt.m_materialType == WearNTear.MaterialType.Stone ||
                       wnt.m_materialType.ToString().Contains("Marble")))
            {
                color *= 0.9f; // 10% dimming for masonry materials
            }

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

            // Store color data on the piece for persistence (if asked, when changed)
            if (persistToZdo)
            {
                var zdo = piece.GetComponent<ZNetView>()?.GetZDO();
                if (zdo != null)
                {
                    string current = zdo.GetString("TorvaldsPainters.Color", "");
                    if (!string.Equals(current, colorName, StringComparison.Ordinal))
                    {
                        zdo.Set("TorvaldsPainters.Color", colorName);
                    }
                }
            }

            // Log only for real paints or at higher debug levels
            if (logOnApply)
            {
                Jotunn.Logger.LogInfo($"Applied {colorName} to piece {piece.name}");
            }
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

            Jotunn.Logger.LogDebug("🎨 Registered painting mallet key hints");
        }

        // Paint the piece the player is looking at - Enhanced with comprehensive validation
        public void PaintAtLook()
        {
            Jotunn.Logger.LogDebug("PaintAtLook: Paint attempt started");

            var player = Player.m_localPlayer;
            if (player == null)
            {
                Jotunn.Logger.LogError("PaintAtLook: No local player found");
                return;
            }

            // Use enhanced raycast system
            string errorMessage;
            float distance;
            var targetObject = GetPaintableObjectAtLook(out errorMessage, out distance);

            if (targetObject == null)
            {
                // Show specific error message to player
                string userMessage = GetUserFriendlyErrorMessage(errorMessage);
                player.Message(MessageHud.MessageType.TopLeft, userMessage);
                Jotunn.Logger.LogDebug($"PaintAtLook: Failed - {errorMessage}");
                return;
            }

            // Get the piece component (we know it exists from validation)
            var piece = targetObject.GetComponentInParent<Piece>();
            if (piece == null)
            {
                Jotunn.Logger.LogError("PaintAtLook: Object passed validation but has no Piece component - this should not happen");
                player.Message(MessageHud.MessageType.TopLeft, "$paint_cannot_paint");
                return;
            }

            // Apply the color
            try
            {
                ApplyColorToPiece(piece, currentSelectedColor);
                
                // Broadcast color change to all clients for immediate sync
                var znv = piece.GetComponent<ZNetView>();
                if (znv != null && znv.IsValid() && ZRoutedRpc.instance != null)
                {
                    // ZDO already set inside ApplyColorToPiece (persistToZdo:true by default)
                    // Now push an immediate visual update to everyone
                    ZRoutedRpc.instance.InvokeRoutedRPC(
                        ZRoutedRpc.Everybody,
                        "TP_SetColor",
                        znv.GetZDO().m_uid,
                        currentSelectedColor
                    );
                    Jotunn.Logger.LogDebug($"Broadcasted color change for {piece.name} to all clients");
                }

                // Success feedback
                string successMessage = $"🎨 Painted {piece.name} with {currentSelectedColor}!";
                player.Message(MessageHud.MessageType.TopLeft, successMessage);
                Jotunn.Logger.LogInfo($"🎨 Painted '{piece.name}' with '{currentSelectedColor}'");
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"PaintAtLook: Failed to apply color to '{piece.name}': {ex.Message}");
                player.Message(MessageHud.MessageType.TopLeft, "Failed to paint object");
            }
        }


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

            // Wider panel to accommodate two-column paint color layout
            colorPickerPanel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                width: 650f,
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
                width: 550f,
                height: 40f,
                addContentSizeFitter: false);

            // Wood Stains section (left)
            CreateSimpleSection(Localization.TryTranslate("$paint_wood_stains_section"), new string[] { "Dark Brown", "Medium Brown", "Natural Wood", "Light Brown", "Pale Wood" }, -180f);

            // Paint Colors section (right) - two columns
            CreatePaintColorSection(Localization.TryTranslate("$paint_colors_section"), new string[] { "Black", "White", "Red", "Blue", "Green", "Yellow", "Orange", "Purple" }, 80f);

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

            // Add PickerCloser component for scoped ESC key handling
            var pickerCloser = colorPickerPanel.AddComponent<PickerCloser>();
            pickerCloser.OnClose = CloseColorPicker;

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

        private void CreatePaintColorSection(string sectionTitle, string[] colorNames, float xOffset)
        {
            // Section header - centered above the two columns
            var headerText = GUIManager.Instance.CreateText(
                text: sectionTitle,
                parent: colorPickerPanel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(xOffset + 80f, 120f), // Centered between the two columns
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: 18,
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: 200f,
                height: 25f,
                addContentSizeFitter: false);

            // Create buttons in two columns (4 colors each)
            int colorsPerColumn = 4;
            for (int i = 0; i < colorNames.Length; i++)
            {
                int column = i / colorsPerColumn;  // 0 for left column, 1 for right column
                int row = i % colorsPerColumn;     // 0-3 for position within column

                float columnXOffset = (xOffset - 40f) + (column * 160f); // Shift left 40px and 160px between columns
                float yPos = 80f - (row * 35f); // 35px between rows

                var colorButton = GUIManager.Instance.CreateButton(
                    text: colorNames[i],
                    parent: colorPickerPanel.transform,
                    anchorMin: new Vector2(0.5f, 0.5f),
                    anchorMax: new Vector2(0.5f, 0.5f),
                    position: new Vector2(columnXOffset, yPos),
                    width: 150f, // Slightly smaller to fit two columns
                    height: 30f);

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

            Jotunn.Logger.LogDebug($"🎨 Color selected from GUI: {colorName}");

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

    // input handling
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

    // Prevent repair functionality when painting mallet is equipped
    [HarmonyPatch(typeof(Player), "Repair")]
    public static class PlayerRepairPatch
    {
        static bool Prefix(Player __instance)
        {
            // Check if painting mallet is equipped
            var rightItem = __instance.GetRightItem();
            if (rightItem?.m_shared.m_name == "$item_painting_mallet")
            {
                // Block repair functionality when our mallet is equipped
                return false;
            }

            return true; // Allow normal repair for other tools
        }
    }

    // Register RPC when the game is ready
    [HarmonyPatch(typeof(Game), "Start")]
    public static class GameStartPatch
    {
        static void Postfix()
        {
            if (ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.Register<ZDOID, string>("TP_SetColor", TorvaldsPainters.OnRpcSetColor);
                Jotunn.Logger.LogInfo("🔗 Multiplayer color sync ready");
            }
        }
    }

    // Restore painted colors when pieces are loaded
    [HarmonyPatch(typeof(Piece), "Awake")]
    public static class PieceAwakePatch
    {
        static void Postfix(Piece __instance)
        {
            // Purely visual client work; skip on dedicated servers
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            string colorName = nview.GetZDO().GetString("TorvaldsPainters.Color", "");
            if (!string.IsNullOrEmpty(colorName))
            {
                // Restore without touching ZDO or logging
                TorvaldsPainters.ApplyColorToPiece(__instance, colorName, persistToZdo: false, logOnApply: false);
            }
        }
    }

    // Restore painted colors after vanilla highlight system clears them
    [HarmonyPatch(typeof(WearNTear), "ResetHighlight")]
    public static class WearNTearResetHighlightPatch
    {
        static void Postfix(WearNTear __instance)
        {
            // var piece = __instance.GetComponent<Piece>();
            // var nview = __instance.GetComponent<ZNetView>();

            // if (piece && nview?.IsValid() == true)
            // {
            //     string colorName = nview.GetZDO().GetString("TorvaldsPainters.Color", "");
            //     if (!string.IsNullOrEmpty(colorName))
            //     {
            //         TorvaldsPainters.ApplyColorToPiece(piece, colorName);
            //     }
            // }
            // Purely visual client work; skip on dedicated servers
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;

            var piece = __instance.GetComponent<Piece>();
            var nview = __instance.GetComponent<ZNetView>();
            if (!(piece && nview?.IsValid() == true)) return;

            string colorName = nview.GetZDO().GetString("TorvaldsPainters.Color", "");
            if (string.IsNullOrEmpty(colorName)) return;

            // Throttle: avoid reapplying too frequently per piece
            int key = piece.gameObject.GetInstanceID();
            float now = Time.time;
            if (TorvaldsPainters._lastApply.TryGetValue(key, out var entry))
            {
                if (entry.color == colorName && (now - entry.t) < TorvaldsPainters.HighlightReapplyMinInterval)
                {
                    return; // too soon; skip
                }
            }

            TorvaldsPainters.ApplyColorToPiece(piece, colorName, persistToZdo: false, logOnApply: false);
            TorvaldsPainters._lastApply[key] = (colorName, now);
        }
    }
}