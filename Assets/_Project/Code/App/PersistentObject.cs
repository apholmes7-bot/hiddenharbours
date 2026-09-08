using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Marks a root GameObject as part of the PERSISTENT CORE (the VS-01 persistent-core / additive-region
    /// model): it survives region scene loads via <see cref="Object.DontDestroyOnLoad"/>. The greybox
    /// builder tags the GameRoot (clock/env/wallet), the player, the boat (+ hold), the camera, the
    /// control switcher, the region loader and the travel coordinator with this, so a Cove↔Nine Mile Creek hop
    /// keeps the player, boat, hold and wallet (and their cross-references, which are by instance and so
    /// outlive the scene the objects were authored in).
    ///
    /// PRAGMATIC SLICE — flagged for lead-architect. Region scenes load additively on top of this core;
    /// the longer-term home for the core is a dedicated Bootstrap scene (coordination.md: Bootstrap.unity
    /// is lead-architect's). DontDestroyOnLoad only promotes ROOT objects, so each persistent root carries
    /// its own marker rather than being parented under one core (which would disturb the camera rig).
    /// </summary>
    public sealed class PersistentObject : MonoBehaviour
    {
        /// <summary>
        /// ⭐ <b>The scene this object was AUTHORED in</b>, captured the instant before it is promoted
        /// out of it — after which <c>gameObject.scene</c> reads "DontDestroyOnLoad" forever and the
        /// question can no longer be asked (2026-09-08).
        ///
        /// <para>Empty on an object that never went through <c>Awake</c> in play (an EditMode rig), and
        /// that emptiness is load-bearing: it is how a consumer tells a boat the region SERIALIZED from
        /// one a fixture merely spawned.</para>
        /// </summary>
        public string AuthoredInScene { get; private set; }

        /// <summary>Where that scene serialized it — the pose, before anything in the running game has
        /// had a chance to move it.</summary>
        public Vector3 AuthoredPosition { get; private set; }

        /// <summary>…and the heading it was laid on. A berth is a position AND a heading.</summary>
        public Quaternion AuthoredRotation { get; private set; } = Quaternion.identity;

        /// <summary>True once <see cref="Awake"/> (or the test seam) has recorded an authored pose.
        /// False means "nobody authored this object into a scene", which is not the same as
        /// "authored at the origin" — and confusing the two is what #798's first draft got wrong.</summary>
        public bool HasAuthoredPose => !string.IsNullOrEmpty(AuthoredInScene);

        private void Awake()
        {
            // ⚠ READ BEFORE THE PROMOTION, never after: DontDestroyOnLoad moves the object into its own
            // scene, so this is the only moment the authored scene and pose are still knowable.
            AuthoredInScene = gameObject.scene.name;
            AuthoredPosition = transform.position;
            AuthoredRotation = transform.rotation;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>State the authored pose directly (tests / builder) — the seam that lets the berth
        /// rule be proved in EditMode, where <c>Awake</c> does not run on an <c>AddComponent</c>.</summary>
        public void ConfigureAuthoredPose(string sceneName, Vector3 position, Quaternion rotation)
        {
            AuthoredInScene = sceneName;
            AuthoredPosition = position;
            AuthoredRotation = rotation;
        }
    }
}
