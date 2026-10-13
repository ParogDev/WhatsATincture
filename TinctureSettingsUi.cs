using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace WhatsATincture;

// Settings panel: the generated SettingsMenu draws the Thresholds / Behavior / HUD tabs and the Burn Buff row from
// the settings class; this file adds what it can't know about: the status strip and Rescan button on the Tinctures
// tab, one card per detected tincture (Settings.Tinctures is a Dictionary), and the scenario-adaptive Guide tab.
public class TinctureSettingsUi
{
    private readonly SettingsMenu _menu;
    private WhatsATinctureSettings _settings;
    private BuildScanResult _build = new();
    private Action _rescan = () => { };

    private static readonly Vector4 Accent = new(0f, 0.75f, 1f, 1f);
    private static readonly Vector4 Label = new(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Vector4 Desc = new(0.45f, 0.47f, 0.52f, 1f);
    private static readonly Vector4 Good = new(0f, 1f, 0.5f, 1f);
    private static readonly Vector4 Warn = new(1f, 0.72f, 0.19f, 1f);
    private static readonly Vector4 BadCol = new(1f, 0.27f, 0.27f, 1f);
    private const float LabelW = 150f;

    public TinctureSettingsUi()
    {
        _menu = new SettingsMenu { Accent = Accent, RootTabName = "Tinctures" }
            .Tab("Tinctures", DrawStatusStrip, first: true)
            .Custom("Tinctures", DrawTinctureCards)
            .Tab("Guide", DrawGuide);
    }

    public void Draw(WhatsATinctureSettings s, BuildScanResult build, Action rescan)
    {
        _settings = s;
        _build = build ?? new BuildScanResult();
        _rescan = rescan ?? (() => { });
        _menu.Draw(s);
    }

    // ---- Tinctures tab: status strip + rescan ----

    private void DrawStatusStrip()
    {
        int count = _settings.Tinctures.Count;
        string scenarioText = _build.DetectedScenario switch
        {
            TinctureScenario.WardenDual => "Warden Dual",
            TinctureScenario.WardenSingle => "Warden Single",
            TinctureScenario.WardenTriple => "Warden Triple",
            TinctureScenario.NonWardenSingle => "Single",
            TinctureScenario.NonWardenDual => "Dual (No Linger)",
            _ => "None"
        };
        Dot(count > 0 ? Good : BadCol);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        Text(Label, $"{count} Tincture{(count != 1 ? "s" : "")}");
        ImGui.SameLine(0, 12);
        _menu.Badge(scenarioText);
        ImGui.SameLine(0, 6);
        _menu.Badge(_build.HasLingerMechanic ? "Linger" : "No Linger", _build.HasLingerMechanic ? Good : Desc);
        ImGui.SameLine(0, 6);
        _menu.Badge("Max Active: " + _build.MaxActiveTinctures, Desc);
        ImGui.SameLine(0, 12);
        if (_menu.AccentButton("Rescan Flask Belt")) _rescan();
        if (ImGui.IsItemHovered()) Tooltip("Re-read the flask belt and passive tree now (also happens on every area change)");
        ImGui.Spacing();
    }

    // ---- Tinctures tab: one card per detected tincture (custom block for Settings.Tinctures) ----

    private void DrawTinctureCards()
    {
        var s = _settings;
        if (s.Tinctures.Count == 0)
        {
            Text(Desc, "No tinctures detected. Equip tinctures and click Rescan.");
            return;
        }
        foreach (var (slot, config) in s.Tinctures.OrderBy(kv => kv.Key))
        {
            ImGui.PushID(slot);
            _menu.SectionHeader($"Slot {slot + 1}: {config.DisplayName}",
                string.IsNullOrEmpty(config.BaseName) || config.BaseName == config.DisplayName ? null : "Base: " + config.BaseName);
            if (config.HighStacksMode)
            {
                ImGui.SameLine();
                _menu.Badge("HighStacks", Warn, "Uses the unique stack limit while a unique enemy is near");
            }

            int normalGlobal = s.Thresholds.StacksNormal.Value;
            int uniqueGlobal = s.Thresholds.StacksUnique.Value;
            ThresholdRow("Normal Limit", "Burn stacks at which this tincture turns off (global unless overridden)",
                config.StackThresholdOverride, normalGlobal, v => config.StackThresholdOverride = v);
            ThresholdRow("Unique Limit", "Stack limit while a unique enemy is near (global unless overridden)",
                config.StackThresholdUniqueOverride, config.HighStacksMode ? uniqueGlobal : normalGlobal, v => config.StackThresholdUniqueOverride = v);

            Row("Deact Priority", "Lower = deactivated first", () =>
            {
                int v = config.DeactivationPriority;
                if (_menu.Slider("##dp", ref v, 1, 100)) config.DeactivationPriority = v;
            });
            Row("Act Priority", "Lower = activated first", () =>
            {
                int v = config.ActivationPriority;
                if (_menu.Slider("##ap", ref v, 1, 100)) config.ActivationPriority = v;
            });
            Row("High Stacks Mode", "Use the unique stack limit vs unique enemies", () =>
            {
                bool hs = config.HighStacksMode;
                if (_menu.PillToggle("##hs", ref hs, "tc_hs_" + slot)) config.HighStacksMode = hs;
            });
            ImGui.PopID();
        }
    }

    private void ThresholdRow(string label, string desc, int overrideVal, int globalValue, Action<int> set)
    {
        Row(label, desc, () =>
        {
            if (overrideVal >= 0)
            {
                int v = overrideVal;
                ImGui.SetNextItemWidth(Math.Max(80f, ImGui.GetContentRegionAvail().X - 70f));
                if (_menu.Slider("##sl", ref v, 1, 100, "stacks")) set(v);
                ImGui.SameLine();
                if (ImGui.SmallButton("Reset##rst")) set(-1);
                if (ImGui.IsItemHovered()) Tooltip("Back to the global limit");
            }
            else
            {
                ImGui.AlignTextToFramePadding();
                Text(Accent, $"Global ({globalValue})");
                ImGui.SameLine();
                if (ImGui.SmallButton("Override##ovr")) set(globalValue);
            }
        });
    }

    // label | control, dim description under the label: the row shape SettingsMenu draws
    private static void Row(string label, string desc, Action control)
    {
        ImGui.PushID(label);
        ImGui.AlignTextToFramePadding();
        Text(Label, label);
        if (ImGui.IsItemHovered()) Tooltip(desc);
        ImGui.SameLine(LabelW);
        ImGui.SetNextItemWidth(-1);
        control();
        Text(Desc, desc);
        ImGui.Dummy(new Vector2(1, 2));
        ImGui.PopID();
    }

    // text taken as-is (ImGui's Text* calls are printf-style: "100% uptime" would be mangled)
    private static void Text(Vector4 color, string text) => SettingsMenu.Text(color, text);
    private static void Dim(string text) => SettingsMenu.Dim(text);
    private static void Tooltip(string text) => SettingsMenu.Tooltip(text);

    private static void Dot(Vector4 color)
    {
        var p = ImGui.GetCursorScreenPos();
        float h = ImGui.GetFrameHeight();
        ImGui.GetWindowDrawList().AddCircleFilled(new Vector2(p.X + 6f, p.Y + h * 0.5f), 4.5f, ImGui.GetColorU32(color));
        ImGui.Dummy(new Vector2(12f, h));
    }

    // ---- Guide tab ----

    private void DrawGuide()
    {
        int tinctureCount = _settings.Tinctures.Count;
        var b = _build;
        switch (b.DetectedScenario)
        {
            case TinctureScenario.WardenDual:
                Title("Warden Dual Rotation - 100% Tincture Uptime");
                Section("How It Works");
                Lines(Label,
                    "Both tinctures run simultaneously, sharing one burn stack pool.",
                    "At 12 stacks, deactivate in priority order. Enduring Suffusion lingers for ~6s.",
                    "Tincture cooldown is also ~6s (with increased cooldown recovery rate).",
                    "Cooldown finishes as lingering ends - reactivate both for 100% uptime.",
                    "HighStacks tinctures (e.g. Sap of the Seasons) deactivate last and use a",
                    "higher threshold vs unique bosses to maximize stack-scaling bonuses.");
                Section("Required Setup");
                Check("Warden Ascendancy", "Must be Warden class (Ranger ascendancy)", b.HasWardenAscendancy);
                Check("Enduring Suffusion", "Tincture effects linger 6s after deactivation", b.HasEnduringSuffusion);
                Check("Experienced Herbalist", "Allows equipping two tinctures simultaneously", b.HasExperiencedHerbalist);
                Check("Bloodsoaked Blade", "Burns life instead of mana (tincture_life_burn vs tincture_mana_burn)", b.HasBloodsoackedBlade, optional: true);
                Section("Optional Notables");
                Check("Seasoned Hunter", "Additional tincture effect bonuses", b.HasSeasonedHunter, optional: true);
                Check("Nature's Concoction", "Enhanced tincture potency", b.HasNaturesConcoction, optional: true);
                CountStatus(tinctureCount, 2, b.MaxActiveTinctures);
                break;
            case TinctureScenario.WardenSingle:
                Title("Warden Single Tincture - Linger Management");
                Section("How It Works");
                Lines(Label,
                    "Single tincture with Enduring Suffusion linger window.",
                    "Deactivate at stack threshold. Linger effect continues for ~6s.",
                    "Tincture cooldown finishes as linger fades - reactivate immediately.",
                    "Near-100% uptime achievable with proper cooldown recovery rate.");
                Section("Required Setup");
                Check("Warden Ascendancy", "Must be Warden class (Ranger ascendancy)", b.HasWardenAscendancy);
                Check("Enduring Suffusion", "Tincture effects linger 6s after deactivation", b.HasEnduringSuffusion);
                Check("Bloodsoaked Blade", "Burns life instead of mana (tincture_life_burn vs tincture_mana_burn)", b.HasBloodsoackedBlade, optional: true);
                Section("Optional Notables");
                Check("Experienced Herbalist", "Allocate to enable dual tincture rotation", b.HasExperiencedHerbalist, optional: true);
                Check("Seasoned Hunter", "Additional tincture effect bonuses", b.HasSeasonedHunter, optional: true);
                Check("Nature's Concoction", "Enhanced tincture potency", b.HasNaturesConcoction, optional: true);
                CountStatus(tinctureCount, 1, b.MaxActiveTinctures);
                break;
            case TinctureScenario.WardenTriple:
                Title("Warden Triple Tincture - Advanced Rotation");
                Section("How It Works");
                Lines(Label,
                    "Three tinctures with priority-based rotation. Advanced setup.",
                    "Plugin activates up to max active count, ordered by activation priority.",
                    "Deactivation follows priority order (lower = deactivated first).",
                    "Linger window covers cooldown for seamless transitions.",
                    "Use per-tincture priority sliders on the Tinctures tab to control order.");
                Section("Required Setup");
                Check("Warden Ascendancy", "Must be Warden class (Ranger ascendancy)", b.HasWardenAscendancy);
                Check("Enduring Suffusion", "Tincture effects linger 6s after deactivation", b.HasEnduringSuffusion);
                Check("Experienced Herbalist", "Allows equipping two tinctures simultaneously", b.HasExperiencedHerbalist);
                Check("Bloodsoaked Blade", "Burns life instead of mana (tincture_life_burn vs tincture_mana_burn)", b.HasBloodsoackedBlade, optional: true);
                Section("Third Tincture Source");
                Lines(Desc,
                    "Third tincture slot requires The Living Blade unique sword or similar item.",
                    "Plugin auto-detects all equipped tinctures regardless of source.");
                Section("Optional Notables");
                Check("Seasoned Hunter", "Additional tincture effect bonuses", b.HasSeasonedHunter, optional: true);
                Check("Nature's Concoction", "Enhanced tincture potency", b.HasNaturesConcoction, optional: true);
                CountStatus(tinctureCount, 3, b.MaxActiveTinctures);
                break;
            case TinctureScenario.NonWardenSingle:
                Title("Single Tincture - Simple On/Off");
                Section("How It Works");
                Lines(Label,
                    "Simple on/off management. Activate when enemies are nearby.",
                    "Deactivate at burn stack threshold to prevent excessive life/mana loss.",
                    "No linger mechanic - cooldown gap is unavoidable, downtime is expected.",
                    "Plugin handles activation and emergency deactivation automatically.");
                Section("Upgrade Path");
                Lines(Desc,
                    "Consider Warden ascendancy (Ranger) for Enduring Suffusion linger,",
                    "which enables near-100% uptime by covering the cooldown gap.");
                Check("Warden Ascendancy", "Not detected - any class can use a single tincture", b.HasWardenAscendancy, optional: true);
                CountStatus(tinctureCount, 1, b.MaxActiveTinctures);
                break;
            case TinctureScenario.NonWardenDual:
                Title("Dual Tincture - No Linger");
                Section("How It Works");
                Lines(Label,
                    "Two tinctures sharing one burn stack pool, no linger mechanic.",
                    "Deactivating any tincture puts all tinctures on shared cooldown.",
                    "Significant downtime during cooldown periods is expected.",
                    "Plugin manages activation order and emergency deactivation.");
                Section("Dual Tincture Source");
                Lines(Desc,
                    "Dual tinctures without Experienced Herbalist likely via The Living Blade",
                    "unique sword, which grants an additional tincture slot.");
                Section("Upgrade Path");
                Lines(Desc,
                    "Consider Warden ascendancy (Ranger) for Enduring Suffusion linger,",
                    "which enables near-100% uptime by covering the cooldown gap.");
                Check("Warden Ascendancy", "Not detected - enables linger for uptime", b.HasWardenAscendancy, optional: true);
                Check("Enduring Suffusion", "Not detected - tincture effects would linger after deactivation", b.HasEnduringSuffusion, optional: true);
                CountStatus(tinctureCount, 2, b.MaxActiveTinctures);
                break;
            default:
                Title("No Tinctures Detected");
                Lines(Label,
                    "No tinctures found in the flask belt.",
                    "Equip one or more tinctures, then click Rescan on the Tinctures tab.",
                    "The plugin will auto-detect your setup and configure smart defaults.");
                break;
        }
    }

    private static void Title(string text)
    {
        Text(Accent, text);
        ImGui.Spacing();
    }

    private void Section(string title) => _menu.SectionHeader(title);

    private static void Lines(Vector4 color, params string[] lines)
    {
        ImGui.PushTextWrapPos(0f);
        foreach (var line in lines) Text(color, line);
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
    }

    private static void Check(string name, string desc, bool allocated, bool optional = false)
    {
        Dot(allocated ? Good : optional ? Warn : BadCol);
        ImGui.SameLine();
        ImGui.BeginGroup();
        Text(Label, name);
        Text(Desc, desc);
        ImGui.EndGroup();
        ImGui.Dummy(new Vector2(1, 2));
    }

    private void CountStatus(int tinctureCount, int needed, int maxActive)
    {
        Section("Tinctures Needed");
        bool enough = tinctureCount >= needed;
        string status = enough
            ? $"{needed} tincture{(needed != 1 ? "s" : "")} equipped ({tinctureCount} found, max active: {maxActive})"
            : $"Need {needed} tinctures - only {tinctureCount} found";
        Dot(enough ? Good : BadCol);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        Text(Label, status);
    }
}
