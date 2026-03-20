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
                    if (pawn.Drafted)
                    {
                        // Pawn is drafted: track for grace period transition, clear any active grace
                        cache.TrackDrafted(pawn);
                        cache.ClearGracePeriod(pawn);
                    }
                    else
                    {
                        // Pawn is undrafted: start grace period ONLY if transitioning from drafted
                        // TryStartGracePeriod returns false if pawn was never drafted or already in grace
                        cache.TryStartGracePeriod(pawn);
                    }
                }
            }
            else if (cache != null)
            {
                // Combat ended: clear all tracking for this pawn
                cache.ClearAllTracking(pawn);
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
        /// Postfix: Interpolates between pre-tick and post-tick need levels based on per-need decay rate.
        /// decayRate=0 -> full freeze (Phase 2 behavior): CurLevel = preTickLevel
        /// decayRate=0.5 -> half decay: CurLevel = midpoint between pre and post tick
        /// decayRate=1 -> normal decay (vanilla): CurLevel = postTickLevel
        /// Safe even if the needs list changed between Prefix and Postfix.
        /// </summary>
        public static void Postfix(Pawn_NeedsTracker __instance, (Need need, float level)[]? __state)
        {
            if (__state == null) return;

            // Global toggle: if mod disabled, allow normal decay (no restoration)
            if (!CantYouSeeImBusyMod.Settings.ModEnabled) return;

            for (int i = 0; i < __state.Length; i++)
            {
                if (__state[i].need == null) continue;

                float preTickLevel = __state[i].level;
                float postTickLevel = __state[i].need.CurLevel;
                float decayRate = CantYouSeeImBusyMod.Settings.GetDecayRate(__state[i].need.def);

                // decayRate=0 -> full freeze: CurLevel = postTickLevel + (preTickLevel - postTickLevel) * 1.0 = preTickLevel
                // decayRate=1 -> normal decay: CurLevel = postTickLevel + (preTickLevel - postTickLevel) * 0.0 = postTickLevel
                // decayRate=0.5 -> half decay: CurLevel = midpoint between pre and post tick levels
                __state[i].need.CurLevel = postTickLevel + (preTickLevel - postTickLevel) * (1f - decayRate);
            }
        }
    }
}
