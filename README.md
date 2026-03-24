# Can't You See I'm Busy

![Preview](About/preview.png)

A RimWorld mod that keeps your colonists focused during combat by freezing need decay and blocking mental breaks.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3688506794)

## Steam Workshop Description

When a raid hits and your colonists are drafted, **Can't You See I'm Busy** freezes need decay and blocks mental breaks so colonists focus on fighting instead of wandering off for a snack or having a mental breakdown. When combat ends, all needs resume decaying exactly where they left off.

**Features:**
- Needs stop decaying while drafted and in active combat
- Mental breaks are blocked during combat
- Configurable decay rate per need (0-100%) — fully frozen, partially slowed, or normal
- 300-tick grace period after undrafting so needs don't spike immediately
- Visual "Focused" hediff indicator on protected colonists
- Mod settings panel with per-need sliders, "Freeze All" / "Normal All" buttons, and reset to defaults

Only applies to player colonists who are drafted, not downed, and capable of violence while hostiles are on the map.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/workshop/filedetails/?id=2009463077)

## How It Works

The mod uses Harmony patches to intercept two core RimWorld systems:

1. **Need Decay** — A prefix snapshots all need levels before the vanilla tick, and a postfix interpolates between the pre-tick and post-tick values based on each need's configured decay rate. At 0% decay, needs are completely frozen. At 50%, they decay at half speed.

2. **Mental Break Blocking** — Intercepts `MentalStateHandler.TryStartMentalState()` and prevents mental breaks from firing on eligible pawns, sending a notification letter instead.

A `CombatStateCache` map component tracks hostiles, draft states, and grace periods. The `FocusedHediffManager` reconciles the visual hediff indicator every 60 ticks to handle edge cases like saving/loading and mid-combat state changes.

## Project Structure

```
1.6/
  Assemblies/          Compiled mod DLL
  Defs/                HediffDef, ThoughtDef
  Source/
    CantYouSeeImBusy.cs                 Main mod class + settings UI
    CombatEligibility.cs                 Pawn eligibility logic
    CombatStateCache.cs                  Combat tracking map component
    FocusedHediffManager.cs              Hediff lifecycle management
    Patches/
      Patch_NeedsTrackerTickInterval.cs  Need decay control
      Patch_TryStartMentalState.cs       Mental break blocking
About/                 Mod metadata + preview image
Languages/English/     Keyed translation strings
```

## Building

```bash
dotnet build "1.6/Source/CantYouSeeImBusy.csproj" --no-restore
```

| Flag | Purpose |
|------|---------|
| `"1.6/Source/CantYouSeeImBusy.csproj"` | Path to the project file. Quoted because the path may contain spaces. |
| `--no-restore` | Skips NuGet package restore. This project has no NuGet dependencies — all references are local DLLs (RimWorld, Unity, Harmony) — so the restore step is unnecessary. |

The compiled DLL is output to `1.6/Assemblies/` as configured by the `<OutputPath>` in the csproj.
