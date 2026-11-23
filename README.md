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

- RimWorld 1.6 or 1.7
- [Harmony](https://steamcommunity.com/workshop/filedetails/?id=2009463077)

## How It Works

The mod uses Harmony patches to intercept two core RimWorld systems:

1. **Need Decay** — A prefix snapshots all need levels before the vanilla tick, and a postfix interpolates between the pre-tick and post-tick values based on each need's configured decay rate. At 0% decay, needs are completely frozen. At 50%, they decay at half speed.

2. **Mental Break Blocking** — Intercepts `MentalStateHandler.TryStartMentalState()` and prevents mental breaks from firing on eligible pawns, sending a notification letter instead.

A `CombatStateCache` map component tracks hostiles, draft states, and grace periods. The `FocusedHediffManager` reconciles the visual hediff indicator every 60 ticks to handle edge cases like saving/loading and mid-combat state changes.

## Project Structure

```
1.6/Assemblies/        Compiled mod DLL for RimWorld 1.6 (.NET Framework 4.7.2, Harmony 2)
1.7/Assemblies/        Compiled mod DLL for RimWorld 1.7 (.NET 10, Harmony 3)
Defs/                  HediffDef, ThoughtDef
Source/
  CantYouSeeImBusy.cs                 Main mod class + settings UI
  CombatEligibility.cs                 Pawn eligibility logic
  CombatStateCache.cs                  Combat tracking map component
  FocusedHediffManager.cs              Hediff lifecycle management
  Patches/
    Patch_NeedsTrackerTickInterval.cs  Need decay control
    Patch_TryStartMentalState.cs       Mental break blocking
About/                 Mod metadata + preview image
Languages/             Keyed translation strings
```

## Building

```bash
dotnet build Source/CantYouSeeImBusy.csproj -c Release
```

This needs the .NET 10 SDK and nothing else. The game and Harmony references come from the [Krafs.Rimworld.Ref](https://www.nuget.org/packages/Krafs.Rimworld.Ref) and [Lib.Harmony.Ref](https://www.nuget.org/packages/Lib.Harmony.Ref) NuGet packages, so RimWorld does not have to be installed.

One build produces both DLLs. The `net472` target goes to `1.6/Assemblies/` and the `net10.0` target goes to `1.7/Assemblies/`.

## Testing

`TestHarness/` is a second, developer-only mod that checks this mod in a running game. It is not uploaded to the Workshop.

On a quick-test map it fires a raid, drafts one colonist and keeps another undrafted as a control. It then logs one `[CYSIB-TEST] CHECK PASS` or `CHECK FAIL` line per check to the game log: needs frozen, mental break blocked, Focused hediff shown, and everything back to normal after combat. It ends with `[CYSIB-TEST] RESULT` and opens the mod settings window.

```bash
dotnet build TestHarness/Source/CysibTestHarness.csproj -c Release
```

Put Harmony, this folder and `TestHarness/` in RimWorld's `Mods` folder, enable all three, and start the game with `-quicktest`. Use `-savedatafolder=<path>` to keep the test away from your real saves. The harness targets RimWorld 1.7.
