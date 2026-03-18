using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ExileCore.Shared.Nodes;
using ImGuiNET;

namespace WhatsATincture;

public class TinctureSettingsUi
{
    private int _activeTab;
    private readonly Dictionary<string, float> _anims = new();

    private static readonly string[] Tabs = { "Tinctures", "Thresholds", "Behavior", "HUD", "Guide" };

    // ── Palette ─────────────────────────────────────────────────────
    private static uint Accent => Col(0f, 0.75f, 1f);
    private static uint AccentDim => Col(0f, 0.45f, 0.6f);
    private static uint Label => Col(0.88f, 0.88f, 0.88f);
    private static uint Desc => Col(0.38f, 0.40f, 0.44f);
    private static uint TabOff => Col(0.48f, 0.48f, 0.52f);
    private static uint CardBg => Col(0.05f, 0.05f, 0.07f, 1f);
    private const float Row = 34f;

    // ── Entry point ─────────────────────────────────────────────────

    public void Draw(WhatsATinctureSettings s, BuildScanResult build, Action rescan)
    {
        var contentMin = ImGui.GetCursorScreenPos();
        float contentW = ImGui.GetContentRegionAvail().X;
        var dl = ImGui.GetWindowDrawList();

        // Tab bar
        float tabH = 26f;
        float tabW = contentW / Tabs.Length;

        for (int i = 0; i < Tabs.Length; i++)
        {
            var tMin = new Vector2(contentMin.X + i * tabW, contentMin.Y);
            var tMax = new Vector2(contentMin.X + (i + 1) * tabW, contentMin.Y + tabH);
            bool active = i == _activeTab;

            if (active)
                dl.AddRectFilled(tMin, tMax, WithAlpha(Accent, 0.12f));

            ImGui.SetCursorScreenPos(tMin);
            ImGui.InvisibleButton($"##tc_tab_{i}", tMax - tMin);
            bool hov = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked()) _activeTab = i;

            if (hov && !active)
                dl.AddRectFilled(tMin, tMax, WithAlpha(Accent, 0.06f));
            if (active)
                dl.AddLine(tMin with { Y = tMax.Y - 1 } + new Vector2(3, 0),
                    tMax - new Vector2(3, 1), Accent, 2f);

            uint tc = active ? Accent : (hov ? AccentDim : TabOff);
            CenterText(dl, Tabs[i], (tMin + tMax) * 0.5f, tc);
        }

        float sepY = contentMin.Y + tabH + 1;
        dl.AddLine(new Vector2(contentMin.X, sepY),
            new Vector2(contentMin.X + contentW, sepY), WithAlpha(Accent, 0.10f), 1f);

        // Content area
        ImGui.SetCursorScreenPos(new Vector2(contentMin.X, sepY + 3));
        var avail = new Vector2(contentW, ImGui.GetContentRegionAvail().Y);
        ImGui.BeginChild("##tc_content", avail, ImGuiChildFlags.None, ImGuiWindowFlags.None);

        var cdl = ImGui.GetWindowDrawList();
        var cMin = ImGui.GetWindowPos();
        var cSz = ImGui.GetWindowSize();

        cdl.AddRectFilled(cMin, cMin + cSz, CardBg, 3f);
        float pulse = (float)(0.4 + 0.3 * Math.Sin(ImGui.GetTime() * 1.8));
        cdl.AddRect(cMin, cMin + cSz, WithAlpha(Accent, pulse * 0.18f), 3f, ImDrawFlags.None, 1f);

        float y = cMin.Y + 10;
        float x = cMin.X + 12;
        float cx = cMin.X + cSz.X * 0.50f;
        float sw = cSz.X * 0.40f;

