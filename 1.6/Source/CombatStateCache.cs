using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CantYouSeeImBusy
{
    public class CombatStateCache : MapComponent
    {
        public bool InCombat { get; private set; }

        private Dictionary<int, int> _gracePeriodStartTick = new Dictionary<int, int>();
        private HashSet<int> _wasDrafted = new HashSet<int>();
        private const int GracePeriodTicks = 300; // ~5 seconds at 1x speed (60 tps); must exceed 150-tick need interval

        private Dictionary<int, int> _lastLetterTick = new Dictionary<int, int>();
        private const int LetterThrottleTicks = 500;

        private int _reconcileTick = 0;
        private const int ReconcileInterval = 60; // ~1 second at 1x speed (60 tps)

        public CombatStateCache(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            InCombat = GenHostility.AnyHostileActiveThreatToPlayer(map);

            _reconcileTick++;
            if (_reconcileTick >= ReconcileInterval)
            {
                _reconcileTick = 0;
                FocusedHediffManager.Reconcile(map);
            }
        }

        public static CombatStateCache? GetFor(Map map)
        {
            return map?.GetComponent<CombatStateCache>();
        }

        public static bool IsMapInCombat(Map map)
        {
            return GetFor(map)?.InCombat ?? false;
        }

        public void TrackDrafted(Pawn pawn)
        {
            if (pawn == null) return;
            _wasDrafted.Add(pawn.thingIDNumber);
        }

        public bool TryStartGracePeriod(Pawn pawn)
        {
            if (pawn == null) return false;
            int id = pawn.thingIDNumber;
            if (!_wasDrafted.Remove(id)) return false;
            if (_gracePeriodStartTick.ContainsKey(id)) return false;
            _gracePeriodStartTick[id] = Find.TickManager.TicksGame;
            return true;
        }

        public void ClearGracePeriod(Pawn pawn)
        {
            if (pawn == null) return;
            _gracePeriodStartTick.Remove(pawn.thingIDNumber);
        }

        public void ClearAllTracking(Pawn pawn)
        {
            if (pawn == null) return;
            _gracePeriodStartTick.Remove(pawn.thingIDNumber);
            _wasDrafted.Remove(pawn.thingIDNumber);
        }

        public bool IsInGracePeriod(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned) return false;
            if (!_gracePeriodStartTick.TryGetValue(pawn.thingIDNumber, out int startTick)) return false;
            return (Find.TickManager.TicksGame - startTick) < GracePeriodTicks;
        }

        public bool ShouldSendLetter(Pawn pawn)
        {
            if (pawn == null) return false;
            int now = Find.TickManager.TicksGame;
            if (_lastLetterTick.TryGetValue(pawn.thingIDNumber, out int lastTick) && (now - lastTick) < LetterThrottleTicks)
                return false;
            _lastLetterTick[pawn.thingIDNumber] = now;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _gracePeriodStartTick, "gracePeriodStartTick", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref _lastLetterTick, "lastLetterTick", LookMode.Value, LookMode.Value);
            _gracePeriodStartTick ??= new Dictionary<int, int>();
            _lastLetterTick ??= new Dictionary<int, int>();
        }
    }
}
