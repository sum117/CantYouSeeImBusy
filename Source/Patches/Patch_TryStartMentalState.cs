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
            Pawn pawn = (Pawn)PawnField.GetValue(__instance);

            // If not protected, allow the original method to run
            if (!CombatEligibility.IsProtected(pawn)) return true;

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
    }
}