        switch (_activeTab)
        {
            case 0: TabTinctures(cdl, s, build, x, cx, ref y, sw, rescan); break;
            case 1: TabThresholds(cdl, s, build, x, cx, ref y, sw); break;
            case 2: TabBehavior(cdl, s, x, cx, ref y, sw); break;
            case 3: TabHud(cdl, s, x, cx, ref y, sw); break;
            case 4: TabGuide(cdl, s, build, x, ref y, cSz.X - 24); break;
        }

        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.Dummy(new Vector2(1, 4));
        ImGui.EndChild();
    }

    // ── Tabs ────────────────────────────────────────────────────────

    private void TabTinctures(ImDrawListPtr dl, WhatsATinctureSettings s, BuildScanResult build,
        float x, float cx, ref float y, float sw, Action rescan)
    {
        // Status bar
        int count = s.Tinctures.Count;
        string scenarioText = build.DetectedScenario switch
        {
            TinctureScenario.WardenDual => "Warden Dual",
            TinctureScenario.WardenSingle => "Warden Single",
            TinctureScenario.WardenTriple => "Warden Triple",
            TinctureScenario.NonWardenSingle => "Single",
            TinctureScenario.NonWardenDual => "Dual (No Linger)",
            _ => "None"
        };
        string lingerText = build.HasLingerMechanic ? "Linger" : "No Linger";

        float stripW = cx + sw - x + 8;
        var stripMin = new Vector2(x - 4, y);
        var stripMax = new Vector2(x - 4 + stripW, y + 22);
        dl.AddRectFilled(stripMin, stripMax, Col(0.03f, 0.06f, 0.08f), 3f);
        dl.AddRect(stripMin, stripMax, WithAlpha(Accent, 0.2f), 3f);

        uint dot = count > 0 ? Col(0, 1, 0.5f) : Col(1, 0.27f, 0.27f);
        dl.AddCircleFilled(new Vector2(x + 4, y + 11), 4f, dot);
        string statusText = $"{count} Tincture{(count != 1 ? "s" : "")}  |  {scenarioText}  |  {lingerText}  |  Max Active: {build.MaxActiveTinctures}";
        dl.AddText(new Vector2(x + 14, y + 3), Label, statusText);
        y += 28;

        // Rescan button
        if (NeonButton("##tc_rescan", "Rescan Flask Belt", dl, x, ref y))
            rescan();

        // Burn buff (auto-detected, display only)
        dl.AddText(new Vector2(x, y + 1), Label, "Burn Buff");
        dl.AddText(new Vector2(x, y + 16), Desc, "Auto-detected from passive tree");
        dl.AddText(new Vector2(cx, y + 1), Accent, s.BurnBuffName.Value ?? "");
        y += Row;

        // Per-tincture cards
        if (count == 0)
        {
            dl.AddText(new Vector2(x + 4, y + 4), Desc, "No tinctures detected. Equip tinctures and click Rescan.");
            y += 28;
        }
        else
        {
            foreach (var (slot, config) in s.Tinctures.OrderBy(kv => kv.Key))
                DrawTinctureCard(dl, s, slot, config, x, cx, ref y, sw);
        }
    }

    private void DrawTinctureCard(ImDrawListPtr dl, WhatsATinctureSettings s,
        int slot, TinctureSlotConfig config, float x, float cx, ref float y, float sw)
    {
        float cardW = cx + sw - x + 8;

        // Header
        var headerMin = new Vector2(x - 2, y);
        var headerMax = new Vector2(x - 2 + cardW, y + 22);
        dl.AddRectFilled(headerMin, headerMax, WithAlpha(Accent, 0.08f), 3f);
        dl.AddText(new Vector2(x + 2, y + 3), Accent, $"Slot {slot + 1}: {config.DisplayName}");

        if (config.HighStacksMode)
        {
            var badge = "HighStacks";
            var badgeSize = ImGui.CalcTextSize(badge);
            dl.AddText(new Vector2(headerMax.X - badgeSize.X - 8, y + 3), Col(1, 0.72f, 0.19f), badge);
        }
        y += 26;

        if (!string.IsNullOrEmpty(config.BaseName) && config.BaseName != config.DisplayName)
        {
            dl.AddText(new Vector2(x + 4, y), Desc, $"Base: {config.BaseName}");
            y += 18;
        }

        // Threshold overrides
        int normalGlobal = s.Thresholds.StacksNormal.Value;
        int uniqueGlobal = s.Thresholds.StacksUnique.Value;

        DrawThresholdRow($"tc_s{slot}_tn", config, false, dl, x + 4, cx, ref y, sw,
            "Normal Limit", normalGlobal);
        DrawThresholdRow($"tc_s{slot}_tu", config, true, dl, x + 4, cx, ref y, sw,
            "Unique Limit", config.HighStacksMode ? uniqueGlobal : normalGlobal);

        // Priority sliders
        int dp = config.DeactivationPriority;
        dl.AddText(new Vector2(x + 4, y + 1), Label, "Deact Priority");
        dl.AddText(new Vector2(x + 4, y + 14), Desc, "Lower = deactivated first");
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 5));
        if (SliderInt($"##tc_s{slot}_dp", ref dp, 1, 100, sw * 0.85f))
            config.DeactivationPriority = dp;
        dl.AddText(new Vector2(cx + sw * 0.85f + 6, y + 5), Accent, dp.ToString());
        y += Row;

        int ap = config.ActivationPriority;
        dl.AddText(new Vector2(x + 4, y + 1), Label, "Act Priority");
        dl.AddText(new Vector2(x + 4, y + 14), Desc, "Lower = activated first");
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 5));
        if (SliderInt($"##tc_s{slot}_ap", ref ap, 1, 100, sw * 0.85f))
            config.ActivationPriority = ap;
        dl.AddText(new Vector2(cx + sw * 0.85f + 6, y + 5), Accent, ap.ToString());
        y += Row;

        // HighStacks toggle
        bool hs = config.HighStacksMode;
        dl.AddText(new Vector2(x + 4, y + 1), Label, "High Stacks Mode");
        dl.AddText(new Vector2(x + 4, y + 14), Desc, "Use unique threshold vs unique enemies");
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 5));
        string animKey = $"tc_s{slot}_hs";
        _anims.TryGetValue(animKey, out float a);
        if (PillToggle($"##{animKey}", ref hs, ref a))
            config.HighStacksMode = hs;
        _anims[animKey] = a;
        y += Row;

        // Card separator
        dl.AddLine(new Vector2(x - 2, y), new Vector2(x - 2 + cardW, y), WithAlpha(Accent, 0.1f));
        y += 8;
    }

    private void DrawThresholdRow(string key, TinctureSlotConfig config, bool isUnique,
        ImDrawListPtr dl, float x, float cx, ref float y, float sw,
        string label, int globalValue)
    {
        dl.AddText(new Vector2(x, y + 1), Label, label);

        int overrideVal = isUnique ? config.StackThresholdUniqueOverride : config.StackThresholdOverride;
        bool isOverridden = overrideVal >= 0;

        if (isOverridden)
        {
            int v = overrideVal;
            ImGui.SetCursorScreenPos(new Vector2(cx, y + 3));
            if (SliderInt($"##{key}_sl", ref v, 1, 100, sw * 0.60f))
            {
                if (isUnique) config.StackThresholdUniqueOverride = v;
                else config.StackThresholdOverride = v;
            }
            dl.AddText(new Vector2(cx + sw * 0.60f + 6, y + 3), Accent, v.ToString());

            if (SmallNeonButton($"##{key}_rst", "Reset", dl, cx + sw * 0.75f, y + 1))
            {
                if (isUnique) config.StackThresholdUniqueOverride = -1;
                else config.StackThresholdOverride = -1;
            }
        }
        else
        {
            dl.AddText(new Vector2(cx, y + 1), Accent, $"Global ({globalValue})");

            if (SmallNeonButton($"##{key}_ovr", "Override", dl, cx + sw * 0.40f, y + 1))
            {
                if (isUnique) config.StackThresholdUniqueOverride = globalValue;
                else config.StackThresholdOverride = globalValue;
            }
        }

        y += 24;
    }

    private void TabThresholds(ImDrawListPtr dl, WhatsATinctureSettings s,
        BuildScanResult build, float x, float cx, ref float y, float sw)
    {
        bool isMana = !build.HasBloodsoackedBlade;
        string resource = isMana ? "Mana" : "HP";

        IntSlider("tc_sn", s.Thresholds.StacksNormal, dl, x, cx, ref y, sw,
            "Normal Stack Limit", "Deactivate tincture at this burn stack count");
        IntSlider("tc_su", s.Thresholds.StacksUnique, dl, x, cx, ref y, sw,
            "Unique Boss Limit", "Higher threshold for High Stacks tinctures vs unique enemies");
        IntSlider("tc_hpn", s.Thresholds.HpMinNormal, dl, x, cx, ref y, sw,
            $"Min {resource} (Normal)", $"Deactivate if {resource.ToLower()} drops below this %");
        IntSlider("tc_hpu", s.Thresholds.HpMinUnique, dl, x, cx, ref y, sw,
            $"Min {resource} (Unique)", $"Safety threshold for unique boss fights");
    }

    private void TabBehavior(ImDrawListPtr dl, WhatsATinctureSettings s,
        float x, float cx, ref float y, float sw)
    {
        IntSlider("tc_range", s.Behavior.ActivationRange, dl, x, cx, ref y, sw,
            "Detection Range", "How far to scan for enemies (grid units)");
        IntSlider("tc_cd", s.Behavior.ToggleCooldownMs, dl, x, cx, ref y, sw,
            "Toggle Cooldown", "Minimum ms between flask key presses");
    }

    private void TabHud(ImDrawListPtr dl, WhatsATinctureSettings s,
        float x, float cx, ref float y, float sw)
    {
        Toggle("tc_show", s.Hud.ShowHud, dl, x, cx, ref y,
            "Show Overlay", "Display tincture status bars on screen");
        IntSlider("tc_hx", s.Hud.HudX, dl, x, cx, ref y, sw,
            "Position X", "Horizontal position of the HUD bars");
        IntSlider("tc_hy", s.Hud.HudY, dl, x, cx, ref y, sw,
            "Position Y", "Vertical position of the HUD bars");
        IntSlider("tc_bw", s.Hud.BarWidth, dl, x, cx, ref y, sw,
            "Bar Width", "Width of each tincture status bar");
        IntSlider("tc_bh", s.Hud.BarHeight, dl, x, cx, ref y, sw,
            "Bar Height", "Height of each tincture status bar");
    }

    private void TabGuide(ImDrawListPtr dl, WhatsATinctureSettings s, BuildScanResult build,
        float x, ref float y, float width)
    {
        var scenario = build.DetectedScenario;
        int tinctureCount = s.Tinctures.Count;

        switch (scenario)
        {
            case TinctureScenario.WardenDual:
                GuideWardenDual(dl, build, x, ref y, width, tinctureCount);
                break;
            case TinctureScenario.WardenSingle:
                GuideWardenSingle(dl, build, x, ref y, width, tinctureCount);
                break;
            case TinctureScenario.WardenTriple:
                GuideWardenTriple(dl, build, x, ref y, width, tinctureCount);
                break;
            case TinctureScenario.NonWardenSingle:
                GuideNonWardenSingle(dl, build, x, ref y, width, tinctureCount);
                break;
            case TinctureScenario.NonWardenDual:
                GuideNonWardenDual(dl, build, x, ref y, width, tinctureCount);
                break;
            default:
                GuideNone(dl, x, ref y);
                break;
        }
    }

    private void GuideWardenDual(ImDrawListPtr dl, BuildScanResult build,
        float x, ref float y, float width, int tinctureCount)
    {
        dl.AddText(new Vector2(x, y), Accent, "Warden Dual Rotation - 100% Tincture Uptime");
        y += 24;

        DrawSectionHeader(dl, x, width, ref y, "How It Works");
        DrawInfoLines(dl, x, ref y, Label,
            "Both tinctures run simultaneously, sharing one burn stack pool.",
            "At 12 stacks, deactivate in priority order. Enduring Suffusion lingers for ~6s.",
            "Tincture cooldown is also ~6s (with increased cooldown recovery rate).",
            "Cooldown finishes as lingering ends - reactivate both for 100% uptime.",
            "HighStacks tinctures (e.g. Sap of the Seasons) deactivate last and use a",
            "higher threshold vs unique bosses to maximize stack-scaling bonuses.");

        DrawSectionHeader(dl, x, width, ref y, "Required Setup");
        DrawPassiveCheckItem(dl, x, ref y, "Warden Ascendancy",
            "Must be Warden class (Ranger ascendancy)", build.HasWardenAscendancy);
        DrawPassiveCheckItem(dl, x, ref y, "Enduring Suffusion",
            "Tincture effects linger 6s after deactivation", build.HasEnduringSuffusion);
        DrawPassiveCheckItem(dl, x, ref y, "Experienced Herbalist",
            "Allows equipping two tinctures simultaneously", build.HasExperiencedHerbalist);
        DrawPassiveCheckItem(dl, x, ref y, "Bloodsoaked Blade",
            "Burns life instead of mana (tincture_life_burn vs tincture_mana_burn)",
            build.HasBloodsoackedBlade, optional: true);
        y += 8;

        DrawSectionHeader(dl, x, width, ref y, "Optional Notables");
        DrawPassiveCheckItem(dl, x, ref y, "Seasoned Hunter",
            "Additional tincture effect bonuses", build.HasSeasonedHunter, optional: true);
        DrawPassiveCheckItem(dl, x, ref y, "Nature's Concoction",
            "Enhanced tincture potency", build.HasNaturesConcoction, optional: true);
        y += 8;

        DrawTinctureCountStatus(dl, x, ref y, width, tinctureCount, 2, build.MaxActiveTinctures);
    }

    private void GuideWardenSingle(ImDrawListPtr dl, BuildScanResult build,
        float x, ref float y, float width, int tinctureCount)
    {
        dl.AddText(new Vector2(x, y), Accent, "Warden Single Tincture - Linger Management");
        y += 24;

        DrawSectionHeader(dl, x, width, ref y, "How It Works");
        DrawInfoLines(dl, x, ref y, Label,
            "Single tincture with Enduring Suffusion linger window.",
            "Deactivate at stack threshold. Linger effect continues for ~6s.",
            "Tincture cooldown finishes as linger fades - reactivate immediately.",
            "Near-100% uptime achievable with proper cooldown recovery rate.");

        DrawSectionHeader(dl, x, width, ref y, "Required Setup");
        DrawPassiveCheckItem(dl, x, ref y, "Warden Ascendancy",
            "Must be Warden class (Ranger ascendancy)", build.HasWardenAscendancy);
        DrawPassiveCheckItem(dl, x, ref y, "Enduring Suffusion",
            "Tincture effects linger 6s after deactivation", build.HasEnduringSuffusion);
        DrawPassiveCheckItem(dl, x, ref y, "Bloodsoaked Blade",
            "Burns life instead of mana (tincture_life_burn vs tincture_mana_burn)",
            build.HasBloodsoackedBlade, optional: true);
        y += 8;

        DrawSectionHeader(dl, x, width, ref y, "Optional Notables");
        DrawPassiveCheckItem(dl, x, ref y, "Experienced Herbalist",
            "Allocate to enable dual tincture rotation", build.HasExperiencedHerbalist, optional: true);
        DrawPassiveCheckItem(dl, x, ref y, "Seasoned Hunter",
            "Additional tincture effect bonuses", build.HasSeasonedHunter, optional: true);
        DrawPassiveCheckItem(dl, x, ref y, "Nature's Concoction",
            "Enhanced tincture potency", build.HasNaturesConcoction, optional: true);
        y += 8;

        DrawTinctureCountStatus(dl, x, ref y, width, tinctureCount, 1, build.MaxActiveTinctures);
    }

    private void GuideWardenTriple(ImDrawListPtr dl, BuildScanResult build,
        float x, ref float y, float width, int tinctureCount)
    {
        dl.AddText(new Vector2(x, y), Accent, "Warden Triple Tincture - Advanced Rotation");
        y += 24;

        DrawSectionHeader(dl, x, width, ref y, "How It Works");
        DrawInfoLines(dl, x, ref y, Label,
            "Three tinctures with priority-based rotation. Advanced setup.",
            "Plugin activates up to max active count, ordered by activation priority.",
            "Deactivation follows priority order (lower = deactivated first).",
            "Linger window covers cooldown for seamless transitions.",
            "Use per-tincture priority sliders on the Tinctures tab to control order.");

        DrawSectionHeader(dl, x, width, ref y, "Required Setup");
        DrawPassiveCheckItem(dl, x, ref y, "Warden Ascendancy",
            "Must be Warden class (Ranger ascendancy)", build.HasWardenAscendancy);
        DrawPassiveCheckItem(dl, x, ref y, "Enduring Suffusion",
            "Tincture effects linger 6s after deactivation", build.HasEnduringSuffusion);
        DrawPassiveCheckItem(dl, x, ref y, "Experienced Herbalist",
            "Allows equipping two tinctures simultaneously", build.HasExperiencedHerbalist);
        DrawPassiveCheckItem(dl, x, ref y, "Bloodsoaked Blade",
            "Burns life instead of mana (tincture_life_burn vs tincture_mana_burn)",
            build.HasBloodsoackedBlade, optional: true);
        y += 8;

        DrawSectionHeader(dl, x, width, ref y, "Third Tincture Source");
        DrawInfoLines(dl, x, ref y, Desc,
            "Third tincture slot requires The Living Blade unique sword or similar item.",
            "Plugin auto-detects all equipped tinctures regardless of source.");
        y += 4;

        DrawSectionHeader(dl, x, width, ref y, "Optional Notables");
        DrawPassiveCheckItem(dl, x, ref y, "Seasoned Hunter",
            "Additional tincture effect bonuses", build.HasSeasonedHunter, optional: true);
        DrawPassiveCheckItem(dl, x, ref y, "Nature's Concoction",
            "Enhanced tincture potency", build.HasNaturesConcoction, optional: true);
        y += 8;

        DrawTinctureCountStatus(dl, x, ref y, width, tinctureCount, 3, build.MaxActiveTinctures);
    }

    private void GuideNonWardenSingle(ImDrawListPtr dl, BuildScanResult build,
        float x, ref float y, float width, int tinctureCount)
    {
        dl.AddText(new Vector2(x, y), Accent, "Single Tincture - Simple On/Off");
        y += 24;

        DrawSectionHeader(dl, x, width, ref y, "How It Works");
        DrawInfoLines(dl, x, ref y, Label,
            "Simple on/off management. Activate when enemies are nearby.",
            "Deactivate at burn stack threshold to prevent excessive life/mana loss.",
            "No linger mechanic - cooldown gap is unavoidable, downtime is expected.",
            "Plugin handles activation and emergency deactivation automatically.");

        DrawSectionHeader(dl, x, width, ref y, "Upgrade Path");
        DrawInfoLines(dl, x, ref y, Desc,
            "Consider Warden ascendancy (Ranger) for Enduring Suffusion linger,",
            "which enables near-100% uptime by covering the cooldown gap.");
        y += 4;

        DrawPassiveCheckItem(dl, x, ref y, "Warden Ascendancy",
            "Not detected - any class can use a single tincture", build.HasWardenAscendancy, optional: true);
        y += 8;

        DrawTinctureCountStatus(dl, x, ref y, width, tinctureCount, 1, build.MaxActiveTinctures);
    }

    private void GuideNonWardenDual(ImDrawListPtr dl, BuildScanResult build,
        float x, ref float y, float width, int tinctureCount)
    {
        dl.AddText(new Vector2(x, y), Accent, "Dual Tincture - No Linger");
        y += 24;

        DrawSectionHeader(dl, x, width, ref y, "How It Works");
        DrawInfoLines(dl, x, ref y, Label,
            "Two tinctures sharing one burn stack pool, no linger mechanic.",
            "Deactivating any tincture puts all tinctures on shared cooldown.",
            "Significant downtime during cooldown periods is expected.",
            "Plugin manages activation order and emergency deactivation.");

        DrawSectionHeader(dl, x, width, ref y, "Dual Tincture Source");
        DrawInfoLines(dl, x, ref y, Desc,
            "Dual tinctures without Experienced Herbalist likely via The Living Blade",
            "unique sword, which grants an additional tincture slot.");
        y += 4;

        DrawSectionHeader(dl, x, width, ref y, "Upgrade Path");
        DrawInfoLines(dl, x, ref y, Desc,
            "Consider Warden ascendancy (Ranger) for Enduring Suffusion linger,",
            "which enables near-100% uptime by covering the cooldown gap.");
        y += 4;

        DrawPassiveCheckItem(dl, x, ref y, "Warden Ascendancy",
            "Not detected - enables linger for uptime", build.HasWardenAscendancy, optional: true);
        DrawPassiveCheckItem(dl, x, ref y, "Enduring Suffusion",
            "Not detected - tincture effects would linger after deactivation",
            build.HasEnduringSuffusion, optional: true);
        y += 8;

        DrawTinctureCountStatus(dl, x, ref y, width, tinctureCount, 2, build.MaxActiveTinctures);
    }

    private static void GuideNone(ImDrawListPtr dl, float x, ref float y)
    {
        dl.AddText(new Vector2(x, y), Accent, "No Tinctures Detected");
        y += 24;

        DrawInfoLines(dl, x, ref y, Label,
            "No tinctures found in the flask belt.",
            "Equip one or more tinctures, then click Rescan on the Tinctures tab.",
            "The plugin will auto-detect your setup and configure smart defaults.");
    }

    private static void DrawTinctureCountStatus(ImDrawListPtr dl, float x, ref float y,
        float width, int tinctureCount, int needed, int maxActive)
    {
        DrawSectionHeader(dl, x, width, ref y, "Tinctures Needed");
        bool enough = tinctureCount >= needed;
        string status = enough
            ? $"{needed} tincture{(needed != 1 ? "s" : "")} equipped ({tinctureCount} found, max active: {maxActive})"
            : $"Need {needed} tinctures - only {tinctureCount} found";
        uint statusCol = enough ? Col(0, 1, 0.5f) : Col(1, 0.27f, 0.27f);
        dl.AddCircleFilled(new Vector2(x + 6, y + 8), 5f, statusCol);
        dl.AddText(new Vector2(x + 18, y + 1), Label, status);
        y += Row;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static void DrawInfoLines(ImDrawListPtr dl, float x, ref float y, uint color, params string[] lines)
    {
        foreach (var line in lines)
        {
            dl.AddText(new Vector2(x + 4, y), color, line);
            y += 16;
        }
        y += 8;
    }

    private static bool NeonButton(string id, string text, ImDrawListPtr dl, float x, ref float y)
    {
        var textSize = ImGui.CalcTextSize(text);
        float btnW = textSize.X + 20;
        const float btnH = 22;
        var min = new Vector2(x, y);
        var max = new Vector2(x + btnW, y + btnH);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, max - min);
        bool hov = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();

        dl.AddRectFilled(min, max, hov ? WithAlpha(Accent, 0.2f) : WithAlpha(Accent, 0.1f), 3f);
        dl.AddRect(min, max, WithAlpha(Accent, hov ? 0.7f : 0.4f), 3f);
        CenterText(dl, text, (min + max) * 0.5f, Accent);

        y += btnH + 6;
        return clicked;
    }

    private static bool SmallNeonButton(string id, string text, ImDrawListPtr dl, float x, float y)
    {
        var textSize = ImGui.CalcTextSize(text);
        float btnW = textSize.X + 12;
        const float btnH = 18;
        var min = new Vector2(x, y);
        var max = new Vector2(x + btnW, y + btnH);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, max - min);
        bool hov = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();

        dl.AddRectFilled(min, max, hov ? WithAlpha(Accent, 0.18f) : WithAlpha(Accent, 0.08f), 3f);
        dl.AddRect(min, max, WithAlpha(Accent, hov ? 0.6f : 0.3f), 3f);
        CenterText(dl, text, (min + max) * 0.5f, hov ? Accent : AccentDim);

        return clicked;
    }

    private static void DrawSectionHeader(ImDrawListPtr dl, float x, float width, ref float y, string title)
    {
        dl.AddText(new Vector2(x, y + 2), Accent, title);
        y += 18;
        dl.AddLine(new Vector2(x, y), new Vector2(x + width * 0.9f, y), WithAlpha(Accent, 0.15f), 1f);
        y += 8;
    }

    private static void DrawPassiveCheckItem(ImDrawListPtr dl, float x, ref float y,
        string name, string desc, bool allocated, bool optional = false)
    {
        uint circleCol = allocated ? Col(0, 1, 0.5f) : (optional ? Col(1, 0.72f, 0.19f) : Col(1, 0.27f, 0.27f));
        dl.AddCircleFilled(new Vector2(x + 6, y + 8), 5f, circleCol);
        dl.AddText(new Vector2(x + 18, y + 1), Label, name);
        dl.AddText(new Vector2(x + 18, y + 16), Desc, desc);
        y += Row;
    }

    // ── Widget primitives ───────────────────────────────────────────

    private void Toggle(string key, ToggleNode node, ImDrawListPtr dl,
        float x, float cx, ref float y, string label, string desc)
    {
        dl.AddText(new Vector2(x, y + 1), Label, label);
        dl.AddText(new Vector2(x, y + 16), Desc, desc);
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 5));
        var v = node.Value;
        _anims.TryGetValue(key, out float a);
        if (PillToggle($"##{key}", ref v, ref a))
            node.Value = v;
        _anims[key] = a;
        y += Row;
    }

    private void IntSlider(string key, RangeNode<int> node, ImDrawListPtr dl,
        float x, float cx, ref float y, float sw, string label, string desc)
    {
        dl.AddText(new Vector2(x, y + 1), Label, label);
        dl.AddText(new Vector2(x, y + 16), Desc, desc);
        ImGui.SetCursorScreenPos(new Vector2(cx, y + 7));
        var v = node.Value;
        if (SliderInt($"##{key}", ref v, node.Min, node.Max, sw))
            node.Value = v;
        dl.AddText(new Vector2(cx + sw + 6, y + 7), Accent, v.ToString());
        y += Row;
    }

    // ── Self-contained drawing helpers ──────────────────────────────

    private static bool PillToggle(string id, ref bool value, ref float animState)
    {
        var dl = ImGui.GetWindowDrawList();
        var cur = ImGui.GetCursorScreenPos();
        const float w = 40f, h = 20f, r = h * 0.5f;

        ImGui.InvisibleButton(id, new Vector2(w, h));
        bool changed = ImGui.IsItemClicked();
        if (changed) value = !value;

        float dt = ImGui.GetIO().DeltaTime;
        float target = value ? 1f : 0f;
        animState = Math.Clamp(animState + (target - animState) * Math.Min(dt * 10f, 1f), 0f, 1f);

        uint trackOff = ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 1f));
        uint trackColor = LerpCol(trackOff, Accent, animState);
        dl.AddRectFilled(cur, cur + new Vector2(w, h), trackColor, r);

        float knobX = cur.X + r + animState * (w - h);
        uint knobCol = ImGui.GetColorU32(new Vector4(0.5f + 0.5f * animState, 0.5f + 0.5f * animState, 0.5f + 0.5f * animState, 1f));
        dl.AddCircleFilled(new Vector2(knobX, cur.Y + r), r - 2f, knobCol);

        return changed;
    }

    private static bool SliderInt(string id, ref int value, int min, int max, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var cur = ImGui.GetCursorScreenPos();
        const float h = 16f, th = 4f, tr = 7f;

        ImGui.InvisibleButton(id, new Vector2(width, h));
        bool changed = false;
        if (ImGui.IsItemActive())
        {
            float frac = Math.Clamp((ImGui.GetMousePos().X - cur.X) / width, 0f, 1f);
            int nv = min + (int)(frac * (max - min));
            if (nv != value) { value = nv; changed = true; }
        }

        float trackY = cur.Y + (h - th) * 0.5f;
        dl.AddRectFilled(cur with { Y = trackY }, new Vector2(cur.X + width, trackY + th),
            ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f)), 2f);

        float f = (max > min) ? (float)(value - min) / (max - min) : 0f;
        float fw = f * width;
        dl.AddRectFilled(cur with { Y = trackY }, new Vector2(cur.X + fw, trackY + th), Accent, 2f);

        float tx = cur.X + fw, ty = cur.Y + h * 0.5f;
        dl.AddCircleFilled(new Vector2(tx, ty), tr, Accent);
        dl.AddCircleFilled(new Vector2(tx, ty), tr - 2f,
            ImGui.GetColorU32(new Vector4(0.95f, 0.95f, 0.95f, 1f)));

        return changed;
    }

    // ── Color utilities ─────────────────────────────────────────────

    private static uint Col(float r, float g, float b, float a = 1f)
        => ImGui.GetColorU32(new Vector4(r, g, b, a));

    private static uint WithAlpha(uint color, float alpha)
    {
        var v = ImGui.ColorConvertU32ToFloat4(color);
        v.W = alpha;
        return ImGui.GetColorU32(v);
    }

    private static uint LerpCol(uint a, uint b, float t)
    {
        var va = ImGui.ColorConvertU32ToFloat4(a);
        var vb = ImGui.ColorConvertU32ToFloat4(b);
        return ImGui.GetColorU32(new Vector4(
            va.X + (vb.X - va.X) * t, va.Y + (vb.Y - va.Y) * t,
            va.Z + (vb.Z - va.Z) * t, va.W + (vb.W - va.W) * t));
    }

    private static void CenterText(ImDrawListPtr dl, string text, Vector2 center, uint color)
    {
        var sz = ImGui.CalcTextSize(text);
        dl.AddText(center - sz * 0.5f, color, text);
    }
}
