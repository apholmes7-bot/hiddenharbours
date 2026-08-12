using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The interior pilot (ADR 0021 §4): the room seen from inside, and the furniture standing
        /// on its floor.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> InteriorKitRigs() =>
            new RigRegistration
            {
                // ---- the insides of those buildings (ADR 0021 §4; the interior pilot) ------------

                // THE INTERIOR IS THE BUILDING SEEN FROM INSIDE. Same turntable, same elev 40°, same
                // 32 px = 1 m, and — verified by measurement, not by the header — the SAME Wd/Ln/wallH
                // formulas as houseIsoRig, so a "cottage" interior registers under a "cottage"
                // exterior. Open-dollhouse cutaway: the camera-facing walls are dropped IN THE BAKE,
                // so visibility is art, not a runtime mask.
                //
                // ⚠️ Declared CCW like its exterior — but do NOT reach for BuildingRigAzimuthProbe to
                // check it. That probe reads which SIDE the door lands on at a quarter turn, and it
                // assumes the door is on the +Y gable because both exterior rigs put it there. THIS
                // RIG PUTS ITS DOOR ON −Y (anchors → pj(0,−Ln/2,fZ); the hearth takes +Y). Fed to that
                // probe the interior measures CLOCKWISE — a wrong answer with a confident report, and
                // the bake would then apply the opposite correction and mislabel every cell.
                // InteriorRigAzimuthProbe measures the door's gable FIRST and is what this entry is
                // cross-checked against.
                //
                // Cell 1180×900 and eight facings: past the 2048 the village kit imports at, so the
                // interior kit LIFTS its import cap and asserts native resolution instead
                // (InteriorKit.ImportSizeCap).
                ["interior"] = new RigEntry($"{RigFolder}/interiorIsoRig.js", "InteriorIso",
                                            AzimuthConvention.CounterClockwise),

                // The furniture that stands on that floor. Prop origin is the floor-centre of the
                // prop's footprint — the same pivot convention as the room — so a prop drops onto a
                // room floor point with no offset maths. Exposes project() like the room does, which is
                // how InteriorRigAzimuthProbe proves the two share one camera and one turntable rather
                // than declaring it. Its render() takes (name, dir, opts), not (dir, opts).
                ["interiorProp"] = new RigEntry($"{RigFolder}/interiorPropRig.js", "PropIso",
                                                AzimuthConvention.CounterClockwise),
            };
    }
}
