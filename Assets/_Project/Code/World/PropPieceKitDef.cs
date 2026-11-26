using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A kit of yard pieces as a key scene names them: each of the kit's piece keys (a key scene row's
    /// <c>Piece</c>) to the <see cref="PropPieceDef"/>s that draw it, and how a run of it is dressed (its posts,
    /// its gate, its bends, an odd worn bay). A row of the kit may be dressed its own way (<see cref="Dressings"/>),
    /// by the row's id. One kit, one asset, one stable id.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Prop Piece Kit", fileName = "PropPieceKit_")]
    public sealed class PropPieceKitDef : ScriptableObject
    {
        [Tooltip("Stable id, proppiecekit.snake_case. Append-only.")]
        public string Id = "proppiecekit.example";

        [Tooltip("The kit name key scene rows use for it.")]
        public string Kit = "";

        public PropPieceKitEntry[] Pieces = Array.Empty<PropPieceKitEntry>();

        public PropPieceDressing[] Dressings = Array.Empty<PropPieceDressing>();

        public PropPieceKitEntry EntryFor(string piece)
        {
            foreach (PropPieceKitEntry e in Pieces ?? Array.Empty<PropPieceKitEntry>())
                if (e != null && e.Piece == piece) return e;
            return null;
        }

        public PropPieceDressing DressingFor(string pieceId)
        {
            foreach (PropPieceDressing d in Dressings ?? Array.Empty<PropPieceDressing>())
                if (d != null && d.PieceId == pieceId) return d;
            return null;
        }
    }

    /// <summary>One piece key of a kit and what draws it.</summary>
    [Serializable]
    public sealed class PropPieceKitEntry
    {
        [Tooltip("The key scene row's Piece.")]
        public string Piece = "";

        [Tooltip("The piece itself: a single picture, or the run's bay.")]
        public PropPieceDef Main;

        [Tooltip("A worn bay that takes the place of every Nth bay of a run (none when empty).")]
        public PropPieceDef Alternate;

        [Tooltip("Which bays the alternate takes: every Nth, counted along the whole run. 0 = never.")]
        [Min(0)] public int AlternateEvery;

        [Tooltip("Stood at a run's vertex where it turns by more than CornerTurnDegrees.")]
        public PropPieceDef Corner;

        [Tooltip("Stood at each end of a run (and either side of a gate).")]
        public PropPieceDef End;

        [Tooltip("Hung, open, in a gap between two runs no wider than GateGapMaxMetres.")]
        public PropPieceDef Gate;

        [Tooltip("Laid where a run turns (a floor run's corner piece).")]
        public PropPieceDef Bend;

        [Tooltip("A run turning by more than this at a vertex gets a corner (or a bend), degrees on the ground.")]
        [Min(0f)] public float CornerTurnDegrees = 20f;

        [Tooltip("A gap between two runs this wide or narrower (plan metres) is a gateway.")]
        [Min(0f)] public float GateGapMaxMetres = 1.6f;

        [Tooltip("How far an open gate stands swung from its run, degrees on the ground, into the side the runs enclose.")]
        public float GateSwingDegrees = 70f;
    }

    /// <summary>A key scene row of the kit dressed its own way.</summary>
    [Serializable]
    public sealed class PropPieceDressing
    {
        [Tooltip("The key scene row's id.")]
        public string PieceId = "";

        public PropPieceDef Main;
        public PropPieceDef Alternate;
        [Min(0)] public int AlternateEvery;
    }
}
