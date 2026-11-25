using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>THE CLUMP LAW.</b> A pure function of world position and the field's seed that makes a recipe
    /// scatter read as placed by a hand: bare ground and dense clumps, tall plants in a clump's core and
    /// small ones at its rim, and a tone that steps from patch to patch.
    ///
    /// <para><b>Deterministic (rule 5).</b> No <c>Random</c>, no <c>Time</c>, no scene. The field is value
    /// noise on a world-metre lattice whose corners are the scatter's own hash
    /// (<see cref="PlantFieldScatter.Hash"/>), so the same seed grows the same clumps on every machine and
    /// nothing is saved. The arithmetic is floor, multiply and add in doubles: no transcendental that could
    /// differ between processors.</para>
    ///
    /// <para><b>Off is exact.</b> With no asset or <see cref="PlantClumpDef.Strength"/> 0,
    /// <see cref="IsOn"/> is false and the field takes the path it took before the law existed. The gain is
    /// also exactly 1 at strength 0 by its own arithmetic, so the two statements cannot drift apart.</para>
    ///
    /// <para><b>It never moves a plant or picks a species.</b> A cell's position, species, variant and
    /// young roll stay the scatter's. The law decides only WHETHER the cell grows (the same density roll
    /// against a scaled density, so at a gain of 1 or less the result is a subset of the shipped scatter),
    /// which growth STAGE it stands at, and which TONE step it wears.</para>
    /// </summary>
    public static class PlantClumpLaw
    {
        // Hash salts, in the scatter's own numbering (PlantFieldScatter uses 1..6). Identifiers, not
        // tunables: changing one re-rolls every clump on the island.
        const int SaltPatch = 21;
        const int SaltDetail = 22;
        const int SaltRim = 23;
        const int SaltCore = 24;
        const int SaltTone = 25;
        const int SaltStray = 26;

        /// <summary>Whether the law changes anything for this asset.</summary>
        public static bool IsOn(PlantClumpDef def) => def != null && def.Strength > 0f;

        /// <summary>
        /// The clump field at a world point, 0..1: low is bare ground, high is the core of a clump.
        /// Two octaves of value noise — the patches, and a finer breakup that keeps a clump's edge ragged.
        /// </summary>
        public static double Field(double worldX, double worldY, int seed, PlantClumpDef def)
        {
            double patch = Lattice(worldX, worldY, def.PatchMetres, seed, SaltPatch);
            double weight = Math.Max(0.0, def.DetailWeight);
            if (weight <= 0.0) return patch;
            return (patch + weight * Lattice(worldX, worldY, def.DetailMetres, seed, SaltDetail)) / (1.0 + weight);
        }

        /// <summary>How much of a clump stands here, 0..1: 0 at and below <see cref="PlantClumpDef.BareBelow"/>,
        /// 1 at and above <see cref="PlantClumpDef.FullAbove"/>, a straight ramp across the rim between.</summary>
        public static double Core(double field, PlantClumpDef def)
        {
            double rim = def.FullAbove - def.BareBelow;
            if (rim <= 1e-6) return field >= def.FullAbove ? 1.0 : 0.0;
            double k = (field - def.BareBelow) / rim;
            return k < 0.0 ? 0.0 : k > 1.0 ? 1.0 : k;
        }

        /// <summary>
        /// What the recipe's density is multiplied by at this point: 0 on bare ground, up to
        /// <see cref="PlantClumpDef.ClumpGain"/> in a core, blended toward 1 by the strength. Exactly 1
        /// at strength 0.
        /// </summary>
        public static double DensityGain(double field, PlantClumpDef def) =>
            1.0 + def.Strength * (Core(field, def) * Math.Max(1.0, def.ClumpGain) - 1.0);

        /// <summary>
        /// The growth stage a scattered plant stands at (0 mature, 1 young, 2 seedling), given the stage
        /// the recipe rolled. At a clump's rim it may step down one or two stages; in a core a young plant
        /// may stand mature. The art is the atlas's own stage cell — no sprite is scaled.
        /// </summary>
        public static int StageFor(int rolledStage, double field, int cellX, int cellY, int seed, PlantClumpDef def)
        {
            double core = Core(field, def);
            double down = def.Strength * def.RimStepChance * (1.0 - core);
            double roll = PlantFieldScatter.Hash(cellX, cellY, seed, SaltRim);
            int stage = rolledStage;
            if (roll < down * def.SecondStepShare) stage += 2;
            else if (roll < down) stage += 1;
            else if (stage == 1 && core >= 1.0 &&
                     PlantFieldScatter.Hash(cellX, cellY, seed, SaltCore) < def.Strength * def.CoreGrowChance) stage = 0;
            return stage > 2 ? 2 : stage;
        }

        /// <summary>
        /// Which tone step a scattered plant wears: an index into <see cref="PlantClumpDef.ToneSteps"/>,
        /// or -1 when the asset names none. Neighbours share a step (the drift is low-frequency and follows
        /// the clumps part of the way), with the odd stray one step off so a patch is not one flat tint.
        /// </summary>
        public static int ToneFor(double field, double worldX, double worldY, int cellX, int cellY, int seed,
                                  PlantClumpDef def)
        {
            int steps = def.ToneSteps != null ? Math.Min(def.ToneSteps.Length, PlantClumpDef.MaxToneSteps) : 0;
            if (steps == 0) return -1;
            double drift = Lattice(worldX, worldY, def.TonePatchMetres, seed, SaltTone);
            double follow = def.ToneFollowsClump < 0f ? 0.0 : def.ToneFollowsClump > 1f ? 1.0 : def.ToneFollowsClump;
            double tone = drift + (field - drift) * follow;
            int index = (int)Math.Floor(tone * steps);
            double stray = PlantFieldScatter.Hash(cellX, cellY, seed, SaltStray);
            if (stray < def.ToneStrayChance) index += stray < def.ToneStrayChance * 0.5 ? -1 : 1;
            return index < 0 ? 0 : index >= steps ? steps - 1 : index;
        }

        /// <summary>The tint of one tone step at this asset's strength: the step's colour, blended from
        /// white by the strength. A plant takes exactly one of these, so the tone is stepped at every
        /// strength.</summary>
        public static Color ToneColor(int step, PlantClumpDef def)
        {
            if (def == null || def.ToneSteps == null || step < 0 || step >= def.ToneSteps.Length) return Color.white;
            Color c = Color.Lerp(Color.white, def.ToneSteps[step], Mathf.Clamp01(def.Strength));
            c.a = 1f;
            return c;
        }

        /// <summary>Value noise on a lattice of <paramref name="metres"/>, 0..1, smoothstep between corners.</summary>
        static double Lattice(double worldX, double worldY, double metres, int seed, int salt)
        {
            if (!(metres > 1e-6)) metres = 1e-6;
            double fx = worldX / metres, fy = worldY / metres;
            double x0 = Math.Floor(fx), y0 = Math.Floor(fy);
            int ix = (int)x0, iy = (int)y0;
            double tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3.0 - 2.0 * tx);
            ty = ty * ty * (3.0 - 2.0 * ty);
            double a = PlantFieldScatter.Hash(ix, iy, seed, salt), b = PlantFieldScatter.Hash(ix + 1, iy, seed, salt);
            double c = PlantFieldScatter.Hash(ix, iy + 1, seed, salt), d = PlantFieldScatter.Hash(ix + 1, iy + 1, seed, salt);
            double low = a + (b - a) * tx, high = c + (d - c) * tx;
            return low + (high - low) * ty;
        }
    }
}
