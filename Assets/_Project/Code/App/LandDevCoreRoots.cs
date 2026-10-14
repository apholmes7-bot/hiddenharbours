using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>After DevRegionBootstrap chooses this inactive core, release its persistent roots
    /// before their Awake runs. A travelled-in core is destroyed inactive and never reaches here.</summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class LandDevCoreRoots : MonoBehaviour
    {
        void Awake()
        {
            while (transform.childCount > 0) transform.GetChild(0).SetParent(null, true);
        }
    }
}
