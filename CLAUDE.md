# CLAUDE.md — WhatsATincture

## Plugin Context

WhatsATincture is an ExileApi plugin that automates tincture management for any build. It auto-detects equipped tinctures (1-5), monitors the shared burn stack pool, detects nearby enemies, checks player HP, and toggles flask slots on/off using a priority-based N-tincture state machine. It supports Warden linger rotation, non-Warden single/dual setups, and applies smart defaults based on known tincture uniques (e.g., Sap of the Seasons gets HighStacksMode). It renders dynamic HUD status bars (one per tincture) and provides a scenario-adaptive settings UI.

**Main class**: `WhatsATincture` inherits `BaseSettingsPlugin<WhatsATinctureSettings>`
**Settings class**: `WhatsATinctureSettings` implements `ISettings`
**Key classes**: `FlaskSlotInfo` (record — flask belt scan data), `TinctureSlotConfig` (per-slot settings POCO), `TinctureRuntimeState` (per-frame state tracking), `BuildScanResult` (passive tree analysis with scenario classification), `SettingsMenu` (the [Menu]-driven settings renderer, a per-plugin copy of the scaffolding's `new-plugin/templates/SettingsMenu.template.cs` with `__HUD__` = ExileCore), `TinctureSettingsUi` (the custom parts the renderer can't know: status strip + Rescan, per-tincture config cards, scenario-adaptive guide)

### Architecture

1. **Initialise() / AreaChange()** — Calls `AutoDetectTinctures()` which scans the flask belt to find all tinctures, creates/updates `TinctureSlotConfig` entries in `Settings.Tinctures` dictionary (keyed by slot index), applies smart defaults for known tinctures, scans the passive tree for Warden notables, classifies the setup scenario (WardenDual, NonWardenSingle, etc.), and syncs burn buff name.

2. **Tick()** — Generic N-tincture automation loop:
   - Reads active state for all tracked slots via `tincture_parent_buff` + `FlaskSlot`
   - Processes slot transitions with shared cooldown (deactivating ANY tincture puts ALL on cooldown)
   - Reads burn stacks from shared pool, HP, and scans for nearby enemies/uniques
   - One action per tick with priority cascade: HP emergency → stack threshold → activation
   - Deactivation ordered by `DeactivationPriority` (lower first), activation by `ActivationPriority` (lower first)
   - Respects `MaxActiveTinctures` from build scan and per-slot effective thresholds

3. **Render()** — Draws N horizontal status bars (one per configured tincture, sorted by slot index). Each bar shows active state with stack fill (using per-slot effective threshold), cooldown countdown, or "Ready" state. Display names are truncated to fit.

4. **DrawSettings()** — `TinctureSettingsUi.Draw` runs `SettingsMenu.Draw(Settings)`: 5 tabs. Tinctures (root tab: status strip + Rescan via `Tab(..., first: true)`, the read-only Burn Buff row from `[MenuReadOnly]`, one card per detected tincture via `Custom("Tinctures", ...)` with threshold overrides, priority sliders and the HighStacks toggle), Thresholds / Behavior / HUD (generated from the `[Submenu]` classes, labels and tooltips from `[Menu]`, units from `[MenuUnit]`), Guide (`Tab("Guide", ...)`: scenario-adaptive strategy explanation + passive tree checklist).

### Lifecycle Methods Used

| Method | What it does in this plugin |
|---|---|
| `Initialise()` | Ensures nested settings exist, calls AutoDetectTinctures, sets SettingsVersion |
| `AreaChange()` | Resets frame guard, calls AutoDetectTinctures (tinctures may be swapped between zones) |
| `Tick()` | N-tincture automation: buff/transition tracking, enemy detection, priority-based toggle decisions |
| `Render()` | Draws N tincture status bars (one per detected tincture, sorted by slot index) |
| `DrawSettings()` | Custom tabbed ImGui settings UI with per-tincture config cards and scenario-adaptive guide |

### Settings Structure

**Root level:**

| Setting | Type | Description |
|---|---|---|
| Enable | ToggleNode | Master enable |
| BurnBuffName | TextNode | Auto-synced burn buff ID |
| Tinctures | Dictionary\<int, TinctureSlotConfig\> | Auto-populated per-slot configs keyed by flask slot index |
| SettingsVersion | int | Migration marker |

**GlobalThresholds** (`Settings.Thresholds`):

