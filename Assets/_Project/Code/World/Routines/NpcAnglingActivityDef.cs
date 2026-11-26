using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A villager fishing from where he stands</b> (ADR 0053 §11): the rod he holds, how a session runs,
    /// how his line and fish are drawn, and where his catch comes from. Named by a routine block's
    /// <see cref="RoutineEntry.Doing"/>; read by <see cref="NpcFigureStand"/>, which answers the Core angler
    /// and figure-beat stands from it as a pure function of the clock.
    ///
    /// <para><b>The catch is not here.</b> The Def names a spot (<see cref="CatchSpotId"/>) and the Fishing
    /// module says what bites there (<see cref="IShoreCatchFacts"/>), from the same species data the player's
    /// catch reads. World never sees a species table.</para>
    ///
    /// <para><b>The look is the player's, copied as data</b>: an EditMode test holds every number in
    /// <see cref="Look"/> equal to the player's rod presenter, so the two lines read as one line.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/NPC Activity/Angling", fileName = "AnglingActivity")]
    public sealed class NpcAnglingActivityDef : NpcActivityDef
    {
        [Header("The rod")]
        [Tooltip("The rod he holds, on the right hand's tool socket (a held-prop mesh Def).")]
        public HeldPropMeshDef Rod;

        [Header("The session (every beat length; the slot must hold the longest session)")]
        public AnglerScript Script;

        [Header("How it is drawn (equal to the player's rod presenter; a test holds them together)")]
        public AnglerLook Look;

        [Header("The catch and the weather")]
        [Tooltip("Where he fishes, as a spot id the Fishing module knows (spot.snake_case, e.g. " +
                 "spot.st_peters.wharf_head). What bites there is that module's to say.")]
        public string CatchSpotId;

        [Tooltip("The roughest sea he will fish in (0 = flat calm, 1 = the worst the sea gets). Above this he " +
                 "stands at his station without casting.")]
        [Range(0f, 1f)] public float MaxSeaState01;

        /// <inheritdoc/>
        /// <remarks>An angler needs open air and water in front of him: never indoors.</remarks>
        public override bool ValidAt(bool stationIndoors, out string why)
        {
            why = stationIndoors ? $"'{Id}' is fishing, and its station is indoors" : null;
            return !stationIndoors;
        }

        /// <summary>True when this Def can be played: a rod, a valid script and a spot.</summary>
        public bool IsPlayable(out string why)
        {
            if (Rod == null) why = $"'{Id}' names no rod";
            else if (string.IsNullOrEmpty(CatchSpotId)) why = $"'{Id}' names no catch spot";
            else if (!Script.IsValid(out string bad)) why = $"'{Id}': {bad}";
            else why = null;
            return why == null;
        }
    }
}
