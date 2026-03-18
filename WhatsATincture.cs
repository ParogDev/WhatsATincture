using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using SharpDX;

namespace WhatsATincture;

public class WhatsATincture : BaseSettingsPlugin<WhatsATinctureSettings>
{
    private readonly Dictionary<int, TinctureRuntimeState> _runtime = new();
    private readonly Dictionary<int, ServerInventory.InventSlotItem> _slotItems = new();

    private int _framesAfterAreaChange;
    private const int AreaChangeGuardFrames = 5;
    private const double PendingToggleTimeoutSeconds = 1.5;
    private const string GracePeriodBuffName = "grace_period";

    private DateTime _lastUniqueSeenAt;
    private const double UniqueStickySeconds = 2.0;

    private DateTime _lastKeyPressAt;

    // Cached from Tick() for Render() — avoids double monster scan
    private bool _cachedUniqueNearby;
    private bool _cachedEnemiesNearby;

    private TinctureSettingsUi _settingsUi;
    private BuildScanResult _buildScan = new();

    private static readonly HashSet<string> HighStacksTinctures = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sap of the Seasons",
        "Wildfire Phloem"
    };

    public override bool Initialise()
    {
        Settings.Thresholds ??= new GlobalThresholds();
        Settings.Behavior ??= new BehaviorSettings();
        Settings.Hud ??= new HudSettings();
        Settings.Tinctures ??= new Dictionary<int, TinctureSlotConfig>();

        _framesAfterAreaChange = AreaChangeGuardFrames;
        AutoDetectTinctures();

        if (Settings.SettingsVersion < 1)
            Settings.SettingsVersion = 1;

        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _framesAfterAreaChange = 0;
        AutoDetectTinctures();
    }

    private void AutoDetectTinctures()
    {
        _slotItems.Clear();
        _runtime.Clear();

        var flasks = ScanFlaskBelt();
        _buildScan = ScanPassiveTree();

        var foundSlots = new HashSet<int>();

        foreach (var flask in flasks.Where(f => f.IsTincture))
        {
            foundSlots.Add(flask.SlotIndex);

            if (!Settings.Tinctures.TryGetValue(flask.SlotIndex, out var config))
            {
                config = new TinctureSlotConfig();
                ApplySmartDefaults(config, flask);
                Settings.Tinctures[flask.SlotIndex] = config;
            }

            config.DisplayName = flask.DisplayName;
            config.BaseName = flask.BaseName;
            config.UniqueName = flask.UniqueName;

            _runtime[flask.SlotIndex] = new TinctureRuntimeState();
        }

        CacheSlotItems();

        var staleSlots = Settings.Tinctures.Keys.Where(k => !foundSlots.Contains(k)).ToList();
        foreach (var slot in staleSlots)
            Settings.Tinctures.Remove(slot);

        int tinctureCount = foundSlots.Count;
        _buildScan.MaxActiveTinctures = _buildScan.HasExperiencedHerbalist ? 2 : 1;
        _buildScan.HasLingerMechanic = _buildScan.HasEnduringSuffusion;
        _buildScan.DetectedScenario = ClassifyScenario(tinctureCount, _buildScan);

        SyncBurnBuff();
    }

    private static void ApplySmartDefaults(TinctureSlotConfig config, FlaskSlotInfo flask)
    {
        bool isHighStacks = HighStacksTinctures.Contains(flask.UniqueName) ||
                            HighStacksTinctures.Contains(flask.DisplayName);

        config.HighStacksMode = isHighStacks;
        config.DeactivationPriority = isHighStacks ? 90 : 50;
        config.ActivationPriority = isHighStacks ? 10 : 50;
    }

    private void CacheSlotItems()
    {
        try
        {
            if (!GameController.InGame) return;

            var flaskHolder = GameController.IngameState.ServerData.PlayerInventories
                .LastOrDefault(x => x.TypeId == InventoryNameE.Flask1);
            if (flaskHolder?.Inventory == null) return;

            foreach (var slot in Settings.Tinctures.Keys)
            {
                var slotItem = flaskHolder.Inventory[slot, 0];
                if (slotItem?.Item?.Address is not null and not 0)
                    _slotItems[slot] = slotItem;
            }
        }
        catch (Exception ex)
        {
            LogError($"CacheSlotItems: {ex.Message}");
        }
    }

    private static TinctureScenario ClassifyScenario(int tinctureCount, BuildScanResult build)
    {
        if (tinctureCount == 0) return TinctureScenario.None;

        return (tinctureCount, build.HasLingerMechanic) switch
        {
            (1, false) => TinctureScenario.NonWardenSingle,
            (1, true) => TinctureScenario.WardenSingle,
            (2, false) => TinctureScenario.NonWardenDual,
            (2, true) => TinctureScenario.WardenDual,
            (>= 3, true) => TinctureScenario.WardenTriple,
            (>= 3, false) => TinctureScenario.NonWardenDual,
            _ => TinctureScenario.None
        };
    }

    private void SyncBurnBuff()
    {
        Settings.BurnBuffName.Value = _buildScan.HasBloodsoackedBlade
            ? "tincture_life_burn"
            : "tincture_mana_burn";
    }

    public override Job Tick()
    {
        if (!GameController.InGame) return null;

        if (Settings.Tinctures.Count == 0)
        {
            AutoDetectTinctures();
            if (Settings.Tinctures.Count == 0) return null;
        }

        try
        {
            DoTinctureLogic();
        }
        catch (Exception ex)
        {
            LogError($"Tick: {ex.Message}");
        }

        return null;
    }

    private static bool IsTinctureActive(List<Buff> buffs, int slotIndex)
    {
        if (slotIndex < 0) return false;
        for (int i = 0; i < buffs.Count; i++)
        {
            var b = buffs[i];
            if (b.Name == "tincture_parent_buff" && b.FlaskSlot == slotIndex)
                return true;
        }
        return false;
    }

    private float ComputeCooldownDuration(int slotIndex)
    {
        if (!_slotItems.TryGetValue(slotIndex, out var slotItem)) return 0f;
        if (slotItem?.Item == null) return 0f;

        var tincture = slotItem.Item.GetComponent<Tincture>();
        if (tincture?.TinctureDat == null) return 0f;

        var player = GameController.Player;
        float playerCdr = player?.GetComponent<Stats>()?.StatDictionary
            .GetValueOrDefault(GameStat.TinctureCooldownRecoveryPct) ?? 0;
        float itemCdr = slotItem.Item.GetComponent<LocalStats>()?.StatDictionary
            .GetValueOrDefault(GameStat.LocalTinctureCooldownRecoveryPct) ?? 0;

        float cdrMultiplier = (100f + playerCdr + itemCdr) / 100f;
        return tincture.TinctureDat.Cooldown / 1000f / cdrMultiplier;
    }

    private float GetCooldownRemaining(int slotIndex)
    {
        if (!_runtime.TryGetValue(slotIndex, out var state)) return 0f;
        return Math.Max(0f, (float)(state.ReadyAt - DateTime.UtcNow).TotalSeconds);
    }

    private bool IsSlotPendingToggle(int slotIndex, bool observedActive)
    {
        if (!_runtime.TryGetValue(slotIndex, out var state) || !state.HasPendingToggle)
            return false;

        if (observedActive == state.PendingExpectedActive)
        {
            state.HasPendingToggle = false;
            return false;
        }

        if ((DateTime.UtcNow - state.PendingPressedAt).TotalSeconds >= PendingToggleTimeoutSeconds)
        {
            state.HasPendingToggle = false;

            // Failed activation — tincture is likely on cooldown in-game.
            // Set a backoff based on the computed cooldown duration to stop key spam.
            if (state.PendingExpectedActive)
            {
                float cd = ComputeCooldownDuration(slotIndex);
                state.ReadyAt = DateTime.UtcNow.AddSeconds(cd > 0 ? cd : 5.0);
            }

            return false;
        }

        return true;
    }

    private void ProcessSlotTransitions(Dictionary<int, bool> currentActive)
    {
        foreach (var (slot, isActive) in currentActive)
        {
            if (!_runtime.TryGetValue(slot, out var state)) continue;

            bool wasOn = state.WasActive;
            state.WasActive = isActive;

            // active → inactive: shared cooldown on ALL tracked slots
            if (wasOn && !isActive)
            {
                float cd = ComputeCooldownDuration(slot);

                // If the game deactivated the tincture on its own (no pending deactivation
                // from us), apply extra backoff. This happens when mana/life runs out.
                bool gameForced = !state.HasPendingToggle || state.PendingExpectedActive;
                float backoff = gameForced ? Math.Max(cd, 3.0f) : cd;

                var readyTime = DateTime.UtcNow.AddSeconds(backoff);
                foreach (var rt in _runtime.Values)
                    rt.ReadyAt = readyTime;
            }
            // inactive → active: clear cooldown for this slot
            else if (!wasOn && isActive)
            {
                state.ReadyAt = DateTime.MinValue;
            }
        }
    }

    private int GetEffectiveThreshold(TinctureSlotConfig config, bool uniqueNearby)
    {
        if (uniqueNearby)
        {
            if (config.StackThresholdUniqueOverride >= 0) return config.StackThresholdUniqueOverride;
            if (config.HighStacksMode) return Settings.Thresholds.StacksUnique.Value;
            return Settings.Thresholds.StacksNormal.Value;
        }

        if (config.StackThresholdOverride >= 0) return config.StackThresholdOverride;
        return Settings.Thresholds.StacksNormal.Value;
    }

    private void DoTinctureLogic()
    {
        if (_framesAfterAreaChange < AreaChangeGuardFrames)
        {
            _framesAfterAreaChange++;
            return;
        }

        var player = GameController.Player;
        if (player == null) return;

        var buffs = player.GetComponent<Buffs>()?.BuffsList;
        if (buffs == null) return;

        // Mandatory safety: never act during grace period (spawn immunity)
        for (int i = 0; i < buffs.Count; i++)
        {
            if (buffs[i].Name == GracePeriodBuffName)
                return;
        }

        // Read active state for all tracked slots
        var activeStates = new Dictionary<int, bool>();
        foreach (var slot in Settings.Tinctures.Keys)
            activeStates[slot] = IsTinctureActive(buffs, slot);

        ProcessSlotTransitions(activeStates);

        // Resolve pending toggles
        var pendingSlots = new HashSet<int>();
        foreach (var slot in Settings.Tinctures.Keys)
        {
            if (IsSlotPendingToggle(slot, activeStates[slot]))
                pendingSlots.Add(slot);
        }

        // Burn stacks (shared pool)
        Buff burnBuff = null;
        for (int i = 0; i < buffs.Count; i++)
        {
            if (buffs[i].Name == Settings.BurnBuffName.Value)
            {
                burnBuff = buffs[i];
                break;
            }
        }
        int stacks = burnBuff?.BuffCharges ?? 0;
        bool burnExists = burnBuff != null;

        // Resource check — life burn checks HP, mana burn checks mana
        var life = player.GetComponent<Life>();
        if (life == null) return;
        bool isMana = !_buildScan.HasBloodsoackedBlade;
        double resourcePct = isMana
            ? life.MPPercentage * 100
            : life.HPPercentage * 100;

        // Monster detection — cached for Render()
        bool uniqueNearby = false;
        bool enemiesNearby = false;
        int range = Settings.Behavior.ActivationRange.Value;
        var now = DateTime.UtcNow;

        if (GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Monster, out var monsters))
        {
            foreach (var m in monsters)
            {
                if (!m.IsAlive || !m.IsHostile) continue;
                if (m.DistancePlayer > range) continue;
                enemiesNearby = true;
                if (m.Rarity == MonsterRarity.Unique)
                {
                    uniqueNearby = true;
                    _lastUniqueSeenAt = now;
                }
            }
        }

        if (!uniqueNearby && (now - _lastUniqueSeenAt).TotalSeconds < UniqueStickySeconds)
            uniqueNearby = true;

        _cachedUniqueNearby = uniqueNearby;
        _cachedEnemiesNearby = enemiesNearby;

        // Decision: one action per tick
        // minResource applies to whichever resource the tincture burns (life or mana)
        double minResource = uniqueNearby ? Settings.Thresholds.HpMinUnique.Value : Settings.Thresholds.HpMinNormal.Value;

        int toggleSlot = -1;
        bool toggleExpectsActive = false;

        // Sorted slot lists for priority-based decisions
        var activeSlotsOrdered = Settings.Tinctures
            .Where(kv => activeStates.GetValueOrDefault(kv.Key) && !pendingSlots.Contains(kv.Key))
            .OrderBy(kv => kv.Value.DeactivationPriority)
            .Select(kv => kv.Key)
            .ToList();

        var readySlotsOrdered = Settings.Tinctures
            .Where(kv => !activeStates.GetValueOrDefault(kv.Key) && !pendingSlots.Contains(kv.Key)
                         && GetCooldownRemaining(kv.Key) <= 0)
            .OrderBy(kv => kv.Value.ActivationPriority)
            .Select(kv => kv.Key)
            .ToList();

        // Priority 1: Resource emergency - deactivate (lowest deactivation priority first)
        if (resourcePct < minResource && activeSlotsOrdered.Count > 0)
        {
            toggleSlot = activeSlotsOrdered[0];
            toggleExpectsActive = false;
        }
        // Priority 2: Stack threshold - deactivate slots exceeding effective threshold
        else if (resourcePct >= minResource)
        {
            foreach (var slot in activeSlotsOrdered)
            {
                int threshold = GetEffectiveThreshold(Settings.Tinctures[slot], uniqueNearby);
                if (stacks >= threshold)
                {
                    toggleSlot = slot;
                    toggleExpectsActive = false;
                    break;
                }
            }
        }

        // Priority 3: Activation - if no deactivation needed
        if (toggleSlot < 0 && enemiesNearby && resourcePct > minResource && readySlotsOrdered.Count > 0)
        {
            int activeCount = activeStates.Count(kv => kv.Value);
            if (activeCount < _buildScan.MaxActiveTinctures)
            {
                bool allOff = activeCount == 0;
                bool canActivate = allOff
                    ? !burnExists
                    : (uniqueNearby || stacks < Settings.Thresholds.StacksNormal.Value);

                if (canActivate)
                {
                    toggleSlot = readySlotsOrdered[0];
                    toggleExpectsActive = true;
                }
            }
        }

        // Execute toggle — only if the game can receive input
        if (toggleSlot >= 0 && CanSendInput())
        {
            // Enforce minimum interval between key presses (ToggleCooldownMs)
            if ((now - _lastKeyPressAt).TotalMilliseconds < Settings.Behavior.ToggleCooldownMs.Value)
                return;

            var key = Keys.D1 + toggleSlot;
            Core.ParallelRunner.Run(new Coroutine(Input.KeyPress(key), this, "WhatsATincture.Toggle"));
            _lastKeyPressAt = now;

            if (_runtime.TryGetValue(toggleSlot, out var state))
            {
                state.HasPendingToggle = true;
                state.PendingExpectedActive = toggleExpectsActive;
                state.PendingPressedAt = now;
            }
        }
    }

    private bool CanSendInput()
    {
        try
        {
            // Game window must be in foreground
            if (!GameController.Window.IsForeground())
                return false;

            var ingameUi = GameController.IngameState.IngameUi;

            // Chat must not be open (keystrokes would type into chat)
            if (ingameUi.ChatPanel?.ChatTitlePanel?.IsVisible == true)
                return false;

            // Block during fullscreen panels (skill tree, atlas, syndicate board)
            if (ingameUi.FullscreenPanels.Any(x => x.IsVisible))
                return false;

            // Block during large panels (vendor, trade, map device, crafting bench)
            if (ingameUi.LargePanels.Any(x => x.IsVisible))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private List<FlaskSlotInfo> ScanFlaskBelt()
    {
        var result = new List<FlaskSlotInfo>();
        try
        {
            if (!GameController.InGame) return result;

            var flaskHolder = GameController.IngameState.ServerData.PlayerInventories
                .LastOrDefault(x => x.TypeId == InventoryNameE.Flask1);
            if (flaskHolder?.Inventory == null) return result;

            for (int i = 0; i < 5; i++)
            {
                var slotItem = flaskHolder.Inventory[i, 0];
                if (slotItem?.Item?.Address is null or 0) continue;

                var item = slotItem.Item;
                var baseName = item.GetComponent<Base>()?.Name ?? "";
                var uniqueName = item.GetComponent<Mods>()?.UniqueName ?? "";
                var displayName = !string.IsNullOrEmpty(uniqueName) ? uniqueName : baseName;
                bool isTincture = item.GetComponent<Tincture>() != null;

                result.Add(new FlaskSlotInfo(i, displayName, baseName, uniqueName, isTincture));
            }
        }
        catch (Exception ex)
        {
            LogError($"ScanFlaskBelt: {ex.Message}");
        }
        return result;
    }

    private BuildScanResult ScanPassiveTree()
    {
        var result = new BuildScanResult();
        try
        {
            if (!GameController.InGame) return result;

            var passiveIds = GameController.IngameState.ServerData.PassiveSkillIds;
            if (passiveIds == null) return result;

            var files = GameController.Files.PassiveSkills;

            foreach (var id in passiveIds)
            {
                var passive = files.GetPassiveSkillByPassiveId(id);
                if (passive == null) continue;

                var name = passive.Name;
                if (string.IsNullOrEmpty(name)) continue;

                if (passive.IsAscendancyStartingNode)
                {
                    result.AscendancyClassName = name;
                    if (name.IndexOf("Warden", StringComparison.OrdinalIgnoreCase) >= 0)
                        result.HasWardenAscendancy = true;
                }

                if (passive.IsNotable)
                    result.AllocatedNotableNames.Add(name);

                switch (name)
                {
                    case "Enduring Suffusion": result.HasEnduringSuffusion = true; break;
                    case "Experienced Herbalist": result.HasExperiencedHerbalist = true; break;
                    case "Bloodsoaked Blade": result.HasBloodsoackedBlade = true; break;
                    case "Seasoned Hunter": result.HasSeasonedHunter = true; break;
                    case "Nature's Concoction": result.HasNaturesConcoction = true; break;
                }
            }

            if (!result.HasWardenAscendancy)
            {
                result.HasWardenAscendancy = result.HasEnduringSuffusion || result.HasExperiencedHerbalist ||
                    result.HasBloodsoackedBlade || result.HasSeasonedHunter || result.HasNaturesConcoction;
                if (result.HasWardenAscendancy)
                    result.AscendancyClassName = "Warden";
            }
        }
        catch (Exception ex)
        {
            LogError($"ScanPassiveTree: {ex.Message}");
        }
        return result;
    }

    public override void DrawSettings()
    {
        _settingsUi ??= new TinctureSettingsUi();
        _settingsUi.Draw(Settings, _buildScan, () => AutoDetectTinctures());
    }

    public override void Render()
    {
        if (!Settings.Hud.ShowHud.Value || !GameController.InGame) return;
        if (Settings.Tinctures.Count == 0) return;

        var buffs = GameController.Player?.GetComponent<Buffs>()?.BuffsList;
        if (buffs == null) return;

        Buff burnBuff = null;
        for (int i = 0; i < buffs.Count; i++)
        {
            if (buffs[i].Name == Settings.BurnBuffName.Value)
            {
                burnBuff = buffs[i];
                break;
            }
        }
        int stacks = burnBuff?.BuffCharges ?? 0;

        // Use cached monster state from Tick() — no double scan
        bool uniqueNearby = _cachedUniqueNearby;

        float x = Settings.Hud.HudX.Value;
        float y = Settings.Hud.HudY.Value;
        float w = Settings.Hud.BarWidth.Value;
        float h = Settings.Hud.BarHeight.Value;

        int barIndex = 0;
        foreach (var (slot, config) in Settings.Tinctures.OrderBy(kv => kv.Key))
        {
            bool isActive = IsTinctureActive(buffs, slot);
            int threshold = GetEffectiveThreshold(config, uniqueNearby);
            float cd = GetCooldownRemaining(slot);

            string displayName = config.DisplayName;
            if (displayName.Length > 12) displayName = displayName[..12];

            var barRect = new RectangleF(x, y + barIndex * (h + 4), w, h);
            DrawTinctureBar(barRect, displayName, isActive, stacks, threshold, cd,
                uniqueNearby && config.HighStacksMode);
            barIndex++;
        }
    }

    private void DrawTinctureBar(RectangleF barRect, string name, bool isActive,
        int stacks, int threshold, float cooldownRemaining, bool showUnique)
    {
        float frac;
        Color fillColor;
        string label;

        if (isActive)
        {
            frac = Math.Min((float)stacks / threshold, 1f);
            fillColor = stacks >= threshold ? Color.Red
                      : stacks >= (int)(threshold * 0.7) ? Color.Orange
                      : Color.Green;
            label = $"{name}: {stacks}/{threshold}" + (showUnique ? " [U]" : "");
        }
        else if (cooldownRemaining > 0)
        {
            frac = Math.Min(cooldownRemaining / 6f, 1f);
            fillColor = Color.Orange;
            label = $"{name} CD: {cooldownRemaining:F1}s";
        }
        else
        {
            frac = 0f;
            fillColor = Color.Green;
            label = $"{name}: Ready";
        }

        Graphics.DrawBox(barRect, new Color(50, 50, 50, 200));

        if (frac > 0)
        {
            var fillRect = new RectangleF(barRect.X, barRect.Y, barRect.Width * frac, barRect.Height);
            Graphics.DrawBox(fillRect, fillColor);
        }

        Graphics.DrawFrame(barRect, Color.Gray, 1);

        var textPos = new System.Numerics.Vector2(barRect.X + 4, barRect.Y + 2);
        Graphics.DrawText(label, textPos, Color.White);
    }
}
