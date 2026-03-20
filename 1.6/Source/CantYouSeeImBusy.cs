using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CantYouSeeImBusy
{
    public class CantYouSeeImBusyMod : Mod
    {
        public static CantYouSeeImBusySettings Settings = null!;
        private static Vector2 _scrollPosition;

        public CantYouSeeImBusyMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CantYouSeeImBusySettings>();
            var harmony = new Harmony("sum117.cantyouseeimbusy");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "Can't You See I'm Busy";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Sort needs alphabetically by display label; filter out hidden/internal needs with empty labels
            var sortedNeeds = DefDatabase<NeedDef>.AllDefsListForReading
                .Where(d => d.showOnNeedList && !d.LabelCap.ToString().NullOrEmpty())
                .OrderBy(d => d.LabelCap.ToString())
                .ToList();

            // Estimate view height: toggle(30) + header(30) + disabled msg(30) + gap(12) + buttons row(30) + gap(12) + per-need(34 each) + gap(12) + reset button(30) + padding(30)
            float viewHeight = 216f + sortedNeeds.Count * 34f;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 30f, viewHeight);

            Widgets.BeginScrollView(inRect, ref _scrollPosition, viewRect);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(new Rect(0f, 0f, viewRect.width, viewHeight));

            // Global toggle checkbox
            ls.CheckboxLabeled("CYSIB_Settings_ModEnabled".Translate(), ref Settings.ModEnabled);

            // Header description
            ls.Label("CYSIB_Settings_Header".Translate());

            // "Mod disabled" message when toggle is OFF
            if (!Settings.ModEnabled)
            {
                ls.Label("CYSIB_Settings_Disabled".Translate());
            }

            ls.GapLine();

            // Quick-set buttons: "Freeze All" and "Normal All" side by side
            Rect buttonsRect = ls.GetRect(30f);
            float halfWidth = (buttonsRect.width - 10f) / 2f;
            if (Widgets.ButtonText(new Rect(buttonsRect.x, buttonsRect.y, halfWidth, 30f),
                "CYSIB_Settings_FreezeAll".Translate(), active: Settings.ModEnabled))
            {
                foreach (var def in sortedNeeds)
                    Settings.NeedDecayRates[def.defName] = 0f;
            }
            if (Widgets.ButtonText(new Rect(buttonsRect.x + halfWidth + 10f, buttonsRect.y, halfWidth, 30f),
                "CYSIB_Settings_NormalAll".Translate(), active: Settings.ModEnabled))
            {
                foreach (var def in sortedNeeds)
                    Settings.NeedDecayRates[def.defName] = 1f;
            }

            ls.Gap(12f);

            // Per-need sliders — grayed out when mod is disabled
            bool prevEnabled = GUI.enabled;
            GUI.enabled = Settings.ModEnabled;

            foreach (NeedDef def in sortedNeeds)
            {
                float current = Settings.GetDecayRate(def);
                int pct = Mathf.RoundToInt(current * 100f);
                // Status word: 0% = Frozen, 100% = Normal, anything else = Slowed
                string statusKey = current <= 0f ? "CYSIB_Settings_StatusFrozen"
                    : current >= 1f ? "CYSIB_Settings_StatusNormal"
                    : "CYSIB_Settings_StatusSlowed";
                string sliderLabel = $"{def.LabelCap}: {pct}% ({statusKey.Translate()})";
                string tooltip = "CYSIB_Settings_SliderTooltip".Translate(def.LabelCap, pct);

                float newVal = ls.SliderLabeled(sliderLabel, current, 0f, 1f, tooltip: tooltip);
                // Snap to 10% steps
                newVal = Mathf.Round(newVal * 10f) / 10f;
                if (newVal != current)
                    Settings.NeedDecayRates[def.defName] = newVal;
            }

            GUI.enabled = prevEnabled;

            ls.GapLine();

            // Reset to Defaults button
            if (ls.ButtonText("CYSIB_Settings_ResetDefaults".Translate()))
            {
                Settings.NeedDecayRates.Clear();
            }

            ls.End();
            Widgets.EndScrollView();
        }
    }

    public class CantYouSeeImBusySettings : ModSettings
    {
        public bool ModEnabled = true;
        public Dictionary<string, float> NeedDecayRates = new Dictionary<string, float>();

        public float GetDecayRate(NeedDef def)
        {
            if (def == null) return 0f;
            return NeedDecayRates.TryGetValue(def.defName, out float rate) ? rate : 0f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ModEnabled, "ModEnabled", true);
            Scribe_Collections.Look(ref NeedDecayRates, "NeedDecayRates", LookMode.Value, LookMode.Value);
            NeedDecayRates ??= new Dictionary<string, float>();
        }
    }
}
