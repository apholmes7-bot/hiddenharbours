using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Marks a root GameObject as part of the PERSISTENT CORE (the VS-01 persistent-core / additive-region
    /// model): it survives region scene loads via <see cref="Object.DontDestroyOnLoad"/>. The greybox
    /// builder tags the GameRoot (clock/env/wallet), the player, the boat (+ hold), the camera, the
    /// control switcher, the region loader and the travel coordinator with this, so a Cove↔Greywick hop
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
        private void Awake() => DontDestroyOnLoad(gameObject);
    }
}
