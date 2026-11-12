using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Lays a <see cref="WreckDef"/> on the ground: her real hull, heeled over, still. She does not
    /// float, rock or ride the tide (the sea comes and goes round her), nobody can tie up to her,
    /// and she is solid. Everything about her comes from the Def; this only draws it.
    ///
    /// <para>She is skinned at runtime, like every mesh hull: the hull picture is made by a service
    /// that only exists in play. Where she lies is right either way, because the builder put this
    /// object there.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeachedWreck : MonoBehaviour, IVesselWay
    {
        [Tooltip("The wreck this object is. Position, heading, heel and hull all come from it.")]
        [SerializeField] private WreckDef _wreck;

        private BoatHullSkinner.Rig _rig;

        public WreckDef Wreck => _wreck;

        /// <summary>She has no power and nobody aboard, so her lamps read this and stay dark.</summary>
        public VesselWay Way => VesselWay.Derelict;

        /// <summary>True once her hull is drawn.</summary>
        public bool IsDrawn => _rig.Skinned;

        /// <summary>The heel her picture is drawn at, degrees (0 until she is drawn).</summary>
        public float DrawnHeelDegrees => _rig.Skinned && _rig.Presenter != null ? _rig.Presenter.VisualTiltDegrees : 0f;

        public void Configure(WreckDef wreck) => _wreck = wreck;

        /// <summary>The solid box she makes, in her own frame: across her beam, along her length.</summary>
        public static Vector2 SolidSize(WreckDef wreck) => wreck == null || wreck.Hull == null
            ? Vector2.zero
            : new Vector2(HullPresence.HalfBeamOf(wreck.Hull, wreck.Hull.Visual) * 2f, wreck.Hull.LengthMeters) * wreck.SolidFraction;

        private void OnEnable() => Present();

        private void Present()
        {
            if (_wreck == null || !_wreck.IsPresentable)
            {
                Debug.LogWarning($"[BeachedWreck] '{name}' has no wreck Def with a hull that has a picture, so nothing is drawn.");
                return;
            }

            transform.rotation = Quaternion.Euler(0f, 0f, -_wreck.HeadingDegrees);
            _rig = BoatHullSkinner.Apply(gameObject, _wreck.Hull.Visual, boat: null,
                                         new BoatHullSkinner.Options
                                         {
                                             SkipOars = true,
                                             SkipWaveMotion = true,
                                             PaintScheme = _wreck.Paint,
                                         });
            if (!_rig.Skinned)
            {
                Debug.LogWarning($"[BeachedWreck] '{_wreck.Id}': her hull could not be drawn (no baked hull mesh in this scene).");
                return;
            }

            // The skinner resets the tilt when it configures her, so the heel goes on after.
            if (_rig.Presenter != null) _rig.Presenter.VisualTiltDegrees = _wreck.HeelDegrees;

            // She stands on the ground among the things people walk round, so she sorts by where she
            // lies, not at a floating hull's fixed order (which is under everything ashore).
            var picture = _rig.Visual != null ? _rig.Visual.GetComponent<IHullMeshRenderer>() : null;
            if (picture != null)
                picture.SetSorting(SortingLayer.NameToID("Default"),
                    SortingBands.DecorBase - Mathf.RoundToInt(transform.position.y * SortingBands.OrdersPerMetre));

            // Nobody makes a line fast to a wreck.
            var cleats = GetComponent<BoatCleats>();
            if (cleats != null) Destroy(cleats);
        }
    }
}
