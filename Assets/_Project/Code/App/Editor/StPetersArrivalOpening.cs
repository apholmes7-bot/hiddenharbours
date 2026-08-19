#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE OPENING, AUTHORED</b> — where the skipper meets the player, and the line he brings her in
    /// on. Places and does not draw: everything visual is
    /// <see cref="HiddenHarbours.App.ArrivalOpening"/>'s at runtime, for the same reason
    /// <c>MooredBoat</c> gives — the mesh path is chosen live, so a builder that instantiated the hull
    /// would bake the sprite fallback into the committed scene and the arrival would silently never be
    /// mesh.
    ///
    /// <para><b>⭐ THE ROUTE IS THE BUOYED FAIRWAY, not a second opinion about the way in.</b> It is read
    /// straight off <see cref="StPetersNavMarks.Entrance"/> — the landfall out in the −4 m bay, the turn,
    /// the mouth of the dredged channel, and the dock. So the first thing the player ever does in this
    /// game is watch a working skipper run the marks, in order, from seaward; and re-routing the channel
    /// or re-siting the dock re-routes the arrival with it, because there is one route and this is not a
    /// copy of it. (It is also the route the claim test already walks metre by metre — the arrival
    /// therefore cannot be sent down water the region does not promise carries her.)</para>
    ///
    /// <para><b>⚠ Everything else is DERIVED too.</b> Her berth is the region's dock zone, the heading
    /// she lies on is the channel's own axis, and where the player is put down is the region's ratified
    /// disembark point. Nothing about the geometry is typed twice, so the arrival cannot drift out of
    /// agreement with the wharf it arrives at.</para>
    /// </summary>
    public static class StPetersArrivalOpening
    {
        /// <summary>The root the opening hangs under, in the region scene.</summary>
        public const string RootName = "StPetersArrivalOpening";

        /// <summary>
        /// The skipper who runs the passage. ⚠ Deliberately NOT in <c>Data/Boats/Owners</c>: that folder
        /// is the Nine Mile Creek WHARF REGISTER, and everything in it is held to a free berth, a free
        /// lot and a unique buoy mark on that wharf (<c>BoatOwnerContentTests</c>). Armand holds no berth
        /// there — he brings people across and goes home — so putting him in the register would have
        /// meant inventing a tenancy for him to satisfy a test about somebody else's wharf.
        /// </summary>
        public const string SkipperPath =
            "Assets/_Project/Data/Boats/Skippers/StPetersArrivalSkipper.asset";

        /// <summary>
        /// The run in, seaward first — the entrance channel's own waypoints. Hoisted so the EditMode
        /// tests can hold the route against the fairway without building a scene.
        /// </summary>
        public static Vector2[] Route()
        {
            NavChannel entrance = StPetersNavMarks.Entrance;
            var route = new List<Vector2>(entrance.Waypoints ?? new Vector2[0]);

            // The fairway's inner end IS the dock zone, which is where she stops — so the route needs no
            // extra mark, and adding one would be a second statement about where the berth is.
            if (route.Count == 0)
                Debug.LogError("[StPetersArrivalOpening] the entrance channel publishes no waypoints — " +
                               "there is no route to bring her in on.");
            return route.ToArray();
        }

        /// <summary>Where she ends up lying — the region's dock zone, off the pier head.</summary>
        public static Vector2 Berth() =>
            new Vector2(StPetersBuilder.DockZonePos.x, StPetersBuilder.DockZonePos.y);

        /// <summary>
        /// The compass heading her bow lies on when she is tied up: <b>down the channel's own axis,
        /// pointing in</b>. A boat lies along the water she came up, not across it, and the channel's
        /// axis is the one thing that already knows which way that is — so a re-cut channel turns her
        /// with it instead of leaving her athwart the fairway.
        /// </summary>
        public static float BerthHeadingDegrees() =>
            ArrivalPilot.CompassOf(StPetersBuilder.ApproachTo - StPetersBuilder.ApproachFrom);

        /// <summary>Where the player is put down — the ratified disembark point, ON the planks.</summary>
        public static Vector2 StepAshore() =>
            new Vector2(StPetersBuilder.DisembarkPos.x, StPetersBuilder.DisembarkPos.y);

        /// <summary>
        /// Hang the opening on the region root. Warn-and-continue if the skipper asset is missing: a
        /// region that builds without an arrival is a region the owner can still walk around, and an
        /// opening is not worth taking a scene down for (rule 10).
        /// </summary>
        public static ArrivalOpening Place(Transform parent)
        {
            var skipper = AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(SkipperPath);
            if (skipper == null)
                Debug.LogWarning($"[StPetersArrivalOpening] no skipper Def at {SkipperPath} — the " +
                                 "opening is placed but will decline to run and the player will simply " +
                                 "start ashore. Author the asset and rebuild.");

            var go = new GameObject(RootName);
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            go.SetActive(false);                                  // configure BEFORE OnEnable subscribes

            var opening = go.AddComponent<ArrivalOpening>();
            opening.Configure(skipper, Route(), Berth(), BerthHeadingDegrees(), StepAshore());
            go.SetActive(true);

            Vector2[] route = Route();
            Debug.Log($"[StPetersArrivalOpening] the opening is placed — {route.Length} marks from " +
                      $"({route[0].x:F0}, {route[0].y:F0}) in to the dock at " +
                      $"({Berth().x:F0}, {Berth().y:F0}), lying on {BerthHeadingDegrees():F0}°, " +
                      $"putting the player down at ({StepAshore().x:F0}, {StepAshore().y:F0}).");
            return opening;
        }
    }
}
#endif
