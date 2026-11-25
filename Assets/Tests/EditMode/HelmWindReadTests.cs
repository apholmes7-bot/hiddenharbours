using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The helm wind read (helm instruments plan, slice 3): <see cref="HelmWind.TryRead"/> is the
    /// sails' own wind resolve behind the fitted grade, and <see cref="BoatEquipment.EffectiveFit"/>
    /// decides that grade from console data plus the owned-per-hull ids.
    ///
    /// <para>Three things are pinned here. The apparent-wind arithmetic at the angles a sailor names
    /// (head to wind, beam, running, at rest). The K1 guard: the instrument's numbers ARE
    /// <see cref="SailDrive.ResolveWind"/>'s, for any heading and any hull speed, so a needle and the
    /// sails cannot disagree. The grade gate: no wind fit reads nothing, the dial hides true wind.</para>
    /// </summary>
    public class HelmWindReadTests
    {
        private const float Tol = 1e-3f;

        // A fresh breeze and a working boat speed, in the sim's own unit (m/s). Test inputs, not tuning.
        private const float WindMs = 6f;
        private const float BoatMs = 3f;

        private static readonly Vector2 North = Vector2.up;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private HelmConsoleDef Console(WindInstrumentGrade defaultWind, WindInstrumentGrade maxWind)
        {
            var c = ScriptableObject.CreateInstance<HelmConsoleDef>();
            c.Rig = ConsoleRigKind.Novi;
            c.DefaultWind = defaultWind;
            c.MaxWind = maxWind;
            _spawned.Add(c);
            return c;
        }

        private static HelmWindReading Read(WindInstrumentGrade grade, Vector2 windMs, Vector2 bow, Vector2 vel)
        {
            Assert.That(HelmWind.TryRead(grade, windMs, bow, vel, out HelmWindReading r), Is.True);
            return r;
        }

        // ---- the apparent-wind maths -----------------------------------------------------------

        [Test]
        public void At_rest_apparent_wind_equals_true_wind()
        {
            // Wind blowing toward the south-west, boat heading north, not moving.
            var wind = new Vector2(-3f, -4f);
            HelmWindReading r = Read(WindInstrumentGrade.Display, wind, North, Vector2.zero);

            Assert.That(r.HasTrue, Is.True);
            Assert.That(r.ApparentAngleDeg, Is.EqualTo(r.TrueAngleDeg).Within(Tol));
            Assert.That(r.ApparentKn, Is.EqualTo(r.TrueKn).Within(Tol));
            Assert.That(r.TrueKn, Is.EqualTo(SailDrive.ToKnots(wind.magnitude)).Within(Tol));
        }

        [Test]
        public void Head_to_wind_the_boats_speed_adds_to_the_wind()
        {
            // The wind vector points DOWNWIND: blowing south is a northerly, dead on a north-heading bow.
            Vector2 wind = Vector2.down * WindMs;
            HelmWindReading r = Read(WindInstrumentGrade.Display, wind, North, North * BoatMs);

            Assert.That(r.TrueAngleDeg, Is.EqualTo(0f).Within(Tol));
            Assert.That(r.ApparentAngleDeg, Is.EqualTo(0f).Within(Tol));
            Assert.That(r.TrueKn, Is.EqualTo(SailDrive.ToKnots(WindMs)).Within(Tol));
            Assert.That(r.ApparentKn, Is.EqualTo(SailDrive.ToKnots(WindMs + BoatMs)).Within(Tol));
        }

        [Test]
        public void Running_the_boats_speed_comes_off_the_wind()
        {
            // Blowing north onto the stern of a north-heading boat that is slower than the wind.
            Vector2 wind = North * WindMs;
            HelmWindReading r = Read(WindInstrumentGrade.Display, wind, North, North * BoatMs);

            Assert.That(Mathf.Abs(r.TrueAngleDeg), Is.EqualTo(180f).Within(Tol));
            Assert.That(Mathf.Abs(r.ApparentAngleDeg), Is.EqualTo(180f).Within(Tol));
            Assert.That(r.ApparentKn, Is.EqualTo(SailDrive.ToKnots(WindMs - BoatMs)).Within(Tol));
        }

        [Test]
        public void On_the_beam_the_apparent_wind_draws_ahead_and_freshens()
        {
            // Blowing west = an easterly = over the STARBOARD side of a north-heading boat: +90 true.
            Vector2 wind = Vector2.left * WindMs;
            HelmWindReading still = Read(WindInstrumentGrade.Display, wind, North, Vector2.zero);
            Assert.That(still.TrueAngleDeg, Is.EqualTo(90f).Within(Tol));
            Assert.That(still.ApparentAngleDeg, Is.EqualTo(90f).Within(Tol));

            // Moving ahead at the wind's own speed: apparent is exactly 45 off the bow, root-two stronger.
            HelmWindReading moving = Read(WindInstrumentGrade.Display, wind, North, North * WindMs);
            Assert.That(moving.TrueAngleDeg, Is.EqualTo(90f).Within(Tol), "true wind does not move with her speed");
            Assert.That(moving.ApparentAngleDeg, Is.EqualTo(45f).Within(Tol));
            Assert.That(moving.ApparentKn, Is.EqualTo(SailDrive.ToKnots(WindMs * Mathf.Sqrt(2f))).Within(Tol));

            // The same breeze over the PORT side reads the mirror image, negative.
            HelmWindReading port = Read(WindInstrumentGrade.Display, Vector2.right * WindMs, North, North * WindMs);
            Assert.That(port.ApparentAngleDeg, Is.EqualTo(-45f).Within(Tol));
        }

        [Test]
        public void Speeds_are_knots()
        {
            // One knot of wind, stated in metres per second, reads as one knot.
            Vector2 wind = Vector2.down * SailDrive.MetresPerSecondPerKnot;
            HelmWindReading r = Read(WindInstrumentGrade.Display, wind, North, Vector2.zero);
            Assert.That(r.TrueKn, Is.EqualTo(1f).Within(Tol));
            Assert.That(r.ApparentKn, Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void The_instrument_reads_exactly_the_sails_wind_at_any_heading_and_speed()
        {
            // The K1 guard. A sweep of headings, winds and hull speeds (a hull at rest, a sloop's pace,
            // a powerboat's): every field must be the sails' own number, bit for bit.
            var wind = new Vector2(-4.5f, 2.5f);
            float[] speedsMs = { 0f, 2f, 3.5f, 12f };
            for (int h = 0; h < 360; h += 15)
            {
                float rad = h * Mathf.Deg2Rad;
                var bow = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                foreach (float speed in speedsMs)
                {
                    Vector2 vel = bow * speed;
                    SailWind sails = SailDrive.ResolveWind(wind, bow, vel);
                    HelmWindReading r = Read(WindInstrumentGrade.Display, wind, bow, vel);

                    Assert.That(r.ApparentAngleDeg, Is.EqualTo(sails.ApparentAngleDeg), $"heading {h}, {speed} m/s");
                    Assert.That(r.ApparentKn, Is.EqualTo(sails.ApparentKn), $"heading {h}, {speed} m/s");
                    Assert.That(r.TrueAngleDeg, Is.EqualTo(sails.TrueAngleDeg), $"heading {h}, {speed} m/s");
                    Assert.That(r.TrueKn, Is.EqualTo(sails.TrueKn), $"heading {h}, {speed} m/s");
                }
            }
        }

        [Test]
        public void The_same_inputs_always_give_the_same_reading()
        {
            var wind = new Vector2(1.5f, -5f);
            var bow = new Vector2(0.6f, 0.8f);
            Vector2 vel = bow * BoatMs;
            HelmWindReading a = Read(WindInstrumentGrade.Display, wind, bow, vel);
            HelmWindReading b = Read(WindInstrumentGrade.Display, wind, bow, vel);
            Assert.That(a, Is.EqualTo(b));
        }

        // ---- the grade gate on the read --------------------------------------------------------

        [Test]
        public void No_wind_instrument_reads_nothing()
        {
            bool read = HelmWind.TryRead(WindInstrumentGrade.None, Vector2.down * WindMs, North, North * BoatMs,
                                         out HelmWindReading r);
            Assert.That(read, Is.False);
            Assert.That(r, Is.EqualTo(default(HelmWindReading)));
        }

        [Test]
        public void The_dial_reads_apparent_wind_and_hides_true_wind()
        {
            Vector2 wind = Vector2.left * WindMs;
            Vector2 vel = North * WindMs;
            HelmWindReading dial = Read(WindInstrumentGrade.Dial, wind, North, vel);
            HelmWindReading display = Read(WindInstrumentGrade.Display, wind, North, vel);

            Assert.That(dial.ApparentAngleDeg, Is.EqualTo(display.ApparentAngleDeg));
            Assert.That(dial.ApparentKn, Is.EqualTo(display.ApparentKn));
            Assert.That(dial.HasTrue, Is.False);
            Assert.That(dial.TrueAngleDeg, Is.EqualTo(0f), "a hidden reading is zeroed, not left readable");
            Assert.That(dial.TrueKn, Is.EqualTo(0f));
            Assert.That(display.HasTrue, Is.True);
            Assert.That(display.TrueKn, Is.GreaterThan(0f));
        }

        [Test]
        public void A_reading_that_is_not_a_number_is_refused()
        {
            var bad = new Vector2(float.NaN, 0f);
            var inf = new Vector2(0f, float.PositiveInfinity);
            Assert.That(HelmWind.TryRead(WindInstrumentGrade.Display, bad, North, Vector2.zero, out _), Is.False);
            Assert.That(HelmWind.TryRead(WindInstrumentGrade.Display, Vector2.down, bad, Vector2.zero, out _), Is.False);
            Assert.That(HelmWind.TryRead(WindInstrumentGrade.Display, Vector2.down, North, inf, out _), Is.False);
        }

        [Test]
        public void A_flat_calm_is_a_reading_of_zero_knots()
        {
            HelmWindReading r = Read(WindInstrumentGrade.Display, Vector2.zero, North, Vector2.zero);
            Assert.That(r.TrueKn, Is.EqualTo(0f));
            Assert.That(r.ApparentKn, Is.EqualTo(0f));
        }

        // ---- the grade as data: console default, console ceiling, owned ids ---------------------

        [Test]
        public void The_instrument_ids_are_stable()
        {
            Assert.That(BoatEquipment.WindDialId, Is.EqualTo("instrument.wind_dial"));
            Assert.That(BoatEquipment.WindDisplayId, Is.EqualTo("instrument.wind_display"));
        }

        [Test]
        public void A_fresh_console_and_a_hull_with_no_console_carry_no_wind_instrument()
        {
            var fresh = ScriptableObject.CreateInstance<HelmConsoleDef>();
            _spawned.Add(fresh);
            Assert.That(BoatEquipment.EffectiveFit(fresh, null).Wind, Is.EqualTo(WindInstrumentGrade.None));
            Assert.That(BoatEquipment.EffectiveFit((HelmConsoleDef)null, null).Wind, Is.EqualTo(WindInstrumentGrade.None));
            Assert.That(HelmFit.None.Wind, Is.EqualTo(WindInstrumentGrade.None));

            // The pre-wind constructor still means "no wind instrument".
            var old = new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.None, false, false);
            Assert.That(old.Wind, Is.EqualTo(WindInstrumentGrade.None));
        }

        [Test]
        public void A_hull_with_no_wind_fit_reads_nothing_even_when_wind_ids_are_owned()
        {
            // The console cannot take a wind instrument at all: owning both changes nothing.
            var console = Console(WindInstrumentGrade.None, WindInstrumentGrade.None);
            var owned = new HashSet<string> { BoatEquipment.WindDialId, BoatEquipment.WindDisplayId };
            HelmFit fit = BoatEquipment.EffectiveFit(console, owned);

            Assert.That(fit.Wind, Is.EqualTo(WindInstrumentGrade.None));
            Assert.That(HelmWind.TryRead(fit.Wind, Vector2.down * WindMs, North, Vector2.zero, out _), Is.False);
        }

        [Test]
        public void The_console_default_reads_through_and_is_capped_by_its_ceiling()
        {
            Assert.That(BoatEquipment.EffectiveFit(Console(WindInstrumentGrade.Dial, WindInstrumentGrade.Display), null).Wind,
                        Is.EqualTo(WindInstrumentGrade.Dial));
            Assert.That(BoatEquipment.EffectiveFit(Console(WindInstrumentGrade.Display, WindInstrumentGrade.Display), null).Wind,
                        Is.EqualTo(WindInstrumentGrade.Display));
            Assert.That(BoatEquipment.EffectiveFit(Console(WindInstrumentGrade.Display, WindInstrumentGrade.Dial), null).Wind,
                        Is.EqualTo(WindInstrumentGrade.Dial), "a default above the ceiling is capped");
        }

        [Test]
        public void Owned_wind_instruments_fit_only_where_the_console_can_take_them()
        {
            var dialOnly = Console(WindInstrumentGrade.None, WindInstrumentGrade.Dial);
            var full = Console(WindInstrumentGrade.None, WindInstrumentGrade.Display);
            var dial = new HashSet<string> { BoatEquipment.WindDialId };
            var display = new HashSet<string> { BoatEquipment.WindDisplayId };
            var both = new HashSet<string> { BoatEquipment.WindDialId, BoatEquipment.WindDisplayId };

            Assert.That(BoatEquipment.EffectiveFit(dialOnly, dial).Wind, Is.EqualTo(WindInstrumentGrade.Dial));
            Assert.That(BoatEquipment.EffectiveFit(dialOnly, display).Wind, Is.EqualTo(WindInstrumentGrade.None),
                        "the display does nothing on a helm that only takes the dial");
            Assert.That(BoatEquipment.EffectiveFit(dialOnly, both).Wind, Is.EqualTo(WindInstrumentGrade.Dial));
            Assert.That(BoatEquipment.EffectiveFit(full, dial).Wind, Is.EqualTo(WindInstrumentGrade.Dial));
            Assert.That(BoatEquipment.EffectiveFit(full, display).Wind, Is.EqualTo(WindInstrumentGrade.Display));
            Assert.That(BoatEquipment.EffectiveFit(full, both).Wind, Is.EqualTo(WindInstrumentGrade.Display),
                        "the display wins over the dial");
        }

        [Test]
        public void An_owned_dial_never_downgrades_a_shipped_display()
        {
            var console = Console(WindInstrumentGrade.Display, WindInstrumentGrade.Display);
            var dial = new HashSet<string> { BoatEquipment.WindDialId };
            Assert.That(BoatEquipment.EffectiveFit(console, dial).Wind, Is.EqualTo(WindInstrumentGrade.Display));
        }

        [Test]
        public void Wind_does_not_disturb_the_rest_of_the_fit()
        {
            var console = Console(WindInstrumentGrade.Dial, WindInstrumentGrade.Display);
            console.DefaultSounder = SounderKind.Depth;
            console.SupportsRadar = true;
            var owned = new HashSet<string> { BoatEquipment.RadarId, BoatEquipment.WindDisplayId };
            HelmFit fit = BoatEquipment.EffectiveFit(console, owned);

            Assert.That(fit.Rig, Is.EqualTo(ConsoleRigKind.Novi));
            Assert.That(fit.Sounder, Is.EqualTo(SounderKind.Depth));
            Assert.That(fit.Radar, Is.True);
            Assert.That(fit.Wind, Is.EqualTo(WindInstrumentGrade.Display));
        }

        // ---- the seam's default answer ---------------------------------------------------------

        [Test]
        public void A_glass_that_does_not_implement_the_wind_read_answers_nothing_fitted()
        {
            IHelmInstruments glass = new BareGlass();
            Assert.That(glass.TryReadWind(out HelmWindReading r), Is.False);
            Assert.That(r, Is.EqualTo(default(HelmWindReading)));
        }

        /// <summary>An implementer written before the wind read existed: it must still compile, and
        /// must read as "no wind instrument".</summary>
        private sealed class BareGlass : IHelmInstruments
        {
            public string HullId => "boat.test";
            public HelmFit Fit => HelmFit.None;
            public bool TryReadDepth(out float metres) { metres = 0f; return false; }
            public bool TryReadPosition(out Vector2 worldPos) { worldPos = Vector2.zero; return false; }
            public SounderPrefs SounderPrefs => default;
            public void SetSounderPrefs(in SounderPrefs prefs) { }
        }
    }
}
