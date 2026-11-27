using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One yard piece: a picture (or one picture per run direction) and how it stands, collides and tiles. A
    /// single piece (the fog bell, a clothesline, a fence post) is one picture stood on its ground point; a run
    /// piece (a picket panel, a gate leaf, a plank) is laid along a line by <see cref="PropRunLayout"/>, picking
    /// the picture drawn nearest the run's direction on screen. One piece, one asset, one stable id.
    ///
    /// <para>Every picture's ground point is its centre (the recipe pads it so), so the importer's default
    /// pivot stands it where it is placed.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Prop Piece", fileName = "PropPiece_")]
    public sealed class PropPieceDef : ScriptableObject
    {
        public const string RaisedLayer = "raised", FloorLayer = "floor";

        [Tooltip("Stable id, proppiece.snake_case. Append-only.")]
        public string Id = "proppiece.example";

        [Tooltip("What a player would call it.")]
        public string DisplayName = "";

        [Tooltip("'raised' stands and sorts by its ground line; 'floor' lies under every figure and is walked on.")]
        public string Layer = RaisedLayer;

        [Tooltip("The picture, or one per run direction: index i is drawn for a run rising i x DirectionStepDegrees " +
                 "on screen (0 = east, 90 = north). A falling run uses the rising picture flipped in x.")]
        public Sprite[] Sprites = Array.Empty<Sprite>();

        [Tooltip("Degrees on screen between the run directions the pictures were drawn for. 0 for a single piece.")]
        [Min(0f)] public float DirectionStepDegrees;

        [Tooltip("A run piece's length along its run, plan metres (the ground, not the screen).")]
        [Min(0f)] public float PlanLengthMetres;

        [Tooltip("How tall it stands, metres (for the record; the picture already draws it).")]
        [Min(0f)] public float HeightMetres;

        [Tooltip("A run piece's walk-blocking band along its run, world metres across; 0 lets a walker through.")]
        [Min(0f)] public float RunColliderThicknessMetres;

        [Tooltip("A single piece's walk-blocking shapes, world metres from its ground point. None lets a walker through.")]
        public PropPieceShape[] Colliders = Array.Empty<PropPieceShape>();

        [Tooltip("A single raised piece casts the sun's shadow from its ground point, as the other standing props do.")]
        public bool CastsShadow;

        public bool IsRun => Sprites != null && Sprites.Length > 1;
        public bool IsFloor => Layer == FloorLayer;

        /// <summary>The picture for a run along a world-space (screen) direction, and whether it is flipped in x.</summary>
        public Sprite SpriteFor(Vector2 runDirection, out bool flipX)
        {
            int i = PropRunLayout.DirectionIndex(runDirection, DirectionStepDegrees, Sprites.Length, out flipX);
            return Sprites[i];
        }
    }

    /// <summary>A walk-blocking polygon, world metres from the piece's ground point.</summary>
    [Serializable]
    public sealed class PropPieceShape
    {
        public Vector2[] Points = Array.Empty<Vector2>();
    }
}
