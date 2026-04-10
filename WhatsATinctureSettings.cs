using System.Collections.Generic;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace WhatsATincture;

public class WhatsATinctureSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(true);
    public TextNode BurnBuffName { get; set; } = new TextNode("tincture_life_burn");
    [IgnoreMenu] public Dictionary<int, TinctureSlotConfig> Tinctures { get; set; } = new();
    public GlobalThresholds Thresholds { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public HudSettings Hud { get; set; } = new();
    [IgnoreMenu] public int SettingsVersion { get; set; }
}

[Submenu]
public class GlobalThresholds
{
    public RangeNode<int> StacksNormal { get; set; } = new RangeNode<int>(12, 1, 100);
    public RangeNode<int> StacksUnique { get; set; } = new RangeNode<int>(50, 1, 100);
    public RangeNode<int> HpMinNormal { get; set; } = new RangeNode<int>(30, 0, 100);
    public RangeNode<int> HpMinUnique { get; set; } = new RangeNode<int>(60, 0, 100);
}

[Submenu]
public class BehaviorSettings
{
    public RangeNode<int> ActivationRange { get; set; } = new RangeNode<int>(80, 10, 200);
    public RangeNode<int> ToggleCooldownMs { get; set; } = new RangeNode<int>(400, 100, 1000);
}

[Submenu]
public class HudSettings
{
    public ToggleNode ShowHud { get; set; } = new ToggleNode(true);
    public RangeNode<int> HudX { get; set; } = new RangeNode<int>(500, 0, 3840);
    public RangeNode<int> HudY { get; set; } = new RangeNode<int>(10, 0, 2160);
    public RangeNode<int> BarWidth { get; set; } = new RangeNode<int>(220, 50, 600);
    public RangeNode<int> BarHeight { get; set; } = new RangeNode<int>(22, 10, 60);
}
