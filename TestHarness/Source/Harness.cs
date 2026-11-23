using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CysibTestHarness
{
    public class Harness : GameComponent
    {
        private enum Phase { WaitForMap, RaidArriving, InCombat, AfterCombat, Done }

        private const string ModPackageId = "sum117.cantyouseeimbusy";
        private const string Tag = "[CYSIB-TEST] ";
        private const float FrozenTolerance = 1e-4f;
        private const float DecayThreshold = 1e-3f;

        private Phase _phase = Phase.WaitForMap;
        private int _phaseStartTick;
        private int _passed;
        private int _failed;
        private Pawn _drafted = null!;
        private Pawn _control = null!;
        private Dictionary<NeedDef, float> _draftedBefore = new Dictionary<NeedDef, float>();
        private Dictionary<NeedDef, float> _controlBefore = new Dictionary<NeedDef, float>();
        private int _framesAfterDone;
        private int _settingsReopens;

        public Harness(Game game) { }

        private static int Now => Find.TickManager.TicksGame;
        private int TicksInPhase => Now - _phaseStartTick;

        public override void GameComponentUpdate()
        {
            if (_phase == Phase.Done)
            {
                WatchSettingsWindow();
                return;
            }
            if (Find.CurrentMap != null && Find.TickManager.CurTimeSpeed != TimeSpeed.Superfast)
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }

        public override void GameComponentTick()
        {
            try
            {
                Step();
            }
            catch (Exception e)
            {
                Check("no harness exception in phase " + _phase, false, e.ToString());
                Finish();
            }
        }

        private void Step()
        {
            Map map = Find.CurrentMap;
            switch (_phase)
            {
                case Phase.WaitForMap:
                    if (map == null || Now < 60 || map.mapPawns.FreeColonistsSpawned.Count < 2) return;
                    Setup(map);
                    Enter(Phase.RaidArriving);
                    break;
                case Phase.RaidArriving:
                    if (TicksInPhase < 90) return;
                    Check("vanilla reports an active hostile threat", GenHostility.AnyHostileActiveThreatToPlayer(map));
                    Check("test pawn is drafted and standing", _drafted.Drafted && !_drafted.Downed);
                    _draftedBefore = Snapshot(_drafted);
                    _controlBefore = Snapshot(_control);
                    Enter(Phase.InCombat);
                    break;
                case Phase.InCombat:
                    if (TicksInPhase < 600) return;
                    VerifyCombat(map);
                    Enter(Phase.AfterCombat);
                    break;
                case Phase.AfterCombat:
                    if (TicksInPhase < 600) return;
                    VerifyAfterCombat(map);
                    Finish();
                    break;
            }
        }

        private void Enter(Phase phase)
        {
            _phase = phase;
            _phaseStartTick = Now;
            Log.Message(Tag + "PHASE " + phase + " at tick " + Now);
        }

        private void Setup(Map map)
        {
            Log.Message(Tag + "START game " + VersionControl.CurrentVersionStringWithRev
                + " mods: " + string.Join(", ", LoadedModManager.RunningMods.Select(m => m.PackageId)));

            Check("mod is loaded", FindMod() != null);
            Check("HediffDef CYSIB_Focused is loaded", DefDatabase<HediffDef>.GetNamedSilentFail("CYSIB_Focused") != null);
            Check("ThoughtDef CYSIB_FocusedThought is loaded", DefDatabase<ThoughtDef>.GetNamedSilentFail("CYSIB_FocusedThought") != null);
            Check("translation keys are loaded", "CYSIB_MentalBreakBlocked_Label".CanTranslate());
            Check("needs patch is applied", IsPatched(typeof(Pawn_NeedsTracker), "NeedsTrackerTickInterval"));
            Check("mental break patch is applied", IsPatched(typeof(MentalStateHandler), "TryStartMentalState"));

            List<Pawn> fighters = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && !p.WorkTagIsDisabled(WorkTags.Violent)).ToList();
            _drafted = fighters[0];
            _control = map.mapPawns.FreeColonistsSpawned.First(p => p != _drafted && !p.Downed);
            Log.Message(Tag + "drafted pawn: " + _drafted.LabelShort + ", undrafted control pawn: " + _control.LabelShort);

            Check("no hostile threat before the raid", !GenHostility.AnyHostileActiveThreatToPlayer(map));

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.forced = true;
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            Check("raid incident fired", IncidentDefOf.RaidEnemy.Worker.TryExecute(parms));

            _drafted.drafter.Drafted = true;
        }

        private void VerifyCombat(Map map)
        {
            Check("threat is still active after 600 ticks", GenHostility.AnyHostileActiveThreatToPlayer(map));
            Check("drafted pawn is still drafted and standing", _drafted.Drafted && !_drafted.Downed);
            Check("drafted pawn has the Focused hediff", HasFocused(_drafted));
            Check("control pawn has no Focused hediff", !HasFocused(_control));

            float draftedDrift = MaxChange(_drafted, _draftedBefore);
            Check("drafted pawn needs are frozen", draftedDrift < FrozenTolerance, Describe(_drafted, _draftedBefore));
            float controlDrift = MaxChange(_control, _controlBefore);
            Check("control pawn needs changed", controlDrift > DecayThreshold, Describe(_control, _controlBefore));

            bool berserk = _drafted.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "CYSIB test", forced: true);
            Check("mental break is blocked on the drafted pawn", !berserk && !_drafted.InMentalState);
            string label = "CYSIB_MentalBreakBlocked_Label".Translate();
            Check("blocked-break letter was sent", Find.LetterStack.LettersListForReading.Any(l => l.Label.ToString() == label));

            bool wander = _control.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Sad, "CYSIB test", forced: true);
            Check("mental break still starts on the control pawn", wander && _control.InMentalState);
            _control.MentalState?.RecoverFromState();

            List<Pawn> hostiles = map.mapPawns.AllPawnsSpawned.Where(p => p.HostileTo(Faction.OfPlayer)).ToList();
            foreach (Pawn hostile in hostiles) hostile.Destroy();
            Log.Message(Tag + "removed " + hostiles.Count + " hostile pawns");

            _draftedBefore = Snapshot(_drafted);
        }

        private void VerifyAfterCombat(Map map)
        {
            Check("no hostile threat after the raid is removed", !GenHostility.AnyHostileActiveThreatToPlayer(map));
            Check("Focused hediff is removed after combat", !HasFocused(_drafted));
            float drift = MaxChange(_drafted, _draftedBefore);
            Check("drafted pawn needs decay again after combat", drift > DecayThreshold, Describe(_drafted, _draftedBefore));

            bool wander = _drafted.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Sad, "CYSIB test", forced: true);
            Check("mental break is allowed again after combat", wander && _drafted.InMentalState);
            _drafted.MentalState?.RecoverFromState();
        }

        private void Finish()
        {
            _phase = Phase.Done;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            try
            {
                OpenSettings();
                Check("settings window opened", Find.WindowStack.IsOpen<Dialog_ModSettings>());
            }
            catch (Exception e)
            {
                Check("settings window opened", false, e.ToString());
            }
            Log.Message(Tag + "RESULT " + (_failed == 0 ? "PASS" : "FAIL") + " passed=" + _passed + " failed=" + _failed);
            Log.Message(Tag + "DONE");
        }

        private static void OpenSettings()
        {
            Mod? mod = FindMod();
            if (mod != null) Find.WindowStack.Add(new Dialog_ModSettings(mod));
        }

        private void WatchSettingsWindow()
        {
            _framesAfterDone++;
            bool open = Find.WindowStack.IsOpen<Dialog_ModSettings>();
            if (_framesAfterDone == 120)
                Log.Message(Tag + "SETTINGS open after 120 frames: " + open + ", reopened " + _settingsReopens + " times");
            if (open || _settingsReopens >= 5) return;
            _settingsReopens++;
            Log.Warning(Tag + "SETTINGS window closed at frame " + _framesAfterDone + ", reopening (" + _settingsReopens + ")");
            OpenSettings();
        }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) _passed++; else _failed++;
            string line = Tag + "CHECK " + (ok ? "PASS" : "FAIL") + " " + name + (detail.Length > 0 ? " | " + detail : "");
            if (ok) Log.Message(line); else Log.Error(line);
        }

        private static Mod? FindMod() =>
            LoadedModManager.ModHandles.FirstOrDefault(m => m.Content.PackageId == ModPackageId);

        private static bool IsPatched(Type type, string method)
        {
            Patches? info = Harmony.GetPatchInfo(AccessTools.Method(type, method));
            return info != null && info.Owners.Contains(ModPackageId);
        }

        private static bool HasFocused(Pawn pawn) =>
            pawn.health.hediffSet.hediffs.Any(h => h.def.defName == "CYSIB_Focused");

        private static Dictionary<NeedDef, float> Snapshot(Pawn pawn) =>
            pawn.needs.AllNeeds.ToDictionary(n => n.def, n => n.CurLevel);

        private static float MaxChange(Pawn pawn, Dictionary<NeedDef, float> before) =>
            pawn.needs.AllNeeds.Where(n => before.ContainsKey(n.def))
                .Select(n => Math.Abs(n.CurLevel - before[n.def])).DefaultIfEmpty(0f).Max();

        private static string Describe(Pawn pawn, Dictionary<NeedDef, float> before) =>
            string.Join(", ", pawn.needs.AllNeeds.Where(n => before.ContainsKey(n.def))
                .Select(n => n.def.defName + " " + before[n.def].ToString("F5") + " -> " + n.CurLevel.ToString("F5")));
    }
}
