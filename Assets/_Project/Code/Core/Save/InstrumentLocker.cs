using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// THE INSTRUMENT LOCKER — the one place a hull's BOUGHT helm instruments (and that hull's sounder
    /// preferences) are read and written.
    ///
    /// <para><b>Why it exists, and why here.</b> Exactly the <see cref="SupplyLocker"/>/<see cref="PotLocker"/>
    /// reason: state touched by three lanes (Economy sells it, Boats resolves the fit from it, UI flips a
    /// preference on it) turns into three hand-rolled "find the row, write it back" loops, which is how an
    /// off-by-one gets into a save file. So it lives here once, in Core, where Economy and Boats can both
    /// reach it without referencing each other (rule 4).</para>
    ///
    /// <para><b>Per hull, and SPARSE.</b> Unlike <see cref="SaveData.OwnedGear"/> — a flat presence-only
    /// wallet for things you carry — an instrument is bolted into ONE boat's dash. A hull with no rows here
    /// carries its console's authored default fit; only the player's <i>deviations</i> are stored, which is
    /// what keeps <c>BoatEquipment.EffectiveFit</c> honest about recomputing rather than storing the
    /// resolved fit (rule 5). ADR 0030.</para>
    ///
    /// <para>Static and null-safe throughout: a null save (EditMode rigs, pre-boot) reads as "nothing owned,
    /// defaults everywhere" rather than throwing — the established greybox posture.</para>
    /// </summary>
    public static class InstrumentLocker
    {
        /// <summary>Does this hull carry this bought instrument? False for a null save, a null id, or a
        /// hull that has never been upgraded.</summary>
        public static bool Owns(SaveData save, string hullId, string instrumentId)
        {
            if (save?.HullInstruments == null) return false;
            if (string.IsNullOrEmpty(hullId) || string.IsNullOrEmpty(instrumentId)) return false;
            for (int i = 0; i < save.HullInstruments.Count; i++)
            {
                HullInstrument row = save.HullInstruments[i];
                if (row.HullId == hullId && row.InstrumentId == instrumentId) return true;
            }
            return false;
        }

        /// <summary>
        /// Fit an instrument to a hull (a purchase or a grant). Idempotent — buying the same unit twice
        /// adds no second row. Returns true iff a NEW row was written, so a vendor can refuse to charge
        /// for something already fitted.
        /// </summary>
        public static bool Add(SaveData save, string hullId, string instrumentId)
        {
            if (save == null || string.IsNullOrEmpty(hullId) || string.IsNullOrEmpty(instrumentId)) return false;
            save.HullInstruments ??= new List<HullInstrument>();
            if (Owns(save, hullId, instrumentId)) return false;
            save.HullInstruments.Add(new HullInstrument(hullId, instrumentId));
            return true;
        }

        /// <summary>
        /// Fill <paramref name="into"/> (cleared first) with every instrument id owned for this hull and
        /// return it — the exact <c>ICollection&lt;string&gt;</c> shape
        /// <c>BoatEquipment.EffectiveFit</c> consumes. Caller-provided list so a per-frame resolver
        /// allocates nothing (rule 7).
        /// </summary>
        public static List<string> OwnedFor(SaveData save, string hullId, List<string> into)
        {
            into ??= new List<string>();
            into.Clear();
            if (save?.HullInstruments == null || string.IsNullOrEmpty(hullId)) return into;
            for (int i = 0; i < save.HullInstruments.Count; i++)
            {
                HullInstrument row = save.HullInstruments[i];
                if (row.HullId == hullId && !string.IsNullOrEmpty(row.InstrumentId))
                    into.Add(row.InstrumentId);
            }
            return into;
        }

        // ---- sounder preferences (per hull, sparse — absent means the owner's defaults) ---------------

        /// <summary>This hull's stored sounder preferences, or <paramref name="fallback"/> when the hull
        /// has none (never touched, or a null save).</summary>
        public static SounderPrefs PrefsFor(SaveData save, string hullId, in SounderPrefs fallback)
        {
            if (save?.HullSounderPrefs == null || string.IsNullOrEmpty(hullId)) return fallback;
            for (int i = 0; i < save.HullSounderPrefs.Count; i++)
                if (save.HullSounderPrefs[i].HullId == hullId) return save.HullSounderPrefs[i].Prefs;
            return fallback;
        }

        /// <summary>Write this hull's sounder preferences (upsert). Returns true iff the save changed —
        /// callers use that to avoid persisting on a no-op button press.</summary>
        public static bool SetPrefs(SaveData save, string hullId, in SounderPrefs prefs)
        {
            if (save == null || string.IsNullOrEmpty(hullId)) return false;
            save.HullSounderPrefs ??= new List<SounderPrefsDto>();
            var row = new SounderPrefsDto(hullId, in prefs);
            for (int i = 0; i < save.HullSounderPrefs.Count; i++)
            {
                if (save.HullSounderPrefs[i].HullId != hullId) continue;
                SounderPrefsDto existing = save.HullSounderPrefs[i];
                // ⚠ EVERY field, or a preference silently stops persisting. RangeMetres joined at v9;
                // omitting it here would make the finder's RANGE pushers move the glass and never the save.
                if (existing.AlarmMetres == row.AlarmMetres && existing.Armed == row.Armed
                    && existing.Feet == row.Feet && existing.Night == row.Night
                    && existing.RangeMetres == row.RangeMetres) return false;
                save.HullSounderPrefs[i] = row;    // SounderPrefsDto is a struct — write it back
                return true;
            }
            save.HullSounderPrefs.Add(row);
            return true;
        }

        // ---- chartplotter preferences (per hull, sparse — absent means the owner's defaults) ----------
        // FLAG lead-architect: additive v10 storage on the ADR 0025 S6 instrument-preferences seam. Same
        // shape and same contract as the sounder's pair above; see ChartplotterPrefs for why the plotter
        // keeps its own record rather than joining SounderPrefs.

        /// <summary>This hull's stored chartplotter preferences, or <paramref name="fallback"/> when the
        /// hull has none (never touched, or a null save).</summary>
        public static ChartplotterPrefs ChartplotterPrefsFor(SaveData save, string hullId,
                                                             in ChartplotterPrefs fallback)
        {
            if (save?.HullChartplotterPrefs == null || string.IsNullOrEmpty(hullId)) return fallback;
            for (int i = 0; i < save.HullChartplotterPrefs.Count; i++)
                if (save.HullChartplotterPrefs[i].HullId == hullId)
                    return save.HullChartplotterPrefs[i].Prefs;
            return fallback;
        }

        /// <summary>Write this hull's chartplotter preferences (upsert). Returns true iff the save
        /// changed — callers use that to avoid persisting on a no-op button press.</summary>
        public static bool SetChartplotterPrefs(SaveData save, string hullId,
                                                in ChartplotterPrefs prefs)
        {
            if (save == null || string.IsNullOrEmpty(hullId)) return false;
            save.HullChartplotterPrefs ??= new List<ChartplotterPrefsDto>();
            var row = new ChartplotterPrefsDto(hullId, in prefs);
            for (int i = 0; i < save.HullChartplotterPrefs.Count; i++)
            {
                if (save.HullChartplotterPrefs[i].HullId != hullId) continue;
                ChartplotterPrefsDto existing = save.HullChartplotterPrefs[i];
                // ⚠ EVERY field, or a preference silently stops persisting — the trap that made the
                // finder's RANGE pushers move the glass and never the save. Three fields today; a
                // fourth added above without a clause here would be the same bug again.
                if (existing.Night == row.Night && existing.HeadUp == row.HeadUp
                    && existing.RangeStep == row.RangeStep) return false;
                save.HullChartplotterPrefs[i] = row;   // ChartplotterPrefsDto is a struct — write it back
                return true;
            }
            save.HullChartplotterPrefs.Add(row);
            return true;
        }
    }
}
