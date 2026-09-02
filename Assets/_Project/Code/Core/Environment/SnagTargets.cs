using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One thing in the water with a line — or a hull — that drifting flotsam can foul on: a buoy
    /// (radius 0 — the line goes straight down from the float), a hull lying-to or moored (its
    /// half-beam as the radius, so the contact lands on the planking, not at her centre).
    /// Plain floats, no engine handle: Core stays engine-light, the <see cref="TrapPlaced"/> shape.
    /// </summary>
    public readonly struct SnagTarget
    {
        /// <summary>Stable id the publisher chose (unique across publishers — prefix it with the
        /// publisher's own id). A consumer keys its "still there?" check on this.</summary>
        public readonly string Id;
        /// <summary>World X (m) of the line / the hull's centre.</summary>
        public readonly float PosX;
        /// <summary>World Y (m).</summary>
        public readonly float PosY;
        /// <summary>How far (m) from the position the thing itself extends — 0 for a buoy line,
        /// the half-beam for a hull. A consumer's contact point sits on THIS rim, and its reach is
        /// measured from it.</summary>
        public readonly float RadiusMeters;

        public SnagTarget(string id, float posX, float posY, float radiusMeters)
        {
            Id = id;
            PosX = posX;
            PosY = posY;
            RadiusMeters = Mathf.Max(0f, radiusMeters);
        }

        public Vector2 Position => new Vector2(PosX, PosY);
    }

    /// <summary>
    /// The live set of things in the water that drift can foul on, published by whoever owns them
    /// and read by whoever drifts — the <see cref="MooringCleats"/> shape (a plain list, add and
    /// remove through the two calls so the two can never diverge), keyed by id because publishers
    /// MOVE things: an ambient fisher's buoy pops up and is hauled again on her schedule, a hull
    /// lies-to and gets under way, and each of those is one <see cref="Set"/> or <see cref="Remove"/>
    /// rather than a churn of registrations.
    ///
    /// <para><b>Why a Core registry and not a signal.</b> The player's trap buoys already reach the
    /// drift through <see cref="TrapPlaced"/>/<see cref="TrapRemoved"/>, and that works because a
    /// placed trap is an EVENT. The NPC fleet's gear is STATE — it is recomputed from the schedule
    /// on every session join and day flip, and a consumer that joined late would have missed every
    /// signal. A registry answers "what is in the water right now" to anyone, whenever they ask,
    /// which is the question the drift actually has.</para>
    ///
    /// <para><b>Rule 4.</b> <c>Boats</c> publishes here; <c>Art</c> reads here; neither names the
    /// other. <b>Rule 5.</b> Presentation state only — nothing here is saved or feeds the sim; a
    /// publisher re-derives its entries from its own deterministic model on activation.
    /// <b>Rule 7.</b> Allocation-free once warm: a swap-remove list plus an id index; readers walk
    /// <see cref="Active"/> by index on their own slow tick and never receive a copy.</para>
    /// </summary>
    public static class SnagTargets
    {
        private static readonly List<SnagTarget> Targets = new List<SnagTarget>(32);
        private static readonly Dictionary<string, int> IndexOf = new Dictionary<string, int>(32);

        /// <summary>How many things are in the water right now.</summary>
        public static int Count => Targets.Count;

        /// <summary>The live set, read-only, in no particular order (a remove swaps the last entry
        /// into the hole). Add and move through <see cref="Set"/>, drop through <see cref="Remove"/>.</summary>
        public static IReadOnlyList<SnagTarget> Active => Targets;

        /// <summary>Bumps on every change, so a reader can rebuild its packed view only when
        /// something actually moved rather than on every tick.</summary>
        public static int Version { get; private set; }

        /// <summary>Publish or move one thing. A blank id is refused (it could never be removed
        /// again); re-setting an id overwrites its entry in place.</summary>
        public static void Set(string id, Vector2 position, float radiusMeters)
        {
            if (string.IsNullOrEmpty(id)) return;
            var target = new SnagTarget(id, position.x, position.y, radiusMeters);
            if (IndexOf.TryGetValue(id, out int i)) Targets[i] = target;
            else
            {
                IndexOf[id] = Targets.Count;
                Targets.Add(target);
            }
            Version++;
        }

        /// <summary>Withdraw one thing (the buoy hauled, the hull under way). Unknown ids are a no-op
        /// — a teardown must never have to check first.</summary>
        public static bool Remove(string id)
        {
            if (string.IsNullOrEmpty(id) || !IndexOf.TryGetValue(id, out int i)) return false;
            int last = Targets.Count - 1;
            if (i != last)
            {
                SnagTarget moved = Targets[last];
                Targets[i] = moved;
                IndexOf[moved.Id] = i;
            }
            Targets.RemoveAt(last);
            IndexOf.Remove(id);
            Version++;
            return true;
        }

        /// <summary>Is this id in the water right now?</summary>
        public static bool TryGet(string id, out SnagTarget target)
        {
            if (!string.IsNullOrEmpty(id) && IndexOf.TryGetValue(id, out int i))
            {
                target = Targets[i];
                return true;
            }
            target = default;
            return false;
        }

        /// <summary>Empty the registry (scene teardown / test isolation).</summary>
        public static void Clear()
        {
            if (Targets.Count == 0) return;
            Targets.Clear();
            IndexOf.Clear();
            Version++;
        }
    }
}
