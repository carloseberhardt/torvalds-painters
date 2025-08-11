# Valheim “Painting Mallet” — Turning a weapon hack into a proper tool

> Goal: Ship a **real tool** (not a weapon) that lets players paint existing build pieces with a clean, Valheim-native UX.

## What “good” looks like
- Equipping the **Painting Mallet** enters a **tool stance** (no unarmed punch/weapon block).
- **LMB / Attack**: apply paint to the pointed piece.
- **RMB / Block**: open a **palette UI** (ColorPicker) or a compact preset palette.
- **Scroll (or Q/E)**: cycle colors with **KeyHints** that show the bindings.
- Optional: an **eyedropper** bind to read the color from the pointed piece.
- Multiplayer-safe: paint operations sync to all clients.

---

## High-level approach

1) Implement the mallet as a **Jötunn CustomItem** that visually clones the hammer, but behaves like a tool.  
2) Attach an **empty CustomPieceTable** to the item to enter tool/build mode without showing vanilla build categories.  
3) Handle inputs with **Jötunn InputManager** + **KeyHints** and route LMB/RMB/Scroll to your painting logic (no combat path).  
4) Use **Jötunn GUIManager.CreateColorPicker** for a palette popup.  
5) Sync paint operations via **Jötunn RPCs** (or ZRoutedRpc if you prefer).  
6) Add niceties (icon rendering, localization, config).

---

## Detailed plan

### 1) Create the tool as a **CustomItem** (clone the hammer visuals)
Use Jötunn’s `ItemManager` to add a **CustomItem** when vanilla prefabs are available (so cloning works). Then adjust the item’s `ItemDrop.m_itemData.m_shared` to behave like a tool: no durability, no damage, no default attacks.

```csharp
// in Awake()
PrefabManager.OnVanillaPrefabsAvailable += () =>
{
    var cfg = new ItemConfig {
        Name = "$item_torvalds_mallet",
        Description = "$item_torvalds_mallet_desc"
    };

    // Clone visuals/pose from the vanilla Hammer
    var mallet = new CustomItem("TorvaldsMallet", "Hammer", cfg);
    ItemManager.Instance.AddItem(mallet);

    var shared = mallet.ItemDrop.m_itemData.m_shared;

    // Tool-like behavior (adjust as you prefer)
    shared.m_useDurability = false;
    shared.m_damages = new HitData.DamageTypes(); // zero damage
    shared.m_blockPower = 0f;
    shared.m_timedBlockBonus = 0f;

    // Optional: ensure no baked-in attack motion
    shared.m_attack.m_attackAnimation = "";
    shared.m_secondaryAttack.m_attackAnimation = "";
};
```
**Why**: Jötunn’s **Items tutorial** shows cloning vanilla items on `OnVanillaPrefabsAvailable` and modifying their SharedData. This is the cleanest way to get the hammer look/stance without bringing weapon behavior along. See references: Items tutorial; Step-by-Step Guide (ModStub + events).

### 2) Enter tool stance via an **empty CustomPieceTable**
Valheim ties “build/tool mode” to the **PieceTable** assigned to an item (e.g., hammer, hoe). Create a **CustomPieceTable** with no pieces and categories disabled, then link it to the mallet’s `m_buildPieces`. The result feels like a real tool (no combat), but there’s nothing to place—perfect for a painter.

```csharp
// Create an empty piece table
var tableCfg = new PieceTableConfig
{
    UseCategories = false,
    UseCustomCategories = true,
    CustomCategories = System.Array.Empty<string>(),
    CanRemovePieces = false
};

var table = new CustomPieceTable(yourBundleWithEmptyTablePrefab, "_PaintTable", tableCfg);
PieceManager.Instance.AddPieceTable(table);

// Link tool -> piece table
mallet.ItemDrop.m_itemData.m_shared.m_buildPieces = table.PieceTable;
```
**Why**: Jötunn’s **Pieces & PieceTables** explains how tables connect tools and pieces, and shows configuring custom tables (including disabling categories). Linking your item to a piece table switches the player into the build/tool input path when equipped.

### 3) Handle inputs cleanly with **InputManager** + **KeyHints**
Register named inputs for paint and color cycling (and an eyedropper), and add **KeyHints** so the HUD tells players what LMB/RMB/Scroll do while the mallet is equipped. Poll inputs only when your tool is in the right hand.

