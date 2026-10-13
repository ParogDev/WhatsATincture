using System.Collections.Generic;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace WhatsATincture;

// Drawn by SettingsMenu (see SettingsMenu.cs): [Menu] gives the label and the description, [MenuReadOnly] shows a
// value the plugin sets itself, [MenuUnit] suffixes a slider's readout. Tinctures (a Dictionary) is drawn by
// TinctureSettingsUi through SettingsMenu.Custom.
public class WhatsATinctureSettings : ISettings
{
    [Menu("Enable", "Turn the whole plugin on or off")]
    public ToggleNode Enable { get; set; } = new ToggleNode(true);

    [Menu("Burn Buff", "Buff that counts tincture burn stacks; set automatically from your build (tincture_life_burn with Bloodsoaked Blade, else tincture_mana_burn)")]
    [MenuReadOnly("Set from your passive tree on every area change: tincture_life_burn with Bloodsoaked Blade, else tincture_mana_burn")]
    public TextNode BurnBuffName { get; set; } = new TextNode("tincture_life_burn");

    [IgnoreMenu] public Dictionary<int, TinctureSlotConfig> Tinctures { get; set; } = new();

    [Menu("Thresholds", "Burn stack limits and minimum life/mana before a tincture is turned off")]
    public GlobalThresholds Thresholds { get; set; } = new();

    [Menu("Behavior", "Enemy detection range and key press pacing")]
    public BehaviorSettings Behavior { get; set; } = new();

    [Menu("HUD", "On-screen tincture status bars")]
    public HudSettings Hud { get; set; } = new();

    [IgnoreMenu] public int SettingsVersion { get; set; }
}

[Submenu]
public class GlobalThresholds
{
    [Menu("Normal Stack Limit", "Turn a tincture off at this many burn stacks (1-100)")]
    [MenuUnit("stacks")]
    public RangeNode<int> StacksNormal { get; set; } = new RangeNode<int>(12, 1, 100);

    [Menu("Unique Stack Limit", "Higher stack limit for High Stacks tinctures while a unique enemy is near (1-100)")]
    [MenuUnit("stacks")]
    public RangeNode<int> StacksUnique { get; set; } = new RangeNode<int>(50, 1, 100);

    [Menu("Min Resource %", "Turn tinctures off below this % of the burned resource (life or mana), 0-100")]
    [MenuUnit("%")]
    public RangeNode<int> HpMinNormal { get; set; } = new RangeNode<int>(30, 0, 100);

    [Menu("Min Resource % (Unique)", "Same floor while a unique enemy is near, 0-100")]
    [MenuUnit("%")]
    public RangeNode<int> HpMinUnique { get; set; } = new RangeNode<int>(60, 0, 100);
}

[Submenu]
public class BehaviorSettings
{
    [Menu("Detection Range", "How far to look for hostile enemies, in grid units (10-200)")]
    [MenuUnit("grid")]
    public RangeNode<int> ActivationRange { get; set; } = new RangeNode<int>(80, 10, 200);

    [Menu("Toggle Cooldown", "Minimum time between flask key presses, in ms (100-1000)")]
    [MenuUnit("ms")]
    public RangeNode<int> ToggleCooldownMs { get; set; } = new RangeNode<int>(400, 100, 1000);
}

[Submenu]
public class HudSettings
{
    [Menu("Show Overlay", "Show the tincture status bars on screen")]
    public ToggleNode ShowHud { get; set; } = new ToggleNode(true);

    [Menu("Position X", "Left edge of the bars, in screen px")]
    [MenuUnit("px")]
    public RangeNode<int> HudX { get; set; } = new RangeNode<int>(500, 0, 3840);

    [Menu("Position Y", "Top edge of the bars, in screen px")]
    [MenuUnit("px")]
    public RangeNode<int> HudY { get; set; } = new RangeNode<int>(10, 0, 2160);

    [Menu("Bar Width", "Width of each tincture bar, in px (50-600)")]
    [MenuUnit("px")]
    public RangeNode<int> BarWidth { get; set; } = new RangeNode<int>(220, 50, 600);

    [Menu("Bar Height", "Height of each tincture bar, in px (10-60)")]
    [MenuUnit("px")]
    public RangeNode<int> BarHeight { get; set; } = new RangeNode<int>(22, 10, 60);
}
