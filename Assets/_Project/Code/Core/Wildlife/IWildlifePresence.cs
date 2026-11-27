using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// What a wild animal near the view is doing, as far as anything that LOOKS at it is concerned (a
    /// camera, later a listener). Ordered from "easy to photograph" to "hardly there".
    /// </summary>
    public enum WildlifePose : byte
    {
        /// <summary>Out on its spot and at ease: lying, scratching, yawning.</summary>
        Resting = 0,

        /// <summary>Out on its spot, head up, watching something come closer.</summary>
        Alert = 1,

        /// <summary>Going from its spot into the water, or back up out of it.</summary>
        Sliding = 2,

        /// <summary>In the water with its head up: a bobbing head that looks about.</summary>
        Swimming = 3,

        /// <summary>Going under, or under: only the ring it left shows.</summary>
        Diving = 4,
    }

    /// <summary>
    /// One animal a camera could point at: which kind it is, where it is, what it is doing. A plain
    /// value; copying it allocates nothing (the subject id is a string the producer already holds).
    /// </summary>
    public readonly struct WildlifeSighting
    {
        /// <summary>The kind of animal, as its species def id (<c>wildlife.grey_seal</c>): the
        /// SUBJECT a photo is of. Stable once merged.</summary>
        public readonly string SubjectId;

        /// <summary>Where its picture stands, world metres (the frame every key-scene piece uses).</summary>
        public readonly Vector2 WorldPosition;

        /// <summary>What it is doing.</summary>
        public readonly WildlifePose Pose;

        /// <summary>Stable for this animal (its place and slot) while it stays near, so a viewfinder can
        /// follow the same seal from one read to the next.</summary>
        public readonly int Key;

        /// <summary>True while it is out of the water (resting, alert, or sliding on the rock).</summary>
        public bool OnLand => Pose <= WildlifePose.Sliding;

        /// <summary>True when there is anything of it to see: everything but a seal already under.</summary>
        public bool Visible => Pose != WildlifePose.Diving;

        public WildlifeSighting(string subjectId, Vector2 worldPosition, WildlifePose pose, int key)
        {
            SubjectId = subjectId;
            WorldPosition = worldPosition;
            Pose = pose;
            Key = key;
        }
    }

    /// <summary>
    /// THE WILDLIFE-PRESENCE SEAM — the photographable animals near the view right now, read-only. The
    /// seal haul-outs (Environment) write it; a camera (not built yet: see
    /// <c>docs/design/first-camera-practice-seal-picture.md</c>) reads it to ask "is a seal in frame?",
    /// and neither names the other (rule 4). Shaped like <see cref="IBirdPresence"/> on purpose.
    ///
    /// <para><b>Reads, never state.</b> Where the animals are is a function of
    /// <c>(worldSeed, gameTime)</c> and the tide, plus how the player has just approached; nothing here
    /// enters the save (rule 5).</para>
    ///
    /// <para><b>No allocation on the read path</b> (rule 7): <see cref="CopySightings"/> fills a
    /// caller-owned array and returns how many it wrote.</para>
    /// FLAG lead-architect: new Core contract (the wildlife-presence seam, the camera's subject list).
    /// </summary>
    public interface IWildlifePresence
    {
        /// <summary>How many animals this producer is drawing right now.</summary>
        int Count { get; }

        /// <summary>Copies the drawn animals into <paramref name="into"/>, up to its length, and returns
        /// how many were written. A null array writes nothing and returns 0.</summary>
        int CopySightings(WildlifeSighting[] into);
    }

    /// <summary>
    /// The registry and the one question a camera asks of it. Several producers may be live at once (one
    /// per haul-out host in a loaded region scene), so this is a list, not a slot.
    /// </summary>
    public static class WildlifePresences
    {
        /// <summary>The most producers followed at once; any more are refused (and that is a content
        /// bug the haul-out content test catches long before it gets here).</summary>
        public const int MaxProducers = 16;

        private static readonly IWildlifePresence[] Producers = new IWildlifePresence[MaxProducers];
        private static int _count;

        /// <summary>How many producers are registered.</summary>
        public static int ProducerCount => _count;

        /// <summary>Adds a producer. Idempotent. False when the registry is full.</summary>
        public static bool Register(IWildlifePresence producer)
        {
            if (producer == null) return false;
            for (int i = 0; i < _count; i++) if (ReferenceEquals(Producers[i], producer)) return true;
            if (_count >= MaxProducers) return false;
            Producers[_count++] = producer;
            return true;
        }

        /// <summary>Removes a producer. Idempotent.</summary>
        public static void Unregister(IWildlifePresence producer)
        {
            for (int i = 0; i < _count; i++)
            {
                if (!ReferenceEquals(Producers[i], producer)) continue;
                Producers[i] = Producers[--_count];
                Producers[_count] = null;
                return;
            }
        }

        /// <summary>Forgets every producer (the play-session reset).</summary>
        public static void Reset()
        {
            for (int i = 0; i < _count; i++) Producers[i] = null;
            _count = 0;
        }

        /// <summary>
        /// How many visible animals of <paramref name="subjectId"/> stand inside <paramref name="frame"/>
        /// (world metres), counting only those out of the water when <paramref name="onLandOnly"/>.
        /// <paramref name="scratch"/> is the caller's reusable buffer; its length caps how many are
        /// looked at per producer. Pure over what the producers report; allocates nothing.
        /// </summary>
        public static int CountInFrame(Rect frame, string subjectId, bool onLandOnly, WildlifeSighting[] scratch)
        {
            if (scratch == null || string.IsNullOrEmpty(subjectId)) return 0;
            int found = 0;
            for (int p = 0; p < _count; p++)
            {
                IWildlifePresence producer = Producers[p];
                if (producer == null || (producer is Object unity && unity == null)) continue;
                int n = producer.CopySightings(scratch);
                found += CountInFrame(scratch, n, frame, subjectId, onLandOnly);
            }
            return found;
        }

        /// <summary>The same count over a list the caller already holds (what a test drives).</summary>
        public static int CountInFrame(WildlifeSighting[] sightings, int count, Rect frame, string subjectId,
                                       bool onLandOnly)
        {
            if (sightings == null) return 0;
            int found = 0;
            int n = Mathf.Min(count, sightings.Length);
            for (int i = 0; i < n; i++)
            {
                WildlifeSighting s = sightings[i];
                if (!s.Visible || (onLandOnly && !s.OnLand)) continue;
                if (!string.Equals(s.SubjectId, subjectId, System.StringComparison.Ordinal)) continue;
                if (frame.Contains(s.WorldPosition)) found++;
            }
            return found;
        }
    }
}