| Setting | Type | Default | Description |
|---|---|---|---|
| StacksNormal | RangeNode\<int\> | 12 (1-100) | Deactivate at this stack count |
| StacksUnique | RangeNode\<int\> | 50 (1-100) | Higher threshold for HighStacks tinctures vs uniques |
| HpMinNormal | RangeNode\<int\> | 30 (0-100) | Emergency deactivation HP% |
| HpMinUnique | RangeNode\<int\> | 60 (0-100) | Boss fight HP% safety |

**BehaviorSettings** (`Settings.Behavior`):

| Setting | Type | Default | Description |
|---|---|---|---|
| ActivationRange | RangeNode\<int\> | 80 (10-200) | Enemy detection range (grid units) |
| ToggleCooldownMs | RangeNode\<int\> | 400 (100-1000) | Min ms between flask key presses |

**HudSettings** (`Settings.Hud`):

| Setting | Type | Default | Description |
|---|---|---|---|
| ShowHud | ToggleNode | true | Show status bars |
| HudX/HudY | RangeNode\<int\> | 500/10 | Bar position |
| BarWidth/BarHeight | RangeNode\<int\> | 220/22 | Bar dimensions |

**TinctureSlotConfig** (per-tincture, auto-populated):

| Field | Type | Default | Description |
|---|---|---|---|
| DisplayName/BaseName/UniqueName | string | "" | Identity from flask scan |
| StackThresholdOverride | int | -1 | Per-slot normal threshold (-1=global) |
| StackThresholdUniqueOverride | int | -1 | Per-slot unique threshold (-1=global) |
| DeactivationPriority | int | 50 | Lower = deactivated first |
| ActivationPriority | int | 50 | Lower = activated first |
| HighStacksMode | bool | false | Use unique threshold vs unique enemies |
| ToggleCooldownMs | RangeNode\<int\> | 400 (100-1000) | Min ms between flask key presses |
| ShowHud | ToggleNode | true | Show status bars |
| HudX | RangeNode\<int\> | 500 (0-3840) | Bar X position |
| HudY | RangeNode\<int\> | 10 (0-2160) | Bar Y position |
| BarWidth | RangeNode\<int\> | 220 (50-600) | Bar width |
| BarHeight | RangeNode\<int\> | 22 (10-60) | Bar height |

## Project Setup

- This is an ExileApi plugin (game HUD overlay framework for Path of Exile)
- Do not edit anything outside this directory
- Target framework: net10.0-windows, OutputType: Library

## Build & Run

- NO manual build command — Loader.exe auto-compiles from Plugins/Source/
- HUD installation path: resolved from .csproj HintPath (parent dir of ExileCore.dll)
- For IDE support set env var: `exapiPackage` = `<HUD installation path>`

## API Reference

- **Default**: HUD installation (from .csproj HintPath) — compiled DLLs with intellisense
- **Enhanced**: If `.claude/override-path` exists, read it for a path to expanded
  API reference with full type definitions and source. Use that path for deep lookups
  when the compiled DLLs don't provide enough detail about a type, method, or pattern.

## Plugin Anatomy

Every plugin is a C# class library. The main class inherits `BaseSettingsPlugin<TSettings>` and the settings class implements `ISettings`.

Minimal `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <OutputType>Library</OutputType>
    <UseWindowsForms>true</UseWindowsForms>
    <PlatformTarget>x64</PlatformTarget>
    <LangVersion>latest</LangVersion>
    <DebugType>embedded</DebugType>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="ExileCore">
      <HintPath>$(exapiPackage)\ExileCore.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="GameOffsets">
      <HintPath>$(exapiPackage)\GameOffsets.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="ImGui.NET" Version="1.90.0.1" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="SharpDX.Mathematics" Version="4.2.0" />
  </ItemGroup>
</Project>
```

### Plugin Lifecycle

| Method | When called | Notes |
|---|---|---|
| `Initialise()` | Once on load | Register hotkeys, wire up `OnPressed`/`OnValueChanged`, return `true` on success |
| `OnLoad()` | After Initialise | Load textures: `Graphics.InitImage("file.png")` |
| `AreaChange(AreaInstance area)` | Zone change | Clear cached entity lists here |
| `Tick()` | Every frame | Return `null` (no async job needed) or a `Job` for background work |
| `Render()` | Every frame | Draw overlays; check `Settings.Enable` and `GameController.InGame` |
| `EntityAdded(Entity entity)` | Entity enters range | Filter and cache relevant entities here |
| `EntityRemoved(Entity entity)` | Entity leaves range | Remove from caches |
| `DrawSettings()` | Settings panel open | Call `base.DrawSettings()` unless fully custom |

## GameController API

`GameController` is the main access point available in all plugin methods:

