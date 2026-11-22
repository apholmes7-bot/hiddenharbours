using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The mesh reveal for a parked dwelling</b> (ADR 0060 section 3): cuts the dwelling's facet mesh open
    /// on the level the occupant is standing in, and closes it when he leaves. The mesh half of
    /// <see cref="IInteriorReveal"/>; the sprite swap every existing house uses is the other half and lives
    /// in <see cref="BuildingInterior"/>, untouched.
    ///
    /// <para><b>It invents no source of truth.</b> Inside or not, and on which storey, is
    /// <see cref="BuildingInterior"/>'s answer, handed in on each real transition. Which level and lid that
    /// opens is the dwelling's own <see cref="DwellingMeshDef"/>, read off the building
    /// (<see cref="DwellingInteriorRules.CutFor"/>). The renderer is asked for through Core's
    /// <see cref="IHullCutaway"/>, so the World module never names the Art type that draws it (rule 4). It
    /// polls nothing: an empty dwelling costs nothing per frame.</para>
    ///
    /// <para><b>The occupant's sort</b> (ADR 0060 section 5). While he is inside, this dwelling tells Core's
    /// <see cref="OccupantSorting"/> its order (<see cref="DwellingSorting.OrderAt"/> at its ground pivot, the
    /// root's position); his own sort writer reads that and draws him one rung in front. He is never parented
    /// here, and this never writes his order itself.</para>
    ///
    /// <para><b>A renderer that arrives late.</b> The presentation service may install the facet renderer
    /// after the building has already revealed (a wake inside, a re-skin). The last cut asked for is kept as
    /// <see cref="RequestedCut"/>; whoever installs calls <see cref="Reassert"/>.</para>
    ///
    /// <para>No save: the cut, the reveal and the occupant's order are recomputed from where he stands
    /// (ADR 0060 section 7).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuildingInterior))]
    public sealed class DwellingCutaway : MonoBehaviour, IInteriorReveal
    {
        private BuildingInterior _interior;
        private IHullCutaway _renderer;

        /// <summary>The cut this dwelling last asked its renderer for; None = whole exterior. Read by tests,
        /// and by anyone debugging a room that will not open.</summary>
        public HullMeshDef.Cut RequestedCut { get; private set; }

        /// <summary>Whether the last reveal had the occupant inside.</summary>
        public bool OccupantInside { get; private set; }

        /// <summary>The storey the last reveal named (0 outside).</summary>
        public int Storey { get; private set; }

        /// <summary>The order this dwelling sorts at: the decor band's Y-sort at its ground pivot, one rung
        /// behind (<see cref="DwellingSorting.OrderAt"/>). A dwelling never moves, so this never changes.</summary>
        public int DwellingOrder => DwellingSorting.OrderAt(transform.position);

        /// <inheritdoc/>
        public void Reveal(bool inside, int storey)
        {
            BuildingInterior interior = Interior();
            DwellingMeshDef def = interior != null ? interior.DwellingMesh : null;

            OccupantInside = inside;
            Storey = inside ? storey : 0;
            RequestedCut = DwellingInteriorRules.CutFor(def, inside, Storey);

            IHullCutaway target = Renderer();
            if (target != null) target.ShowCutaway(RequestedCut);

            Transform occupant = inside && interior != null ? interior.Occupant : null;
            if (occupant != null) OccupantSorting.Enter(this, occupant, DwellingOrder);
            else OccupantSorting.Leave(this);
        }

        /// <summary>Say the last cut again, to a renderer found afresh. For the installer, after it puts a
        /// facet renderer on this dwelling. Safe and idempotent when nothing is installed.</summary>
        public void Reassert()
        {
            _renderer = null;
            IHullCutaway target = Renderer();
            if (target != null) target.ShowCutaway(RequestedCut);
        }

        private void OnDisable()
        {
            // A dwelling that is switched off is not drawn, so nobody can be sorting against it. The
            // building re-reveals from outside when it comes back on (BuildingInterior.OnEnable).
            OccupantSorting.Leave(this);
        }

        private BuildingInterior Interior()
        {
            if (_interior == null) _interior = GetComponent<BuildingInterior>();
            return _interior;
        }

        /// <summary>The facet renderer on this dwelling, as Core's cut seam, or null. Re-found when the one
        /// held has been destroyed (explicit Unity null check: an interface reference to a destroyed
        /// component is not null to C#).</summary>
        private IHullCutaway Renderer()
        {
            if (_renderer is Object held && held != null) return _renderer;
            _renderer = GetComponentInChildren<IHullCutaway>(includeInactive: true);
            return _renderer;
        }
    }
}
