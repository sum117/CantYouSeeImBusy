using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CantYouSeeImBusy
{
    /// <summary>
    /// Static helper for CYSIB_Focused hediff lifecycle: apply, remove, and reconcile.
    /// Reconcile() is the canonical entry point for polling-based state correction.
    /// </summary>
    public static class FocusedHediffManager
    {
        private static HediffDef? _focusedDef;

        /// <summary>
        /// Lazy-cached reference to the CYSIB_Focused HediffDef.
        /// Resolves on first access to avoid per-call dictionary lookups.
        /// </summary>
        public static HediffDef FocusedDef =>
            _focusedDef ??= DefDatabase<HediffDef>.GetNamed("CYSIB_Focused");

        /// <summary>
        /// Applies the Focused hediff to the pawn.
        /// Uses GetOrAddHediff to prevent duplicate instances.
        /// </summary>
        public static void ApplyTo(Pawn pawn)
        {
            pawn.health.GetOrAddHediff(FocusedDef);
        }

        /// <summary>
        /// Removes the Focused hediff from the pawn if present.
        /// </summary>
        public static void RemoveFrom(Pawn pawn)
        {
            Hediff? hediff = pawn.health.hediffSet.GetFirstHediffOfDef(FocusedDef);
            if (hediff != null)
                pawn.health.RemoveHediff(hediff);
        }

        /// <summary>
        /// Reconciles hediff presence against eligibility for all spawned colonists on the map.
        /// Applies hediff to eligible pawns that lack it; removes it from ineligible pawns that have it.
        /// Handles edge cases: save/load, downed mid-combat, map transitions, mod enable/disable.
        /// </summary>
        public static void Reconcile(Map map)
        {
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;

            // If mod disabled, remove ALL Focused hediffs and bail out
            if (!CantYouSeeImBusyMod.Settings.ModEnabled)
            {
                for (int i = 0; i < colonists.Count; i++)
                    RemoveFrom(colonists[i]);
                return;
            }

            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                bool eligible = CombatEligibility.IsProtected(pawn);
                bool hasHediff = pawn.health.hediffSet.HasHediff(FocusedDef);

                if (eligible && !hasHediff)
                    ApplyTo(pawn);
                else if (!eligible && hasHediff)
                    RemoveFrom(pawn);
            }
        }
    }
}
