using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A POND: STILL WATER IN A BASIN.</b> One asset per pond (<c>pond.snake_case</c>, e.g.
    /// <c>pond.stp_fen_pool</c>). Its water stands at <see cref="Surface"/> over a bowl down to
    /// <see cref="Bed"/>, in a basin carved <see cref="Basin"/>.y deep over <see cref="Basin"/>.x metres
    /// (terrain pass 9, part 1 §4). A pond holds water only in its basin: it never leaks.
    ///
    /// <para>A fall's plunge pool is a PondDef its <see cref="WaterfallDef"/> names; the fall lays its
    /// bowl, so the plan's pond list does not carry it.</para>
    ///
    /// <para>A key scene's pool on a ground file (<c>pool.snake_case</c>, e.g. <c>pool.stp_gap_dipping_pool</c>)
    /// is a PondDef in the plan's StillPools: the ground holds its bowl, and its water stands by flood (PR 5 B).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Pond", fileName = "Pond")]
    public class PondDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (pond.snake_case; pool.snake_case for a key scene's still pool).")]
        public string Id = "pond.example";
        public string DisplayName = "";
        [Tooltip("bog_pond, fen_pool, plunge_pool, pond or dipping_pool.")]
        public string Kind = "bog_pond";
        public Vector2 Centre;
        [Tooltip("The water's radii (m): along, across.")]
        public Vector2 Radii = new Vector2(4f, 3f);
        public float RotationDeg;
        [Tooltip("The water's level (m).")]
        public float Surface;
        [Tooltip("The bowl's deepest point (m).")]
        public float Bed;
        [Tooltip("The basin round it: reach past the rim (m), depth (m).")]
        public Vector2 Basin;
        [Tooltip("The stream it spills into, or none.")]
        public StreamDef Outlet;
        [TextArea] public string Why = "";
    }
}
