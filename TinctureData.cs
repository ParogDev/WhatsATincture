using System;
using System.Collections.Generic;

namespace WhatsATincture;

public record FlaskSlotInfo(int SlotIndex, string DisplayName, string BaseName, string UniqueName, bool IsTincture);

public enum TinctureScenario
{
    None,
    NonWardenSingle,
    NonWardenDual,
    WardenSingle,
    WardenDual,
    WardenTriple
}

public class TinctureSlotConfig
{
    public string DisplayName { get; set; } = "";
    public string BaseName { get; set; } = "";
    public string UniqueName { get; set; } = "";
    public int StackThresholdOverride { get; set; } = -1;
    public int StackThresholdUniqueOverride { get; set; } = -1;
    public int DeactivationPriority { get; set; } = 50;
    public int ActivationPriority { get; set; } = 50;
    public bool HighStacksMode { get; set; }
}

public class TinctureRuntimeState
{
    public bool WasActive { get; set; }
    public DateTime ReadyAt { get; set; } = DateTime.MinValue;
    public bool HasPendingToggle { get; set; }
    public bool PendingExpectedActive { get; set; }
    public DateTime PendingPressedAt { get; set; }
}

public class BuildScanResult
{
    public bool HasWardenAscendancy { get; set; }
    public bool HasEnduringSuffusion { get; set; }
    public bool HasExperiencedHerbalist { get; set; }
    public bool HasBloodsoackedBlade { get; set; }
    public bool HasSeasonedHunter { get; set; }
    public bool HasNaturesConcoction { get; set; }
    public List<string> AllocatedNotableNames { get; set; } = new();
    public string AscendancyClassName { get; set; } = "";
    public int MaxActiveTinctures { get; set; } = 1;
    public bool HasLingerMechanic { get; set; }
    public TinctureScenario DetectedScenario { get; set; } = TinctureScenario.None;
}