```csharp
// State checks
GameController.InGame
GameController.Game.IsInGameState

// Player
GameController.Player                          // local player Entity
GameController.Player.GetComponent<Positioned>().GridPosNum

// Area
GameController.Area.CurrentArea.IsPeaceful
GameController.Area.CurrentArea.Area.RawName
GameController.Area.CurrentArea.IsTown
GameController.Area.CurrentArea.IsHideout

// Entities — prefer ValidEntitiesByType over Entities for filtered access
GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
GameController.Entities   // all entities, more expensive

// Server data
GameController.IngameState.ServerData.PlayerInventories  // flask belt, inventory
GameController.IngameState.ServerData.PassiveSkillIds    // allocated passive IDs

// Ingame UI / state
GameController.Game.IngameState.IngameUi.Map.LargeMap.IsVisible
GameController.Game.IngameState.Camera        // Camera, WorldToScreen
GameController.Game.IngameState.Data          // terrain, server data

// Game files (static data)
GameController.Files.BaseItemTypes.Translate(entity.Path)
GameController.Files.PassiveSkills.GetPassiveSkillByPassiveId(id)

// Window
GameController.Window.GetWindowRectangleTimeCache   // cached

// Inter-plugin
GameController.PluginBridge.SaveMethod("MyPlugin.Method", delegate)
GameController.PluginBridge.GetMethod<TDelegate>("OtherPlugin.Method")

// Timing
GameController.DeltaTime  // double, seconds since last frame

// Input
Input.KeyDown(Keys.D1)    // send keypress
Input.KeyUp(Keys.D1)
Input.RegisterKey(key)    // register for hotkey tracking
```

## Entity API

```csharp
entity.Path         // "Metadata/Monsters/..."
entity.Id           // uint, unique per session
entity.IsValid      // always check before extensive use
entity.IsAlive
entity.IsHostile    // true for enemies
entity.Rarity       // MonsterRarity enum (Normal, Magic, Rare, Unique)
entity.GridPosNum   // Vector2, 2D grid position
entity.PosNum       // Vector3, 3D world position
entity.DistancePlayer  // float, distance to local player
entity.Type         // EntityType enum
entity.GetComponent<T>()  // null if not present
```

## Component Quick Reference

### Life
```csharp
var life = entity.GetComponent<Life>();
life.CurHP / life.MaxHP      // health
life.CurMana / life.MaxMana  // mana
life.CurES / life.MaxES      // energy shield
life.HPPercentage             // 0-100
```

### Buffs
```csharp
var buffs = entity.GetComponent<Buffs>();
buffs.BuffsList               // List<Buff>
buff.Name                     // internal string ID (e.g., "frozen", "tincture_life_burn")
buff.DisplayName              // human-readable
buff.Timer                    // seconds remaining
buff.MaxTime                  // original duration
buff.Charges                  // stack count
buff.BuffCharges              // alias for Charges
buff.FlaskSlot                // int — which flask slot this buff came from (-1 if N/A)
```

### Base & Mods (for item identification)
```csharp
var base_ = entity.GetComponent<Base>();
base_.Name                    // base item name
base_.ItemBaseName            // base type name

var mods = entity.GetComponent<Mods>();
mods.UniqueName               // unique item name (empty for non-uniques)
mods.ItemRarity               // ItemRarity enum
mods.ItemMods                 // List<ItemMod>
```

### Tincture
```csharp
var tincture = entity.GetComponent<Tincture>();
// Non-null if the item is a tincture
```

### Positioned & Render
```csharp
var pos = entity.GetComponent<Positioned>();
pos.GridPosNum                // Vector2 grid coords

var render = entity.GetComponent<Render>();
render.Name                   // display name
```

### ObjectMagicProperties
```csharp
var omp = entity.GetComponent<ObjectMagicProperties>();
omp.Rarity                    // MonsterRarity enum
omp.Mods                      // List<string> mod names
```

### Stats
```csharp
var stats = entity.GetComponent<Stats>();
stats.StatDictionary          // Dictionary<GameStat, int>
```

### Other Components
- `Monster` — monster-specific data
- `Player` — player-specific data
- `Chest`, `Portal`, `WorldItem`, `Targetable`, `Sockets`

## Flask Belt Access

```csharp
// Get the flask inventory holder
var flaskHolder = GameController.IngameState.ServerData.PlayerInventories
    .LastOrDefault(x => x.TypeId == InventoryNameE.Flask1);

// Iterate 5 flask slots (0-4)
for (int i = 0; i < 5; i++)
{
    var slotItem = flaskHolder.Inventory[i, 0];
    if (slotItem?.Item?.Address is null or 0) continue;
    var item = slotItem.Item;
    // item.GetComponent<Base>(), item.GetComponent<Mods>(), etc.
}
```

