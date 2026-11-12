using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>A WRECK</b>: a hull lying ashore that nobody sails. She is scenery with a name: which hull
    /// she was, where she lies and how far over, and whether she can be put right LATER. That last
    /// part is only a promise in data. Nothing here repairs her, sells her or gives her to anyone;
    /// a later piece of the game reads <see cref="RepairableLater"/> and builds on it.
    ///
    /// <para>One wreck per file, stable id <c>wreck.&lt;region&gt;_&lt;name&gt;</c> (rule 2).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Wreck", fileName = "Wreck")]
    public sealed class WreckDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (wreck.<region>_<name>).")]
        public string Id = "wreck.example";

        [Tooltip("The region she lies in (region.snake_case).")]
        public string RegionId = "";

        [Tooltip("What people call her.")]
        public string DisplayName = "";

        [Header("What she was")]
        [Tooltip("The hull she is. Her picture, length, beam and draught all come from this.")]
        public BoatHullDef Hull;

        [Tooltip("Her paint as she lies now (rust, bare plate). Empty = the hull's own paint as shipped.")]
        public HullPaintSchemeDef Paint;

        [Header("How she lies")]
        [Tooltip("Where her middle lies, world metres.")]
        public Vector2 Position;

        [Tooltip("The compass bearing her bow points, degrees (0 = north, clockwise).")]
        public float HeadingDegrees;

        [Tooltip("How far she lies over on her side, degrees. The sign picks the side.")]
        [Range(-85f, 85f)] public float HeelDegrees = 50f;

        [Tooltip("How much of her length and beam is solid to walk or sail into (1 = all of it).")]
        [Range(0.3f, 1f)] public float SolidFraction = 0.85f;

        [Tooltip("How far outside her solid side a person stands to look at her, metres.")]
        [Min(0.1f)] public float LookStandOffMeters = 0.8f;

        [Tooltip("The id of the thing-to-read that speaks when the player looks at her (npc.snake_case).")]
        public string LookNpcId = "";

        [Header("The promise")]
        [Tooltip("True = she can be repaired LATER and put to work. Nothing reads this yet: it is the " +
                 "promise the campaign will build on, not a repair loop.")]
        public bool RepairableLater;

        [Tooltip("In plain words, what is promised and what is not built. Authoring note only.")]
        [TextArea] public string Promise = "";

        /// <summary>Is there a hull with a picture to lay on the beach?</summary>
        public bool IsPresentable => Hull != null && Hull.Visual != null;

        /// <summary>Her length as she lies, metres.</summary>
        public float LengthMeters => Hull != null ? Hull.LengthMeters : 0f;

        /// <summary>The two ends of her keel line as she lies: stern, then bow.</summary>
        public void KeelLine(out Vector2 stern, out Vector2 bow)
        {
            Vector2 along = Along;
            stern = Position - along * (LengthMeters * 0.5f);
            bow = Position + along * (LengthMeters * 0.5f);
        }

        /// <summary>The unit vector her bow points along (compass bearing to world).</summary>
        public Vector2 Along
        {
            get
            {
                float r = HeadingDegrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
            }
        }
    }
}
