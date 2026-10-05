using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A channel's cross-section: the bed rises <see cref="Rise"/>·(a / <see cref="Scale"/>)² out to
    /// <see cref="Edge"/> metres from the line, then its walls rise <see cref="Bank"/> per metre.
    /// </summary>
    [Serializable]
    public struct FallSection
    {
        public float Rise;
        public float Scale;
        public float Edge;
        public float Bank;

        public FallSection(float rise, float scale, float edge, float bank)
        {
            Rise = rise; Scale = scale; Edge = edge; Bank = bank;
        }
    }

    /// <summary>
    /// <b>A WATERFALL: WHERE A BROOK BREAKS AT A LIP.</b> One asset per fall (<c>fall.snake_case</c>, e.g.
    /// <c>fall.stp_alder_fall</c>). The stream keeps its id and its line; inside the fall's box its ground is
    /// the fall's own cut into the plan's bank (the bank without the stream's carve): the approach above the
    /// lip, the plunge pool's bowl and the chute down to the stream's own bed, blended into the plan's ground
    /// at the box's edges (the key scene's WaterfallDef and its ground change, R2 SCHEMA.md).
    ///
    /// <para>The water stands at <see cref="ReferenceFlow"/>: the still-water map is static. How the flow
    /// follows the rain is the owner's call and a later PR's.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Waterfall", fileName = "Waterfall")]
    public class WaterfallDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (fall.snake_case).")]
        public string Id = "fall.example";
        public string DisplayName = "";
        [Tooltip("The stream it breaks. Its designed bed gives the chute's floor.")]
        public StreamDef Stream;
        [Tooltip("The plunge pool below the lip.")]
        public PondDef Pool;

        [Header("The box it cuts")]
        public Vector2 BoxMin;
        public Vector2 BoxMax;
        [Tooltip("The cut blends into the plan's ground over these widths (m): west, east, south, north.")]
        public Vector4 BoxBlend = new Vector4(1.6f, 1.6f, 0.6f, 1.2f);

        [Header("The line and the lip")]
        [Tooltip("The run's line through the box, south to north (m).")]
        public Vector2[] Centreline = new Vector2[0];
        [Tooltip("The brink's middle (m).")]
        public Vector2 Lip;
        [Tooltip("The brink's height (m).")]
        public float LipZ = 3.85f;
        [Tooltip("The lip's rock, within this of the brink on its south side, is not cut (m).")]
        public float LipRock = 1.35f;
        [Tooltip("The drop, the brink to the pool's surface (m).")]
        public float Drop = 1.8f;

        [Header("The approach above the lip (y, m)")]
        [Tooltip("The fall's channel is cut from this y to the lip.")]
        public float ApproachFrom = 55.2f;
        [Tooltip("The glide: from this y...")]
        public float GlideFrom = 55.5f;
        [Tooltip("...to the step; its bed runs from x to y (m).")]
        public Vector2 GlideBed = new Vector2(5.01f, 4.95f);
        [Tooltip("The step at the heath's edge: the slide starts here.")]
        public float Step = 58.2f;
        [Tooltip("The slide's bed runs from x to y (m), bowed by SlideBow.")]
        public Vector2 SlideBed = new Vector2(4.72f, 3.95f);
        public float SlideBow = 0.12f;
        [Tooltip("The sill: from here to the lip, down to LipZ.")]
        public float Sill = 60.6f;
        [Tooltip("The glide's section fades into the slide's between these y.")]
        public Vector2 SectionFade = new Vector2(57.9f, 58.4f);
        public FallSection GlideSection = new FallSection(0.12f, 0.5f, 0.8f, 0.9f);
        public FallSection SlideSection = new FallSection(0.07f, 0.6f, 0.9f, 1.6f);
        [Tooltip("The boss that splits the sill: height (m), spread (m², n² over it), and the y it rises over.")]
        public float BossHeight = 0.12f;
        public float BossSpread = 0.04f;
        public Vector2 BossRise = new Vector2(60.72f, 60.92f);
        [Tooltip("The channel is cut this far either side of the line (m).")]
        public float CarveHalfWidth = 4f;

        [Header("The plunge pool's bowl")]
        [Tooltip("Where the fall lands (m).")]
        public Vector2 Foot;
        [Tooltip("The bowl is deepest here, off the foot (m).")]
        public Vector2 FootOffset = new Vector2(0f, 0.5f);
        [Tooltip("The deepening round the foot: its radii (m)...")]
        public Vector2 FootRadii = new Vector2(1.7f, 1.5f);
        [Tooltip("...and its share of the depth.")]
        public float FootShare = 0.25f;
        [Tooltip("The bowl's floor: surface - depth·(1 - q²)^this.")]
        public float BowlPower = 0.7f;
        [Tooltip("Outside the pool the walls start this far above its surface (m)...")]
        public float RimLift = 0.07f;
        [Tooltip("...and rise this much per unit of q (m).")]
        public float RimSlope = 2.43f;

        [Header("The chute to the stream's bed")]
        [Tooltip("The chute is cut from this y north.")]
        public float ChuteFrom = 63.6f;
        [Tooltip("Its water runs to this y; the stream's own water carries on beyond.")]
        public float ChuteTo = 68.8f;
        [Tooltip("Its bed is min(this, the stream's designed bed - ChuteBelowPlan) (m).")]
        public float ChuteCap = 2f;
        public float ChuteBelowPlan = 0.02f;
        public FallSection ChuteSection = new FallSection(0.1f, 0.55f, 0.8f, 0.85f);
        [Tooltip("Where the pool spills into the chute (m).")]
        public Vector2 Spill;
        [Tooltip("Where the chute meets the stream (m), and its height there.")]
        public Vector2 OutflowTo;
        public float OutflowToZ;

        [Header("The water (m over the bed, at a flow f: x + y·f)")]
        [Range(0f, 1f)] public float ReferenceFlow = 0.4f;
        public Vector2 GlideWater = new Vector2(0.05f, 0.12f);
        public Vector2 SlideWater = new Vector2(0.035f, 0.09f);
        public Vector2 SillWater = new Vector2(0.04f, 0.16f);
        [Tooltip("The pool stands this far over its surface.")]
        public Vector2 PoolWater = new Vector2(0f, 0.03f);
        public Vector2 ChuteWater = new Vector2(0.04f, 0.1f);
        [Tooltip("The water runs this far either side of the line (m).")]
        public float WaterHalfWidth = 1.3f;
        [Tooltip("Thinner water than this is none (m).")]
        public float MinWater = 0.004f;
        [Tooltip("On a ground file, the chute's water runs this far past where its level meets the chute's designed dish (m).")]
        public float ChuteWaterPast = 0.12f;

        [Header("Paint (inside PaintMin..PaintMax; elsewhere the plan's)")]
        public Vector2 PaintMin = new Vector2(99.5f, 55.2f);
        public Vector2 PaintMax = new Vector2(113f, 69f);
        [Tooltip("Under the glide's and the chute's water.")]
        public string GlideZone = "shingle";
        [Tooltip("Under the slide's and the sill's water, and the lip's rock.")]
        public string RockZone = "ledge";
        [Tooltip("The pool's floor inside PoolMudQ of its radius...")]
        public string PoolZone = "mud";
        public float PoolMudQ = 0.72f;
        [Tooltip("...and beyond it, and the pool's rim below RimShingleBelow.")]
        public string PoolRimZone = "shingle";
        [Tooltip("The lip's rock: from this y to the brink (+ LipRockTop), within LipRockHalfWidth of the line (m).")]
        public float LipRockFrom = 57.9f;
        public float LipRockTop = 0.05f;
        public float LipRockHalfWidth = 1.7f;
        [Tooltip("The rock round the foot: within this of the foot (m, y squashed by FootRockSquash), above FootRockAbove, south of FootRockBelowY.")]
        public float FootRockRadius = 2.4f;
        public float FootRockSquash = 1.1f;
        public float FootRockAbove = 1.95f;
        public float FootRockBelowY = 62.6f;
        [Tooltip("Steeper than this (m per m) the foot's rock is ledge; gentler, talus.")]
        public float FootRockSlope = 0.5f;
        public string TalusZone = "talus";
        [Tooltip("The pool's rim: q from x to y.")]
        public Vector2 RimBand = new Vector2(1f, 1.75f);
        public float RimShingleBelow = 2.45f;
        [Tooltip("Steeper than this (m per m) the rim is talus.")]
        public float RimTalusSlope = 0.85f;
        [Tooltip("The chute's bar: between these y, within ChuteBarHalfWidth of the line, below its bed + ChuteBarAbove.")]
        public Vector2 ChuteBarY = new Vector2(64.3f, 68.6f);
        public float ChuteBarHalfWidth = 1.05f;
        public float ChuteBarAbove = 0.5f;

        [Header("Source")]
        public string SourceId = "";
        [TextArea] public string Why = "";
    }
}
