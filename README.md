# What's a Tincture?

> Automate your tincture rotation -- burn stacks, HP safety, and enemy detection handled for you.

Part of the **WhatsA** plugin family for ExileApi.

## What It Does

- Auto-detects your equipped tinctures and manages activation/deactivation based on burn stack thresholds
- Monitors HP and immediately deactivates tinctures when you drop below safety thresholds (configurable for normal vs. boss fights)
- Only activates when enemies are nearby, with per-tincture priority ordering for which activates/deactivates first
- Displays status bars for each tincture showing active state, stack count, cooldown, or "Ready"

## Getting Started

1. Download and place in `Plugins/Source/What's a Tincture/`
2. HUD auto-compiles on next launch
3. Enable in plugin list
4. Equip tinctures in your flask belt -- the plugin auto-detects them on zone change
5. Check the Tinctures tab in settings to verify detection and adjust priorities

## Prerequisites

- **Bloodsoaked Blade** keystone (for burn stack tracking)
- Warden ascendancy with **Enduring Suffusion** recommended for linger rotation
- **Experienced Herbalist** if running multiple tinctures

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Normal Stack Limit | 12 | Deactivate tincture at this burn stack count |
| Unique Boss Limit | 50 | Higher threshold for tinctures with High Stacks mode vs. unique enemies |
| Min HP (Normal) | 30% | Emergency deactivation threshold |
| Min HP (Unique) | 60% | Boss fight safety threshold |
| Detection Range | 80 | How far to scan for enemies (grid units) |
| Toggle Cooldown | 400ms | Minimum time between flask key presses |
| Show Overlay | On | Display status bars on screen |
| HUD Position X / Y | 500 / 10 | Status bar screen position |

Per-tincture settings (auto-populated in the Tinctures tab):

| Setting | Default | Description |
|---------|---------|-------------|
| Stack Threshold Override | *(global)* | Per-tincture normal threshold |
| High Stacks Mode | Auto | Use the higher unique threshold vs. unique enemies |
| Deactivation Priority | 50 | Lower = deactivated first |
| Activation Priority | 50 | Lower = activated first |

<details>
<summary>Technical Details</summary>

### Tincture Life Burn (Bloodsoaked Blade)

- `tincture_life_burn` stacks are **shared** across all active tinctures
- Stacks build while ANY tincture is active
- Stacks only reset when ALL tinctures are off AND residual expires

### Enduring Suffusion Residual

- When a tincture deactivates, its effects linger for `stacks x 0.5s`
- At 12 stacks: 6s residual. At 50 stacks: 25s residual
- The residual per stack is configurable (default 0.5s)

### Normal Cycle

1. Both tinctures off, stacks = 0 -> activate Sap, then Rose (~400ms apart)
2. Both on, stacks building -> at 12: deactivate Rose, then Sap
3. Both off -> residual lingers -> stacks clear -> repeat

### Unique Cycle

- Rose deactivates at 12 stacks, Sap stays until 50
- Rose rapid-toggles on/off for brief uptime + residual each cycle
- At 50 stacks -> deactivate Sap -> both off -> stacks clear -> repeat

### HP Safety

- Below HP threshold: deactivate tinctures immediately
- Normal HP threshold: 30% (configurable)
- Unique encounter HP threshold: 60% (configurable)

### Why a Plugin Instead of ReAgent Script?

| Feature | ReAgent Script | This Plugin |
|---------|---------------|-------------|
| Residual tracking | Approximated via flask cooldown | Exact: stacks x 0.5s from deactivation timestamp |
| State persistence | None (stateless per frame) | Fields survive across ticks |
| Double-toggle prevention | F24/F23 virtual key hack | DateTime guard with configurable cooldown |
| Settings | Hardcoded constants | UI-configurable settings panel |

### Build Scenario Classification

The plugin scans your passive tree and flask belt to classify your setup:
- **Warden Dual/Single/Triple** -- Warden ascendancy with Enduring Suffusion
- **Non-Warden Single/Dual** -- Basic tincture support without linger rotation

### Smart Defaults

Known tinctures like Sap of the Seasons and Wildfire Phloem automatically get High Stacks Mode enabled with priority 10 (activated first) and deactivation priority 90 (deactivated last).

### Architecture

- N-tincture state machine with priority-based cascade: HP emergency -> stack threshold -> activation
- Deactivating ANY tincture puts ALL on shared cooldown
- One action per tick to prevent flask spam
- Auto-detection rescans on every zone change (tinctures may be swapped)
- Custom 5-tab ImGui settings UI with per-tincture config cards and scenario-adaptive guide

</details>

## About This Project

These plugins are built with AI-assisted development using Claude Code and the
ExileApiScaffolding (private development workspace) workspace.

The developer works professionally in cybersecurity and high-risk software --
AI compensates for a C# knowledge gap specifically, not engineering judgment.
Plugin data comes from the PoE Wiki and PoEDB data mining.

The focus is on UX: friction points and missing expected features that the
existing plugin ecosystem doesn't address. Every hour spent developing is an
hour not spent on league progression, so feedback is the best way to support
the project.

## WhatsA Plugin Family

| Plugin | Description |
|--------|-------------|
| [What's a Blade Vortex?](https://github.com/ParogDev/WhatsABladeVortex) | Blade Vortex stack tracker with Minion Pact snapshot detection |
| [What's a Breakpoint?](https://github.com/ParogDev/WhatsABreakpoint) | Kinetic Fusillade attack speed breakpoint visualizer |
| [What's a Crowd Control?](https://github.com/ParogDev/WhatsACrowdControl) | OmniCC-style CC effect overlay with timers |
| [What's a Mirage?](https://github.com/ParogDev/WhatsAMirage) | League mechanic overlay for spawners, chests, and wishes |
| **What's a Tincture?** | Automated tincture management with burn stack tracking |
| [What's a Tooltip?](https://github.com/ParogDev/WhatsATooltip) | Shared rich tooltip service for WhatsA plugins |
| [What's an AI Bridge?](https://github.com/ParogDev/WhatsAnAiBridge) | File-based IPC for AI-assisted plugin development |
| [What's an Unbound Avatar?](https://github.com/ParogDev/WhatsAnUnboundAvatar) | Auto-activation for Avatar of the Wilds at 100 fury |

Built with ExileApiScaffolding (private development workspace)
