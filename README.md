# WhatsATincture

ExileApi plugin for managing dual tinctures (Experienced Herbalist allows 2 simultaneous).

## Mechanics

### Tincture Life Burn (Bloodsoaked Blade)
- `tincture_life_burn` stacks are **shared** across all active tinctures
- Stacks build while ANY tincture is active
- Stacks only reset when ALL tinctures are off AND residual expires

### Enduring Suffusion Residual
- When a tincture deactivates, its effects linger for `stacks × 0.5s`
- At 12 stacks: 6s residual. At 50 stacks: 25s residual
- The residual per stack is configurable (default 0.5s)

### Normal Cycle
1. Both tinctures off, stacks = 0 → activate Sap, then Rose (~400ms apart)
2. Both on, stacks building → at 12: deactivate Rose, then Sap
3. Both off → residual lingers → stacks clear → repeat

### Unique Cycle
- Rose deactivates at 12 stacks, Sap stays until 50
- Rose rapid-toggles on/off for brief uptime + residual each cycle
- At 50 stacks → deactivate Sap → both off → stacks clear → repeat

### HP Safety
- Below HP threshold: deactivate tinctures immediately
- Normal HP threshold: 30% (configurable)
- Unique encounter HP threshold: 60% (configurable)

## Why a Plugin Instead of ReAgent Script?

| Feature | ReAgent Script | This Plugin |
|---------|---------------|-------------|
| Residual tracking | Approximated via flask cooldown | Exact: stacks × 0.5s from deactivation timestamp |
| State persistence | None (stateless per frame) | Fields survive across ticks |
| Double-toggle prevention | F24/F23 virtual key hack | DateTime guard with configurable cooldown |
| Settings | Hardcoded constants | UI-configurable settings panel |

## Prerequisites

- Warden ascendancy with **Enduring Suffusion**
- **Experienced Herbalist** + **Seasoned Hunter**
- **Bloodsoaked Blade** keystone (for `tincture_life_burn` stacks)

## Setup

1. Build the plugin or place compiled output in `Plugins/Compiled/WhatsATincture/`
2. Enable "WhatsATincture" in the ExileApi plugin list
3. Configure tincture names in settings to match your items:
   - **Tincture1Name**: exact match (default: "Sap of the Seasons")
   - **Tincture2Match**: contains match (default: "Rosethorn")
4. Equip both tinctures in your flask belt
5. Enter a map — both tinctures will activate when enemies are nearby

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Tincture1Name | Sap of the Seasons | Primary tincture (exact name match) |
| Tincture2Match | Rosethorn | Secondary tincture (contains match) |
| BurnBuffName | tincture_life_burn | Buff name for burn stacks |
| StacksNormal | 12 | Deactivate threshold (normal) |
| StacksUniqueSap | 50 | Sap threshold vs unique enemies |
| HpMinNormal | 30% | HP threshold (normal) |
| HpMinUnique | 60% | HP threshold (unique nearby) |
| ActivationRange | 80 | Enemy detection range (grid units) |
| ResidualPerStack | 0.5s | Residual duration per stack |
| ToggleCooldownMs | 400ms | Minimum time between toggles |
| ShowHud | true | Show HUD overlay |
| HudX / HudY | 500 / 10 | HUD position |
| BarWidth / BarHeight | 220 / 22 | HUD bar dimensions |
