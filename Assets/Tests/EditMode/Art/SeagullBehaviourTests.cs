using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The bird's rules come from the drop, and the drop is on disk.</b>
    /// <see cref="SeagullVisualDef"/> is the baked intake of <c>seagullIsoRig.gameplay.json</c> — 13
    /// states, 26 edges, 384 cells, and the creature's real measurements. These check that the
    /// committed asset still loads through the production path, still says what the art director's
    /// drop says, and still describes a bird that is 45 px across at every altitude.
    ///
    /// <para>🔴 <b>What these would have caught.</b> The drop ships <c>dive</c> as
    /// <c>alt: [0.3, 8]</c> — LOW first — while its README writes the descent as "8 → 0.3". A consumer
    /// that reads the pair as [start, end] flies the dive UPWARDS out of the sea. The entry/exit rule
    /// is taken off the SIGN OF vz instead, and <see cref="TheDiveEntersHighEvenThoughTheDropWritesItLow"/>
    /// is what says so. The second is the owner's ruling of 2026-09-09: altitude is a screen offset,
    /// never a sprite scale — so the config carries no proximity knob and the presenter never writes
    /// a scale, and <see cref="NothingScalesAGullByHowCloseItIs"/> holds both ends of that shut.</para>
    /// </summary>
    public class SeagullBehaviourTests
    {
        /// <summary>32 px to the metre, everywhere in this project.</summary>
        const float PixelsPerMetre = 32f;

        /// <summary>The gull's span in pixels at EVERY altitude: 1.40 m x 32 px/m.</summary>
        const float WingspanPixels = 44.8f;

        const float Eps = 1e-4f;

        static SeagullVisualDef Def()
        {
            var def = Resources.Load<SeagullVisualDef>(SeagullVisualDef.ResourcesPath);
            Assert.IsNotNull(def, "Resources.Load<SeagullVisualDef>(\"" +
                             SeagullVisualDef.ResourcesPath + "\") came back null — this is the call " +
                             "GullFlock.Awake makes, and the harbour has no birds without it");
            return def;
        }

        // =========================================================================================
        //  1. the committed asset
        // =========================================================================================

        /// <summary>The def validates itself. <see cref="SeagullVisualDef.TryValidate"/> is the same
        /// check the baker runs before it writes, so a green here means the asset on disk and the
        /// asset the baker would produce agree about their own shape.</summary>
        [Test]
        public void TheCommittedDefValidates()
        {
            Assert.IsTrue(Def().TryValidate(out string error), error);
        }

        /// <summary>Thirteen states, in the order the contract lays the sheet's columns out in. The
        /// order is not cosmetic — the slicer, the strips and <see cref="SeagullStates.IndexOf"/> all
        /// index by it.</summary>
        [Test]
        public void TheDropDeclaresThirteenStatesInTheSheetsOrder()
        {
            var def = Def();
            Assert.AreEqual(13, SeagullStates.Order.Length);
            Assert.AreEqual(13, def.States.Count, "the def carries a different number of states");
            Assert.AreEqual(13, def.Behaviour.StateCount);

            for (int i = 0; i < SeagullStates.Order.Length; i++)
                Assert.AreEqual(SeagullStates.Order[i], def.Behaviour.IdOf(i), "state " + i);

            var fromStrips = new List<string>();
            foreach (var s in def.Strips) fromStrips.Add(s.State);
            CollectionAssert.AreEqual(SeagullStates.Order, fromStrips,
                "the SHEET's strips are in a different order from the state table — the two index each " +
                "other, so a mismatch draws the wrong animation rather than throwing");
        }

        /// <summary>Every strip's frame count matches the state's. The strip is what the sheet HAS; the
        /// state is what the sidecar SAYS. A bird whose state runs longer than its strip reads a cell
        /// that belongs to the next animation.</summary>
        [Test]
        public void EveryStripIsAsLongAsItsState()
        {
            var def = Def();
            int column = 0;
            foreach (string id in SeagullStates.Order)
            {
                var strip = def.Strip(id);
                Assert.IsNotNull(strip, id + " has no strip on the sheet");
                Assert.AreEqual(column, strip.Column, id + " starts at the wrong column");
                Assert.AreEqual(def.Behaviour.Row(def.Behaviour.IndexOf(id)).Frames, strip.Frames,
                                id + ": the sheet and the sidecar disagree about frame count");
                column += strip.Frames;
            }
            Assert.AreEqual(def.Columns, column, "the strips do not fill the sheet");
        }

        /// <summary>All 8 x 48 cells resolve to a real sprite. A null here is an invisible bird on one
        /// facing of one frame — the kind of hole that only shows up when a gull happens to turn that
        /// way in front of the owner.</summary>
        [Test]
        public void EveryCellOnTheSheetResolves()
        {
            var def = Def();
            Assert.AreEqual(def.Directions * def.Columns, def.CellCount);
            for (int dir = 0; dir < def.Directions; dir++)
                foreach (string id in SeagullStates.Order)
                {
                    int frames = def.Strip(id).Frames;
                    for (int f = 0; f < frames; f++)
                        Assert.IsNotNull(def.Cell(dir, id, f),
                                         "dir " + dir + " " + id + " frame " + f + " is empty");
                }
        }

        // =========================================================================================
        //  2. the state machine's table
        // =========================================================================================

        /// <summary>
        /// The drop declares 26 edges, and <see cref="SeagullBehaviour.CanGo"/> accepts those 26 and
        /// refuses the other 143 pairs. The table is packed into a bitmask, one <c>uint</c> per state,
        /// so this also says the packing did not lose a bit or shift the wrong way.
        /// </summary>
        [Test]
        public void CanGoIsExactlyTheDeclaredEdgeSetAndNothingElse()
        {
            var def = Def();
            var behaviour = def.Behaviour;
            int n = behaviour.StateCount;

            var declared = new HashSet<string>();
            foreach (var e in def.Transitions) declared.Add(e.From + "->" + e.To);
            Assert.AreEqual(26, declared.Count, "the drop no longer declares 26 distinct edges");
            Assert.AreEqual(declared.Count, behaviour.EdgeCount,
                            "the behaviour table counted a different number of edges than the def lists");

            int accepted = 0;
            for (int from = 0; from < n; from++)
                for (int to = 0; to < n; to++)
                {
                    bool legal = declared.Contains(behaviour.IdOf(from) + "->" + behaviour.IdOf(to));
                    Assert.AreEqual(legal, behaviour.CanGo(from, to),
                                    behaviour.IdOf(from) + " -> " + behaviour.IdOf(to));
                    if (legal) accepted++;
                }
            Assert.AreEqual(26, accepted, "the sweep did not actually exercise all 26 edges");
        }

        /// <summary>A state index the presenter has lost track of gets "no", not an exception. A bird
        /// asking an impossible question mid-frame should stand still, not take the game down.</summary>
        [Test]
        public void AnOutOfRangeStateIsRefusedNotThrown()
        {
            var behaviour = Def().Behaviour;
            Assert.IsFalse(behaviour.CanGo(-1, 0));
            Assert.IsFalse(behaviour.CanGo(0, -1));
            Assert.IsFalse(behaviour.CanGo(99, 0));
            Assert.IsFalse(behaviour.CanGo(0, 99));
            Assert.AreEqual(-1, behaviour.Next(-1));
            Assert.AreEqual(-1, behaviour.Next(99));
            Assert.IsFalse(behaviour.IsLoop(99));
            Assert.IsFalse(behaviour.LeavesSurface(99));
            Assert.IsFalse(behaviour.ArrivesOnSurface(99));
            Assert.AreEqual(SeagullSurface.None, behaviour.SurfaceOf(99));
        }

        /// <summary>The four oneshot chains the charter names: <c>dive→splash</c>,
        /// <c>splash→float</c>, <c>land→stand</c>, <c>takeoff→fly</c>. Everything else loops until
        /// something asks it to stop.</summary>
        [Test]
        public void TheOneshotsChainWhereTheDropSaysTheyDo()
        {
            var b = Def().Behaviour;
            Assert.AreEqual(SeagullStates.Splash, b.IdOf(b.Next(b.IndexOf(SeagullStates.Dive))));
            Assert.AreEqual(SeagullStates.Float, b.IdOf(b.Next(b.IndexOf(SeagullStates.Splash))));
            Assert.AreEqual(SeagullStates.Stand, b.IdOf(b.Next(b.IndexOf(SeagullStates.Land))));
            Assert.AreEqual(SeagullStates.Fly, b.IdOf(b.Next(b.IndexOf(SeagullStates.Takeoff))));

            var oneshots = new[] { SeagullStates.Dive, SeagullStates.Splash,
                                   SeagullStates.Land, SeagullStates.Takeoff };
            foreach (string id in SeagullStates.Order)
            {
                int s = b.IndexOf(id);
                bool oneshot = Array.IndexOf(oneshots, id) >= 0;
                Assert.AreEqual(!oneshot, b.IsLoop(s), id + ": loop flag");
                Assert.AreEqual(oneshot, b.Next(s) >= 0, id + ": chain target");
            }
        }

        /// <summary>The charter: "only takeoff leaves a surface". <see cref="SeagullBehaviour"/>'s own
        /// doc says a test says so — this is it, and it names the whole set, not just the member.</summary>
        [Test]
        public void TakeoffIsTheOnlyStateThatLeavesASurface()
        {
            var b = Def().Behaviour;
            var leaving = new List<string>();
            for (int s = 0; s < b.StateCount; s++) if (b.LeavesSurface(s)) leaving.Add(b.IdOf(s));
            CollectionAssert.AreEqual(new[] { SeagullStates.Takeoff }, leaving,
                                      "the set of states that leave a surface");
        }

        /// <summary>And "only land/splash arrive on one" — land onto ground, splash onto water. A dive
        /// does NOT arrive: it chains into the splash, and the splash is what touches the sea.</summary>
        [Test]
        public void LandAndSplashAreTheOnlyStatesThatArriveOnASurface()
        {
            var b = Def().Behaviour;
            var arriving = new List<string>();
            for (int s = 0; s < b.StateCount; s++) if (b.ArrivesOnSurface(s)) arriving.Add(b.IdOf(s));
            CollectionAssert.AreEquivalent(new[] { SeagullStates.Land, SeagullStates.Splash }, arriving,
                                           "the set of states that arrive on a surface");

            Assert.AreEqual(SeagullSurface.Ground, b.ArrivalSurface(b.IndexOf(SeagullStates.Land)));
            Assert.AreEqual(SeagullSurface.Water, b.ArrivalSurface(b.IndexOf(SeagullStates.Splash)));
            Assert.AreEqual(SeagullSurface.None, b.ArrivalSurface(b.IndexOf(SeagullStates.Dive)),
                            "the dive chains into the splash; the splash is what wets the bird");
            Assert.AreEqual(SeagullSurface.None, b.ArrivalSurface(b.IndexOf(SeagullStates.Fly)));
        }

        /// <summary>Which surface a state is ON. This drop wires <c>preen</c> and <c>peck</c> only off
        /// <c>float</c>, so they are water states here; a later drop that lets a standing bird preen
        /// would have to make the surface follow the bird in rather than sit on the state.</summary>
        [Test]
        public void EveryStateSitsOnTheSurfaceTheDropPutItOn()
        {
            var b = Def().Behaviour;
            var expected = new Dictionary<string, SeagullSurface>
            {
                { SeagullStates.Fly,     SeagullSurface.None },
                { SeagullStates.Glide,   SeagullSurface.None },
                { SeagullStates.Swoop,   SeagullSurface.None },
                { SeagullStates.Dive,    SeagullSurface.None },
                { SeagullStates.Splash,  SeagullSurface.None },
                { SeagullStates.Float,   SeagullSurface.Water },
                { SeagullStates.Preen,   SeagullSurface.Water },
                { SeagullStates.Peck,    SeagullSurface.Water },
                { SeagullStates.Land,    SeagullSurface.None },
                { SeagullStates.Stand,   SeagullSurface.Ground },
                { SeagullStates.Walk,    SeagullSurface.Ground },
                { SeagullStates.Perch,   SeagullSurface.Perch },
                { SeagullStates.Takeoff, SeagullSurface.None },
            };
            foreach (var kv in expected)
                Assert.AreEqual(kv.Value, b.SurfaceOf(b.IndexOf(kv.Key)), kv.Key);
        }

        /// <summary>The three flock states are the three the wheel can draw, and the mapping back from
        /// the wheel's anim enum lands on them. If these drift apart a wheeling bird draws a peck.</summary>
        [Test]
        public void TheWheelsThreeAnimsAreTheThreeFlockDrivenStates()
        {
            var b = Def().Behaviour;
            var flockDriven = new List<string>();
            for (int s = 0; s < b.StateCount; s++) if (b.IsFlockDriven(s)) flockDriven.Add(b.IdOf(s));
            CollectionAssert.AreEquivalent(
                new[] { SeagullStates.Fly, SeagullStates.Glide, SeagullStates.Swoop }, flockDriven);

            Assert.AreEqual(SeagullStates.Fly,
                            b.IdOf(b.StateOf(SeagullFlockMath.SeagullFlockAnim.Fly)));
            Assert.AreEqual(SeagullStates.Glide,
                            b.IdOf(b.StateOf(SeagullFlockMath.SeagullFlockAnim.Glide)));
            Assert.AreEqual(SeagullStates.Swoop,
                            b.IdOf(b.StateOf(SeagullFlockMath.SeagullFlockAnim.Swoop)));
        }

        // =========================================================================================
        //  3. which end of the altitude pair the bird starts at
        // =========================================================================================

        /// <summary>
        /// 🔴 The one that would have bitten. The drop writes the dive as <c>alt: [0.3, 8]</c> — LOW
        /// first — while the rig's own README describes the descent as "8 → 0.3". Read the pair as
        /// [start, end] and the dive flies UPWARDS out of the sea. So the pair is a RANGE, not a path,
        /// and which end the bird enters at is taken off the sign of <c>vz</c>: a state that climbs
        /// starts at the bottom, a state that descends starts at the top.
        /// </summary>
        [Test]
        public void TheDiveEntersHighEvenThoughTheDropWritesItLow()
        {
            var def = Def();
            var b = def.Behaviour;
            int dive = b.IndexOf(SeagullStates.Dive);

            var row = b.Row(dive);
            Assert.Less(row.AltitudeA, row.AltitudeB,
                        "the drop no longer ships dive's altitude pair low-end first — " +
                        "this test's whole premise was that it does");
            Assert.Less(row.ClimbMetresPerSecond, 0.0, "a dive that does not descend is not a dive");

            Assert.AreEqual(8.0, b.EntryAltitude(dive), Eps, "the dive must START at the high end");
            Assert.AreEqual(0.3, b.ExitAltitude(dive), Eps, "and FINISH at the low end");
            Assert.Greater(b.EntryAltitude(dive), b.ExitAltitude(dive), "a dive goes down");
        }

        /// <summary>The other three oneshots, so the entry/exit rule is checked on a descent whose pair
        /// is written high-first (<c>land</c>), on a climb (<c>takeoff</c>), and on a state pinned flat
        /// at the waterline (<c>splash</c>).</summary>
        [Test]
        public void TheOtherOneshotsEnterAndLeaveWhereTheirClimbSaysTheyDo()
        {
            var b = Def().Behaviour;

            int land = b.IndexOf(SeagullStates.Land);
            Assert.AreEqual(0.6, b.EntryAltitude(land), Eps, "land enters at flare height");
            Assert.AreEqual(0.0, b.ExitAltitude(land), Eps, "and finishes on the ground");

            int takeoff = b.IndexOf(SeagullStates.Takeoff);
            Assert.Greater(b.Row(takeoff).ClimbMetresPerSecond, 0.0);
            Assert.AreEqual(0.0, b.EntryAltitude(takeoff), Eps, "a takeoff starts on the surface");
            Assert.AreEqual(1.2, b.ExitAltitude(takeoff), Eps, "and hands to fly at climb-out height");

            int splash = b.IndexOf(SeagullStates.Splash);
            Assert.AreEqual(0.0, b.EntryAltitude(splash), Eps);
            Assert.AreEqual(0.0, b.ExitAltitude(splash), Eps);
        }

        /// <summary>Every state's min/max is the pair sorted, whichever order the drop wrote it in, and
        /// no state ever asks the presenter for a negative altitude — a bird below the ground would
        /// draw under its own shadow.</summary>
        [Test]
        public void NoStateEverAsksToBeBelowTheGround()
        {
            var b = Def().Behaviour;
            for (int s = 0; s < b.StateCount; s++)
            {
                var row = b.Row(s);
                double lo = Math.Min(row.AltitudeA, row.AltitudeB);
                double hi = Math.Max(row.AltitudeA, row.AltitudeB);
                Assert.AreEqual(lo, b.AltitudeMin(s), Eps, b.IdOf(s) + ": min");
                Assert.AreEqual(hi, b.AltitudeMax(s), Eps, b.IdOf(s) + ": max");
                Assert.GreaterOrEqual(lo, 0.0, b.IdOf(s) + ": altitude floor");
                Assert.GreaterOrEqual(b.EntryAltitude(s), 0.0, b.IdOf(s) + ": entry");
                Assert.GreaterOrEqual(b.ExitAltitude(s), 0.0, b.IdOf(s) + ": exit");
            }
        }

        /// <summary>A state the bird is ON a surface in sits at altitude zero at both ends. The pivot is
        /// the surface; the PlayMode landing test reads exactly this number when it checks the sprite
        /// has come down onto its shadow.</summary>
        [Test]
        public void AStateOnASurfaceSitsAtAltitudeZero()
        {
            var b = Def().Behaviour;
            int onSurface = 0;
            for (int s = 0; s < b.StateCount; s++)
            {
                if (b.SurfaceOf(s) == SeagullSurface.None) continue;
                onSurface++;
                Assert.AreEqual(0.0, b.AltitudeMin(s), Eps, b.IdOf(s) + ": min");
                Assert.AreEqual(0.0, b.AltitudeMax(s), Eps, b.IdOf(s) + ": max");
            }
            Assert.AreEqual(6, onSurface, "the drop puts six states on a surface");
        }

        // =========================================================================================
        //  4. strict world scale — the owner's ruling of 2026-09-09
        // =========================================================================================

        /// <summary>
        /// 32 px to the metre, so a 1.40 m herring gull is 44.8 px across — and the cell the artist
        /// drew has to be wide enough to hold it. If the sheet's pixels-per-unit ever drifts off 32 the
        /// bird stops being the same animal as the boats it lands next to.
        /// </summary>
        [Test]
        public void TheGullIsFortyFivePixelsAcrossBecauseSheIsOnePointFourMetresAcross()
        {
            var def = Def();
            Assert.AreEqual(PixelsPerMetre, def.PixelsPerUnit, Eps,
                            "the sheet left the project's 32 px = 1 m");
            Assert.AreEqual(1.4f, (float)def.WingspanMetres, Eps, "the drop's herring gull spans 1.4 m");

            float spanPixels = (float)def.WingspanMetres * def.PixelsPerUnit;
            Assert.AreEqual(WingspanPixels, spanPixels, 0.05f, "wingspan in sheet pixels");
            Assert.LessOrEqual(spanPixels, def.CellWidth,
                               "the bird is wider than the cell she is drawn in");
        }

        /// <summary>
        /// 🔴 The owner, 2026-09-09: "its scale is appropriate for its height/proximity to camera
        /// (there aren't giant gulls that fly across the camera)". The fix is NOT a size knob — it is
        /// strict world scale at every altitude, with height carried as a screen OFFSET
        /// (<c>pivot − altitude · cos 40° · 32 px</c>) and the shadow left on the ground. This holds
        /// both ends of that shut: the config carries no proximity field to turn, and the presenter
        /// never writes a scale to turn it with.
        /// </summary>
        [Test]
        public void NothingScalesAGullByHowCloseItIs()
        {
            var banned = new Regex(@"scale|proximity|distance|nearness|zoom|perspective|shrink|grow",
                                   RegexOptions.IgnoreCase);
            foreach (var field in typeof(GullConfig).GetFields(BindingFlags.Public |
                                                               BindingFlags.Instance))
                Assert.IsFalse(banned.IsMatch(field.Name),
                               "GullConfig." + field.Name + " looks like the size-by-proximity knob " +
                               "the owner ruled out on 2026-09-09 — altitude is a screen offset, " +
                               "not a sprite scale");

            string code = CodeOf("GullFlock.cs");
            foreach (var write in new[] { @"\blocalScale\b", @"\blossyScale\b", @"\.Scale\b" })
                Assert.IsFalse(Regex.IsMatch(code, write),
                               "GullFlock.cs writes " + write + " — the presenter must never scale a " +
                               "bird; the wheel's per-bird Scale is produced and deliberately dropped");

            Assert.IsTrue(Regex.IsMatch(code, @"IsoGround\s*\.\s*HeightScale"),
                          "GullFlock.cs no longer converts altitude to a screen offset through " +
                          "IsoGround.HeightScale — if that went away, the scan above is testing " +
                          "a file that no longer draws the bird's height at all");
        }

        /// <summary>The config is still a serialized struct on the presenter, so the owner can still
        /// tune the flock from the inspector — the charter's "GullConfig keeps its Def home".</summary>
        [Test]
        public void TheOwnerCanStillTuneTheFlockFromTheInspector()
        {
            Assert.IsTrue(typeof(GullConfig).IsSerializable ||
                          typeof(GullConfig).IsDefined(typeof(SerializableAttribute), false),
                          "GullConfig lost [Serializable] and stopped showing in the inspector");

            var field = typeof(GullFlock).GetField("_config", BindingFlags.NonPublic |
                                                              BindingFlags.Instance);
            Assert.IsNotNull(field, "GullFlock no longer carries a _config field");
            Assert.AreEqual(typeof(GullConfig), field.FieldType);
            Assert.IsTrue(field.IsDefined(typeof(SerializeField), false),
                          "GullFlock._config is not [SerializeField] — nothing the owner types in the " +
                          "inspector would survive a domain reload");
        }

        /// <summary>Reads a production source file with its comments stripped, so a scan cannot be
        /// fooled by the word it is hunting for appearing inside a doc comment that says the code does
        /// not do it. The length guard is there so a moved or renamed file fails loudly rather than
        /// passing every assertion against an empty string.</summary>
        static string CodeOf(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Code", "Art", fileName);
            Assert.IsTrue(File.Exists(path), "missing source: " + path);
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            text = Regex.Replace(text, @"//[^\r\n]*", " ");
            Assert.Greater(text.Trim().Length, 200, fileName + ": nothing survived the comment strip");
            return text;
        }
    }
}