```csharp
// Define buttons (you can back these with ConfigEntries so users can rebind)
ButtonConfig PaintBtn, NextColorBtn, PrevColorBtn, PickBtn;

void RegisterInputs()
{
    PaintBtn = new ButtonConfig { Name = "Paint_Apply", HintToken = "$paint_apply" };
    NextColorBtn = new ButtonConfig { Name = "Paint_Next", HintToken = "$paint_next" };
    PrevColorBtn = new ButtonConfig { Name = "Paint_Prev", HintToken = "$paint_prev" };
    PickBtn  = new ButtonConfig { Name = "Paint_Pick", HintToken = "$paint_pick" };

    InputManager.Instance.AddButton(PluginGUID, PaintBtn);
    InputManager.Instance.AddButton(PluginGUID, NextColorBtn);
    InputManager.Instance.AddButton(PluginGUID, PrevColorBtn);
    InputManager.Instance.AddButton(PluginGUID, PickBtn);

    // Override default HUD hints while the mallet is equipped
    KeyHintManager.Instance.AddKeyHint(new KeyHintConfig {
        Item = "TorvaldsMallet",
        ButtonConfigs = new [] {
            // Map vanilla “Attack” hint text to your action
            new ButtonConfig { Name = "Attack", HintToken = "$paint_apply" },
            // Show scroll cycling
            new ButtonConfig { Name = "Scroll", Axis = "Mouse ScrollWheel", HintToken = "$paint_cycle" }
        }
    });
}

void Update()
{
    if (ZInput.instance == null) return;
    var player = Player.m_localPlayer;
    if (player == null) return;

    // Only when our tool is equipped
    if (player.m_visEquipment.m_rightItem == "TorvaldsMallet")
    {
        if (ZInput.GetButtonDown("Attack") || ZInput.GetButtonDown(PaintBtn.Name))
            PaintAtLook(player);

        if (ZInput.GetButtonDown("Block"))
            OpenPaletteUI();

        if (ZInput.GetButton("Mouse ScrollWheel") || ZInput.GetButtonDown(NextColorBtn.Name) || ZInput.GetButtonDown(PrevColorBtn.Name))
            CycleColor(ZInput.GetAxis("Mouse ScrollWheel"));
    }
}
```
**Why**: Jötunn’s **Inputs tutorial** shows defining custom buttons, gating logic on the **equipped item name**, and **overriding HUD KeyHints** (including a special “Mouse ScrollWheel” axis). That gives you proper UX without relying on weapon block/punch semantics.

### 4) Palette UI via **GUIManager.CreateColorPicker**
Use the built-in, Valheim-styled color picker and block world input while it’s open.

```csharp
void OpenPaletteUI()
{
    GUIManager.Instance.CreateColorPicker(
        new Vector2(0.5f, 0.5f),  // anchorMin
        new Vector2(0.5f, 0.5f),  // anchorMax
        new Vector2(0.5f, 0.5f),  // pivot
        CurrentColor,
        "$paint_choose_color",
        c => CurrentColor = c,
        _ => GUIManager.BlockInput(false),
        allowAlpha: true
    );
    GUIManager.BlockInput(true);
}
```
**Why**: **GUIManager** exposes `CreateColorPicker(...)` and `BlockInput(true)` so you can open a picker that feels native and temporarily disable player/camera input.

### 5) Painting logic (you already have it)
Refactor your working paint code into a `PaintAtLook(Player p)` method. When LMB or your “Apply” button fires and the mallet is equipped, raycast from the camera to find the targeted piece, then apply the color (e.g., material swap / shader param).

- Keep your current “apply color” routines.  
- Add an **eyedropper** that reads color from the targeted piece and sets `CurrentColor`.

### 6) Multiplayer syncing
If you’re already writing color into a `ZNetView/ZDO` field on the piece and handling `OnSynced`, keep that. Otherwise, add a small **CustomRPC** to send `(ZDOID pieceId, Color color)` to the server and fan out to clients.