## Passive Tree Access

```csharp
// Get allocated passive skill IDs
var passiveIds = GameController.IngameState.ServerData.PassiveSkillIds;

// Look up passive details
var files = GameController.Files.PassiveSkills;
foreach (var id in passiveIds)
{
    var passive = files.GetPassiveSkillByPassiveId(id);
    passive.Name              // passive node name
    passive.IsNotable         // true for notable passives
    passive.IsAscendancyStartingNode  // true for ascendancy class node
}
```

## Settings Node Types

```csharp
public class MySettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public RangeNode<int> SomeRange { get; set; } = new RangeNode<int>(10, 1, 100);
    public RangeNode<float> SomeFloat { get; set; } = new RangeNode<float>(1.5f, 0f, 10f);
    public ColorNode SomeColor { get; set; } = new ColorNode(Color.White);
    public HotkeyNodeV2 SomeKey { get; set; } = Keys.F5;
    public TextNode SomeText { get; set; } = new TextNode("default");
    public EmptyNode SomeHeader { get; set; }

    [Menu("Display Name")]
    public ToggleNode WithLabel { get; set; } = new ToggleNode(true);

    [IgnoreMenu]
    public SomeType NotShownInSettings { get; set; }
}
```

## Graphics API

```csharp
// Rectangles — use System.Numerics.Vector2 for positions
Graphics.DrawBox(rectF, color);                // RectangleF overload
Graphics.DrawBox(topLeft, bottomRight, color, rounding);  // Vector2 overload
Graphics.DrawFrame(rectF, color, thickness);
Graphics.DrawFrame(topLeft, bottomRight, color, borderWidth, segments, rounding);

// Text
Graphics.DrawText("text", position, color);
Graphics.DrawTextWithBackground("text", position, textColor, alignment, bgColor);
var size = Graphics.MeasureText("text");

// Textures
Graphics.InitImage("key", fullPath);
Graphics.DrawImage("key", rectF, color);
Graphics.HasImage("key");

// Camera projection
var screenPos = GameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
```

## Performance Rules

- **NEVER** call `GetComponent<T>()` in `Render()` — do it in `Tick()`, store in fields
- **NEVER** iterate `GameController.Entities` — use `EntityListWrapper.ValidEntitiesByType`
- **NEVER** call `entity.Path` per-frame — cache in `EntityAdded()`
- Use `EntityAdded`/`EntityRemoved` to maintain filtered entity lists
- Use `TimeCache<T>` for expensive operations
- Clear state in `AreaChange()`, not per-tick
- Use cached accessors (`GetWindowRectangleTimeCache`)
- Check `entity.IsValid` and `entity.IsAlive` before processing
- Load textures in `OnLoad()`, never in `Render()`
- Separate data reads (`Tick`) from drawing (`Render`)
- Use `_canRender` flags to skip `Render()` entirely when nothing to draw
- Guard early in `Tick()`: InGame, IsAlive, area checks before work

## Coordinate Systems

- **Grid** (`GridPosNum`, `Vector2`): tile-based. Used for minimap and `DistancePlayer`.
- **World** (`PosNum`, `Vector3`): 3D world. Convert to screen with `Camera.WorldToScreen`.
- **Screen** (`Vector2`): pixel coords for ImGui and Graphics calls.

## Common Patterns

### Buff Monitoring
```csharp
var buffs = player.GetComponent<Buffs>()?.BuffsList;
if (buffs != null)
{
    for (int i = 0; i < buffs.Count; i++)
    {
        var buff = buffs[i];
        if (buff?.Name == "target_buff" && buff.Timer > 0)
        {
            // buff is active, buff.BuffCharges for stacks
        }
    }
}
```

### Flask Slot Detection via Buff
```csharp
// Detect which flask slot a buff came from
bool tinctureActive = buffs.Any(b => b.FlaskSlot == slotIndex);
```

### Rate-Limited Updates
```csharp
private readonly TimeCache<List<MyData>> _cache;
_cache = new TimeCache<List<MyData>>(BuildData, 200); // refresh every 200ms
```

### Custom ImGui Settings UI
```csharp
public override void DrawSettings()
{
    _settingsUi ??= new TinctureSettingsUi();
    _settingsUi.Draw(Settings, data, callback);
}
```

## Reference Plugins

Study other plugins in `Plugins/Source/` for pattern reference.
