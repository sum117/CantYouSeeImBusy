using RimWorld;
using Verse;

namespace CantYouSeeImBusy
{
    public static class CombatEligibility
    {
        /// <summary>
        /// Returns true when this pawn should receive combat protection
        /// (need freeze + mental break blocking).
        /// Conditions (ALL must be true):
        ///   - Player colonist (not ally, prisoner, slave, animal)
        ///   - Not downed
        ///   - Not incapable of violence (WorkTags.Violent)
        ///   - Map has active hostile threat (via CombatStateCache)
        ///   - Currently drafted OR within 300-tick undraft grace period
        /// </summary>
        public static bool IsProtected(Pawn pawn)
        {
            if (pawn?.Map == null) return false;
            if (!pawn.IsColonist) return false;
            if (pawn.Downed) return false;
            if (pawn.WorkTagIsDisabled(WorkTags.Violent)) return false;

            CombatStateCache? cache = CombatStateCache.GetFor(pawn.Map);
            if (cache == null || !cache.InCombat) return false;

            return pawn.Drafted || cache.IsInGracePeriod(pawn);
        }
    }
}