```csharp
// at Awake()
private static CustomRPC PaintRPC;
PaintRPC = NetworkManager.Instance.AddRPC("PaintRPC", OnPaintServer, OnPaintClient);

// apply: client -> server
void SendPaintRPC(ZDOID id, Color c) {
    var pkg = new ZPackage();
    pkg.Write(id);
    pkg.Write(c.r); pkg.Write(c.g); pkg.Write(c.b); pkg.Write(c.a);
    PaintRPC.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), pkg);
}
```
**Why**: Jötunn’s **RPC tutorial** wraps Valheim’s `ZRoutedRpc` with a friendlier API (payload slicing/compression and coroutine processing). The modding wiki also has a good ZRoutedRpc intro/reference if you go lower-level.

### 7) Nice-to-haves
- **Runtime icon**: Use **RenderManager** to generate a crisp inventory icon from the hammer clone (nice when you don’t ship a custom sprite).
- **Localization**: Use tokenized strings (`$item_torvalds_mallet`) and Jötunn’s localization tutorial.
- **Packaging**: Start from the **ModStub** and enable **PreBuild** (adds refs & publicized assemblies) and **PostBuild** (copies to `BepInEx/plugins` automatically).

---

## Implementation checklist

- [ ] Scaffold from **Jötunn ModStub**; set `PluginGUID/Name/Version`.  
- [ ] Register **CustomItem** (`TorvaldsMallet`) on `OnVanillaPrefabsAvailable` and adjust `SharedData`.  
- [ ] Create an **empty CustomPieceTable**; link it via `m_buildPieces`.  
- [ ] Add **InputManager** buttons + **KeyHints**; poll inputs only when the mallet is equipped.  
- [ ] Wire **GUIManager.CreateColorPicker** on RMB.  
- [ ] Move your **paint** routine behind `PaintAtLook()`; add an **eyedropper**.  
- [ ] Implement **RPC** sync (or continue ZDO-based sync).  
- [ ] Add **icon**, **localization**, and **config** polish.  

---

## Migration tips from “weapon that paints”
- Remove the weapon/attack hacks; rely on **tool stance** + your own inputs.  
- Don’t overload “Block” to mean “cycle colors”—use **Scroll** or a dedicated bind and put the picker on **RMB**.  
- If you previously cloned the hammer prefab wholesale and fought the combat path, switch to: **clone for visuals + empty piece table + custom inputs**. It avoids the “punch while equipped” issue entirely.

---

## References & further reading

- **Jötunn – Step-by-Step Guide** (ModStub, PreBuild/PostBuild automations)  
  https://valheim-modding.github.io/Jotunn/guides/guide.html

- **Jötunn – Items tutorial** (clone items on `OnVanillaPrefabsAvailable`, modify SharedData)  
  https://valheim-modding.github.io/Jotunn/tutorials/items.html

- **Jötunn – Pieces & PieceTables tutorial** (how tables link tools & pieces, custom tables & categories)  
  https://valheim-modding.github.io/Jotunn/tutorials/pieces.html

- **Jötunn – Inputs tutorial** (custom buttons, gating on equipped item, overriding KeyHints & scroll text)  
  https://valheim-modding.github.io/Jotunn/tutorials/inputs.html

- **Jötunn – GUIManager API: CreateColorPicker + BlockInput**  
  https://valheim-modding.github.io/Jotunn/api/Jotunn.Managers.GUIManager.CreateColorPicker.html  
  https://valheim-modding.github.io/Jotunn/api/Jotunn.Managers.GUIManager.html

- **Jötunn – RPCs tutorial** (CustomRPC, NetworkManager, SendPackage/Initiate)  
  https://valheim-modding.github.io/Jotunn/tutorials/rpcs.html

- **Jötunn – RenderManager** (runtime sprite/icon rendering)  
  https://valheim-modding.github.io/Jotunn/tutorials/renderqueue.html

- **Valheim Modding Wiki – Custom Item & Recipe Creation** (ItemDrop.SharedData overview and workflows)  
  https://github.com/Valheim-Modding/Wiki/wiki/Custom-Item-and-Recipe-Creation

- **Valheim Modding Wiki – RPC Introduction & Example** (ZRoutedRpc modality, registration, ZPackage usage)  
  https://github.com/Valheim-Modding/Wiki/wiki/RPC-Introduction-and-Example
