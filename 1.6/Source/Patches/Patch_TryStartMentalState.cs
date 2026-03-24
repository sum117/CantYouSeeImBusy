using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CantYouSeeImBusy
{
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class Patch_TryStartMentalState
    {
        private static readonly FieldInfo PawnField =
            AccessTools.Field(typeof(MentalStateHandler), "pawn");

        public static bool Prefix(
            MentalStateHandler __instance,
            MentalStateDef stateDef,
            ref bool __result)
        {
            if (!CantYouSeeImBusyMod.Settings.ModEnabled) return true;

            Pawn pawn = (Pawn)PawnField.GetValue(__instance);

            // If not protected, allow the original method to run
            if (!CombatEligibility.IsProtected(pawn) && !CombatEligibility.IsCaravanProtected(pawn)) return true;

            // BRKD-01/BRKD-02: If breakdown blocking is disabled, allow mental breaks through
            // Need freezing (Patch_NeedsTrackerTickInterval) is unaffected by this check
            if (!CantYouSeeImBusyMod.Settings.BlockBreakdowns) return true;

            // CUBE-01/CUBE-02: If Cube override is enabled and this is a Cube-related break, allow it
            if (IsCubeBreakAllowed(pawn, stateDef)) return true;

            // Block the mental break
            __result = false;

            // Send throttled letter notification
            CombatStateCache? cache = CombatStateCache.GetFor(pawn.Map);
            if (cache != null && cache.ShouldSendLetter(pawn))
            {
                string label = "CYSIB_MentalBreakBlocked_Label".Translate();
                string text = "CYSIB_MentalBreakBlocked_Text".Translate(pawn.LabelShort);
                Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.PositiveEvent, pawn);
            }

            return false; // skip original TryStartMentalState
        }

        private static bool IsCubeBreakAllowed(Pawn pawn, MentalStateDef stateDef)
        {
            // CUBE-05: When override is disabled, no cube bypass
            if (!CantYouSeeImBusyMod.Settings.AllowCubeBreaks)
                return false;

            // CUBE-01: CubeSculpting mental state (from Hediff_CubeInterest.DoMentalBreak)
            if (stateDef.defName == "CubeSculpting")
                return true;

            // CUBE-02: Berserk triggered by CubeRage hediff (from HediffComp_CauseMentalState)
            if (stateDef.defName == "Berserk")
            {
                var hediffs = pawn.health?.hediffSet?.hediffs;
                if (hediffs != null)
                {
                    for (int i = 0; i < hediffs.Count; i++)
                    {
                        if (hediffs[i].def.defName == "CubeRage")
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
