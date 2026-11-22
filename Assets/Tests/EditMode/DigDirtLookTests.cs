using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The dig's dirt reads</b> (owner, on the dig: "Yes, make it show"). The clods were sand on sand at
    /// 0.7 alpha and, through a sprite four texels at 48 ppu, under a centimetre across: a probe found 20 to 40
    /// changed pixels a frame. These pin the look's data (three wet tones that hold a contrast floor against
    /// the dry flat, solid alpha), the clod's world size, the toss off the blade at the lift, the pool's
    /// ceiling and the press that drops a toss still on the blade. Presentation only: no clam, yield, gate or
    /// save is touched, so nothing here needs a dig rig.
    /// </summary>
    public sealed class DigDirtLookTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private MomentBurstEmitter Emitter()
        {
            var go = new GameObject("bursts");
            _spawned.Add(go);
            var em = go.AddComponent<MomentBurstEmitter>();
            em.OverrideSettings(JuiceSettings.Default);
            return em;
        }

        private static SpriteRenderer[] Shown(MomentBurstEmitter em) =>
            em.GetComponentsInChildren<SpriteRenderer>(false);

        // ---- the colour ---------------------------------------------------------------------------

        [Test]
        public void Every_wet_tone_holds_the_contrast_floor_against_the_dry_flat()
        {
            var c = MomentBurstConfig.Default;
            Assert.GreaterOrEqual(c.DirtMinContrast, 1.5f, "the floor itself is a real floor");
            foreach (var (name, tone) in new[] { ("dark", c.DirtDark), ("mid", c.DirtMid), ("light", c.DirtLight) })
            {
                float ratio = MomentBurstMath.ContrastRatio(tone, c.FlatReferenceColor);
                Assert.GreaterOrEqual(ratio, c.DirtMinContrast, $"the {name} tone against the flat: {ratio:0.00}");
                Assert.Less(MomentBurstMath.RelativeLuminance(tone), MomentBurstMath.RelativeLuminance(c.FlatReferenceColor),
                    $"wet dirt reads DARKER than the dry flat ({name})");
            }
        }

        [Test]
        public void The_three_tones_step_from_dark_to_light_and_are_all_used()
        {
            var c = MomentBurstConfig.Default;
            Assert.Less(MomentBurstMath.RelativeLuminance(c.DirtDark), MomentBurstMath.RelativeLuminance(c.DirtMid));
            Assert.Less(MomentBurstMath.RelativeLuminance(c.DirtMid), MomentBurstMath.RelativeLuminance(c.DirtLight));
            Assert.AreEqual(0, MomentBurstMath.DirtTone(0f));
            Assert.AreEqual(1, MomentBurstMath.DirtTone(0.5f));
            Assert.AreEqual(2, MomentBurstMath.DirtTone(0.999f));
            Assert.AreEqual(2, MomentBurstMath.DirtTone(1f), "clamped: never a fourth tone");
            Assert.AreEqual(0, MomentBurstMath.DirtTone(-3f), "clamped");
        }

        [Test]
        public void The_old_sand_tint_would_have_failed_the_floor()
        {
            // The colour the dirt used to be, kept as the reason the floor exists.
            var oldSand = new Color(0.62f, 0.52f, 0.36f, 1f);
            Assert.Less(MomentBurstMath.ContrastRatio(oldSand, MomentBurstConfig.Default.FlatReferenceColor),
                        MomentBurstConfig.Default.DirtMinContrast);
            Assert.AreEqual(1f, MomentBurstMath.ContrastRatio(Color.gray, Color.gray), 1e-5f, "identical colours: 1");
        }

        [Test]
        public void A_clod_is_solid_and_the_size_its_data_says()
        {
            var c = MomentBurstConfig.Default;
            Assert.GreaterOrEqual(c.ChunkMaxAlpha, 0.95f, "wet dirt is solid, not a 0.7 haze");
            var em = Emitter();
            em.OnMoment(new JuiceMomentCue(JuiceMoment.DigImpact, new Vector2(4f, -20f), 0.1f));
            em.Tick(0.03f);
            var shown = Shown(em);
            Assert.AreEqual(JuiceSettings.Default.SandChunkCount, shown.Length, "the impact's clods; the toss is still on the blade");
            foreach (var sr in shown)
            {
                Assert.GreaterOrEqual(sr.color.a, c.ChunkMaxAlpha * (1f - c.NightFade) - 1e-3f,
                    "solid from the first tick (no fade-in), whatever the hour does");
                float world = sr.bounds.size.x;
                Assert.GreaterOrEqual(world, c.ChunkSize * 0.7f, "a clod is ChunkSize across in the world, not a speck");
                Assert.LessOrEqual(world, c.ChunkSize * 1.3f);
                Assert.GreaterOrEqual(world * 32f, 2f, "at least two art pixels across (1 art px = 1/32 m)");
            }
        }

        // ---- the motion ---------------------------------------------------------------------------

        [Test]
        public void The_clods_lift_out_of_the_hole_land_round_it_and_lie_still()
        {
            var c = MomentBurstConfig.Default;
            var j = JuiceSettings.Default;
            var hole = new Vector2(10f, 5f);
            var em = Emitter();
            em.OnMoment(new JuiceMomentCue(JuiceMoment.DigImpact, hole, 0.1f));

            const float dt = 0.005f;
            float t = 0f, maxLift = 0f;
            float airtime = 2f * c.ChunkUp / c.ChunkGravity;          // the highest throw's flight
            while (t < airtime * 0.5f) { em.Tick(dt); t += dt; foreach (var sr in Shown(em)) maxLift = Mathf.Max(maxLift, sr.transform.position.y - hole.y); }
            Assert.Greater(maxLift, 0.03f, "they lift out of the hole (an arc, not a slide)");

            while (t < airtime + 0.02f) { em.Tick(dt); t += dt; }
            Assert.Less(t + 0.01f, j.SandChunkSeconds * 0.85f, "the fixture checks before the shortest clod dies");
            var before = Shown(em).Select(sr => sr.transform.position).ToArray();
            em.Tick(0.01f); t += 0.01f;
            var after = Shown(em).Select(sr => sr.transform.position).ToArray();
            Assert.AreEqual(j.SandChunkCount, before.Length);
            Assert.AreEqual(before.Length, after.Length);
            float reach = c.ChunkSpeed * airtime + 0.01f;
            for (int i = 0; i < before.Length; i++)
            {
                Assert.AreEqual(before[i], after[i], "landed: it lies there");
                Assert.LessOrEqual(Vector2.Distance(after[i], hole), reach, "round the hole, not across the flat");
            }

            em.Tick(j.SandChunkSeconds - t + 0.001f);
            Assert.AreEqual(0, em.AliveCount, "the impact's clods are gone by their life (the toss is its own cue)");
        }

        // ---- the toss: off the blade tip, to the side he throws -----------------------------------

        [Test]
        public void The_toss_leaves_from_the_blade_tip_not_the_hole()
        {
            var j = JuiceSettings.Default;
            Assert.Greater(j.DigTossChunkCount, 0, "the toss ships on");
            var blade = new Vector2(7.4f, 3.1f);
            const float lift = 0.42f;
            // The clods stream off the blade one after another (DigTossReleaseSeconds, owner's bigger toss
            // 2026-11-22); each is born at the tip. Released all at once, every clod starts there together.
            var once = j;
            once.DigTossReleaseSeconds = 0f;
            var em = Emitter();
            em.OverrideSettings(once);
            em.OnMoment(new JuiceMomentCue(JuiceMoment.DigToss, blade, 0.1f, lift, Vector2.right));
            em.Tick(0.001f);
            var shown = Shown(em);
            Assert.AreEqual(j.DigTossChunkCount, shown.Length, "the whole clump at the lift");
            foreach (var sr in shown)
            {
                Vector3 p = sr.transform.position;
                Assert.AreEqual(blade.x, p.x, 0.02f, "at the tip's x");
                Assert.AreEqual(blade.y + lift, p.y, 0.02f, "at the tip's drawn height, over the ground under it");
            }

            Assert.Greater(j.DigTossReleaseSeconds, 0f, "the shipped toss streams off the blade");
            var stream = Emitter();
            stream.OnMoment(new JuiceMomentCue(JuiceMoment.DigToss, blade, 0.1f, lift, Vector2.right));
            stream.Tick(0.001f);
            Assert.AreEqual(1, Shown(stream).Length, "the first clod leaves alone");
            stream.Tick(j.DigTossReleaseSeconds);
            Assert.AreEqual(j.DigTossChunkCount, Shown(stream).Length, "and the rest are off the blade by the release");
        }

        [Test]
        // The owner asked for a BIGGER toss (2026-11-22): this used to pin "low: it barely rises off the blade";
        // the toss now arcs visibly, so the test pins the arc instead (high enough to see, no higher than its
        // up-throw allows) and that it lands and lies there before it fades.
        public void The_toss_arcs_to_the_side_he_throws_and_lands_beside_him()
        {
            var c = MomentBurstConfig.Default;
            var j = JuiceSettings.Default;
            var blade = new Vector2(0f, 0f);
            const float lift = 0.4f;
            foreach (Vector2 dir in new[] { Vector2.right, Vector2.left, new Vector2(1f, -1f).normalized })
            {
                var em = Emitter();
                em.OnMoment(new JuiceMomentCue(JuiceMoment.DigToss, blade, 0.1f, lift, dir));
                const float dt = 0.005f;
                float peak = float.MinValue;
                // Flight: lift + up-throw, down at ChunkGravity. The fastest-rising clod's flight bounds it.
                float vUp = c.TossUp * j.DigTossUpScale;
                float flight = (vUp + Mathf.Sqrt(vUp * vUp + 2f * c.ChunkGravity * lift)) / c.ChunkGravity;
                float allDown = j.DigTossReleaseSeconds + flight + 0.05f;   // the last clod leaves last
                Assert.Less(allDown, j.DigTossChunkSeconds * 0.5f, "it lands and lies there a good while before it fades");
                for (float t = 0f; t < allDown; t += dt)
                {
                    em.Tick(dt);
                    foreach (var sr in Shown(em)) peak = Mathf.Max(peak, sr.transform.position.y - (blade.y + lift));
                }
                // On screen a clod's y is its ground y plus its height; the sideways spread can carry the ground
                // up the screen by at most half the spread over the flight.
                float drift = c.TossSpeed * c.TossSpread * j.DigTossSpreadScale * 0.5f * c.ChunkGroundYScale * flight;
                Assert.LessOrEqual(peak, vUp * vUp / (2f * c.ChunkGravity) + drift + 0.01f, "no higher than its up-throw");
                if (Mathf.Approximately(dir.y, 0f))   // a throw toward the camera also runs down the screen
                    Assert.GreaterOrEqual(peak, 0.15f, "a visible arc: about five art pixels up at the top");
                var landed = Shown(em);
                Assert.AreEqual(j.DigTossChunkCount, landed.Length);
                foreach (var sr in landed)
                {
                    Vector2 off = (Vector2)sr.transform.position - blade;
                    Vector2 ground = new Vector2(off.x, off.y / Mathf.Max(1e-4f, c.ChunkGroundYScale));
                    Assert.Greater(Vector2.Dot(ground, dir), 0.05f, $"thrown along {dir}, landed at {off}");
                }
                var before = landed.Select(sr => sr.transform.position).ToArray();
                em.Tick(0.02f);
                var after = Shown(em).Select(sr => sr.transform.position).ToArray();
                for (int i = 0; i < before.Length; i++) Assert.AreEqual(before[i], after[i], "landed: it lies there");
                em.Tick(j.DigTossChunkSeconds + j.DigTossReleaseSeconds);
                Assert.AreEqual(0, em.AliveCount, "and is gone by its life");
            }
        }

        [Test]
        public void A_toss_clod_is_a_bigger_lump_than_the_impacts_spatter_and_solid()
        {
            var c = MomentBurstConfig.Default;
            Assert.GreaterOrEqual(c.TossSizeScale, 1f, "the shovel's load: lumps at least the impact clod's size");
            var em = Emitter();
            em.OnMoment(new JuiceMomentCue(JuiceMoment.DigToss, Vector2.zero, 0.1f, 0.3f, Vector2.right));
            em.Tick(0.01f);
            foreach (var sr in Shown(em))
            {
                Assert.GreaterOrEqual(sr.bounds.size.x * 32f, 3f, "at least three art pixels across (1 art px = 1/32 m)");
                Assert.GreaterOrEqual(sr.color.a, c.ChunkMaxAlpha * (1f - c.NightFade) - 1e-3f, "solid");
            }
        }

        [Test]
        public void A_toss_with_no_blade_falls_back_to_the_old_lift_and_one_side()
        {
            var c = MomentBurstConfig.Default;
            var em = Emitter();
            var at = new Vector2(2f, 2f);
            em.OnMoment(new JuiceMomentCue(JuiceMoment.DigToss, at, 0.1f));
            em.Tick(0.001f);
            foreach (var sr in Shown(em))
                Assert.AreEqual(at.y + c.TossLiftHeight, sr.transform.position.y, 0.02f, "the sprite body's lift height");
            em.Tick(0.3f);
            var xs = Shown(em).Select(sr => sr.transform.position.x - at.x).ToArray();
            Assert.IsTrue(xs.All(x => x > 0f) || xs.All(x => x < 0f), "one side per dig");
        }

        [Test]
        public void The_impact_spawns_no_toss_and_a_press_spawns_nothing()
        {
            var j = JuiceSettings.Default;
            var em = Emitter();
            em.OnMoment(new JuiceMomentCue(JuiceMoment.DigStrike, Vector2.zero, 0.1f));
            Assert.AreEqual(0, em.AliveCount, "the press: the shovel is still in the air");
            em.OnMoment(new JuiceMomentCue(JuiceMoment.DigImpact, Vector2.zero, 0.1f));
            Assert.AreEqual(j.SandChunkCount, em.AliveCount, "the impact: its own clods only; the relay sends the toss at the lift");
        }

        // ---- the pool -----------------------------------------------------------------------------

        [Test]
        public void Digging_fast_never_grows_the_pool()
        {
            var em = Emitter();
            em.OnMoment(new JuiceMomentCue(JuiceMoment.DigImpact, Vector2.zero, 0.1f));   // builds the pool (EditMode runs no Awake)
            int renderers = em.GetComponentsInChildren<SpriteRenderer>(true).Length;
            Assert.AreEqual(MomentBurstConfig.Default.PoolSize, renderers, "built once at its size");
            int perDig = JuiceSettings.Default.SandChunkCount + JuiceSettings.Default.DigTossChunkCount;
            Assert.LessOrEqual(perDig, MomentBurstConfig.Default.PoolSize, "one dig fits the pool");
            for (int i = 0; i < 20; i++)
            {
                em.OnMoment(new JuiceMomentCue(JuiceMoment.DigImpact, new Vector2(i, 0f), 0.1f));
                em.OnMoment(new JuiceMomentCue(JuiceMoment.DigToss, new Vector2(i, 0f), 0.1f, 0.3f, Vector2.right));
                em.Tick(0.05f);
            }
            Assert.LessOrEqual(em.AliveCount, MomentBurstConfig.Default.PoolSize, "the pool is the ceiling (rule 7)");
            Assert.AreEqual(renderers, em.GetComponentsInChildren<SpriteRenderer>(true).Length, "no renderer added");
        }
    }
}
