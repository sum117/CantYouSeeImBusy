using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CantYouSeeImBusy
{
    [HarmonyPatch(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.NeedsTrackerTickInterval))]
    public static class Patch_NeedsTrackerTickInterval
    {
        private static readonly FieldInfo _pawnField =
            AccessTools.Field(typeof(Pawn_NeedsTracker), "pawn");

        private static readonly FieldInfo _needsField =
            AccessTools.Field(typeof(Pawn_NeedsTracker), "needs");

        /// <summary>
        /// Prefix: Snapshots all need levels before the vanilla loop runs.
        /// Uses object-reference snapshot (not index-based) to avoid mismatch
        /// if the needs list changes between Prefix and Postfix.
        /// Also manages grace period state transitions for undrafted pawns.
        /// Never returns false -- preserves other mods' patches.
        /// </summary>
        public static void Prefix(Pawn_NeedsTracker __instance, int delta, ref (Need need, float level)[]? __state)
        {
            __state = null;

            Pawn pawn = (Pawn)_pawnField.GetValue(__instance);
            if (pawn == null) return;

            // Guard: only do work on the 150-tick hash interval (same gate as the original)
            if (!pawn.IsHashIntervalTick(150, delta)) return;

            // Handle grace period state transitions BEFORE the eligibility check
            // so grace period starts/clears are tracked even for partially-eligible pawns
            CombatStateCache? cache = CombatStateCache.GetFor(pawn.Map);
            if (cache != null && cache.InCombat)
            {
                if (pawn.IsColonist && !pawn.Downed && !pawn.WorkTagIsDisabled(WorkTags.Violent))
                {
                    if (!pawn.Drafted)
                        cache.StartGracePeriod(pawn);
                    else
                        cache.ClearGracePeriod(pawn);
                }
            }
            else if (cache != null)
            {
                // Combat ended: clear any stale grace period entries
                cache.ClearGracePeriod(pawn);
            }

            // Now check full eligibility (includes grace period)
            if (!CombatEligibility.IsProtected(pawn)) return;

            // Snapshot need levels by object reference (not by index)
            var needs = (List<Need>?)_needsField.GetValue(__instance);
            if (needs == null) return;

            __state = new (Need, float)[needs.Count];
            for (int i = 0; i < needs.Count; i++)
            {
                __state[i] = (needs[i], needs[i].CurLevel);
            }
        }

        /// <summary>
        /// Postfix: Restores pre-tick need levels using captured object references.
        /// Safe even if the needs list changed between Prefix and Postfix.
        /// </summary>
        public static void Postfix(Pawn_NeedsTracker __instance, (Need need, float level)[]? __state)
        {
            if (__state == null) return;

            // Restore each need's level using the captured reference (not by index)
            // This is safe even if the needs list changed between Prefix and Postfix
            for (int i = 0; i < __state.Length; i++)
            {
                // Guard: if need was removed/despawned between Prefix and Postfix, skip
                if (__state[i].need != null)
                    __state[i].need.CurLevel = __state[i].level;
            }
        }
    }
}
