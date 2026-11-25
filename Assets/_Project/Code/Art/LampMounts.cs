using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Where a lamp is mounted</b> (ADR 0016, "lamps light what they can see"; CLAUDE.md rule 6). A lamp
    /// fixed to a WALL lights what is in front of that wall and not the wall above it, its eave, a canopy
    /// over the door or the roof: in this view all of those lie behind the wall's line on the ground, so the
    /// ground pool is cut there (<see cref="LightMath.PoolWallGate"/>). A lamp hung UNDER a canopy is also
    /// hidden by the canopy: its bloom sorts as if it stood <see cref="Mount.SortBackMetres"/> further back,
    /// behind the building's front, so the canopy draws over it and its light shows on the step below.
    ///
    /// <para>One asset, <c>Resources/LampMounts</c>, read by <see cref="PreconfiguredLight"/> when it stamps
    /// its preset. Every window spill is a wall lamp (<see cref="WindowSpill"/>); any other lamp is matched
    /// by the start of its placement id, first match wins. A lamp that matches nothing is free-standing,
    /// exactly as before. No asset on disk = every lamp free-standing.</para>
    ///
    /// <para>Determinism (rule 5): authored constants; nothing here is saved or randomised. Cost: one
    /// prefix scan when a light is stamped, never per frame.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Lighting/Lamp Mounts", fileName = "LampMounts")]
    public sealed class LampMounts : ScriptableObject
    {
        /// <summary>Where the asset is loaded from.</summary>
        public const string ResourcePath = "LampMounts";

        /// <summary>One way a lamp is fixed.</summary>
        [Serializable]
        public struct Mount
        {
            [Tooltip("Placement ids starting with this are mounted this way (ignored for the window spill).")]
            public string IdPrefix;

            [Tooltip("The way the wall faces, on the ground (0,-1 = toward the camera). Zero = no wall.")]
            public Vector2 Facing;

            [Tooltip("How far behind the lamp's foot the wall line stands, metres. Ground nearer than this to " +
                     "the wall is still lit; behind the line nothing is.")]
            [Min(0f)] public float WallBackMetres;

            [Tooltip("How far back the bloom sorts, metres: a lamp under a canopy sorts behind the building's " +
                     "front so the canopy covers it. 0 = sorts at its own foot.")]
            [Min(0f)] public float SortBackMetres;

            /// <summary>Is this a wall at all?</summary>
            public bool HasWall => Facing.sqrMagnitude > 1e-6f;
        }

        [Tooltip("Every window spill (the Window Glow preset) is light out of a window in a wall.")]
        [SerializeField] private Mount _windowSpill = new Mount { Facing = new Vector2(0f, -1f) };

        [Tooltip("Other lamps by placement id prefix, first match wins.")]
        [SerializeField] private Mount[] _byId = Array.Empty<Mount>();

        [Tooltip("The soft edge of the wall line, metres either side.")]
        [Min(0.01f)] [SerializeField] private float _wallSoftMetres = 0.35f;

        private static LampMounts s_Current;

        /// <summary>The window spill's mount.</summary>
        public Mount WindowSpill => _windowSpill;
        /// <summary>The soft edge of every wall line, metres.</summary>
        public float WallSoftMetres => _wallSoftMetres;

        /// <summary>The mounts in force: the shipped asset, else none (every lamp free-standing).
        /// Settable so a test can substitute its own.</summary>
        public static LampMounts Current
        {
            get
            {
                if (s_Current == null) s_Current = Resources.Load<LampMounts>(ResourcePath);
                if (s_Current == null) { s_Current = CreateInstance<LampMounts>(); s_Current.name = "LampMounts (none)"; s_Current._windowSpill = default; }
                return s_Current;
            }
            set => s_Current = value;
        }

        /// <summary>How the lamp at <paramref name="placementId"/> of kind <paramref name="kind"/> is mounted;
        /// default (free-standing) when nothing matches.</summary>
        public Mount For(string placementId, LightPresets.Kind kind)
        {
            if (kind == LightPresets.Kind.WindowGlow) return _windowSpill;
            if (!string.IsNullOrEmpty(placementId) && _byId != null)
                for (int i = 0; i < _byId.Length; i++)
                {
                    string p = _byId[i].IdPrefix;
                    if (!string.IsNullOrEmpty(p) && placementId.StartsWith(p, StringComparison.Ordinal)) return _byId[i];
                }
            return default;
        }

        /// <summary>Stamp the mount onto a light (free-standing clears it).</summary>
        public void ApplyTo(SceneLight light, string placementId, LightPresets.Kind kind)
        {
            if (light == null) return;
            Mount m = For(placementId, kind);
            light.SetWallMount(m.HasWall ? m.Facing.normalized : Vector2.zero, m.WallBackMetres, _wallSoftMetres,
                               m.SortBackMetres);
        }
    }
}
