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

        [Tooltip("A floor run's planks drawn as a few long pictures instead of one object a plank: fewer objects in " +
                 "the scene and fewer draw calls, the same pixels. Written by the bake step; the placer refuses a " +
                 "bake that no longer matches its run.")]
        public PropPieceBakedRun[] BakedRuns = Array.Empty<PropPieceBakedRun>();

        [Tooltip("The longest a baked floor picture reaches either way, world metres.")]
        [Min(1f)] public float BakeStripMaxMetres = 12f;

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

        [Tooltip("Moves a single piece off its row's point, world metres (a yard nudge the package's next issue should carry).")]
        public Vector2 Offset;

        [Tooltip("More of the kit laid with this row and drawn as its parts: beds in a garden, a walk to its gate.")]
        public PropPieceExtra[] Extras = Array.Empty<PropPieceExtra>();
    }

    /// <summary>One baked picture of a row's floor run: the strip's picture, where its centre stands, and what it was baked from.</summary>
    [Serializable]
    public sealed class PropPieceBakedRun
    {
        [Tooltip("The key scene row's id.")]
        public string Row = "";

        [Tooltip("Which strip of the row, in the run's order.")]
        [Min(0)] public int Index;

        public Sprite Sprite;

        [Tooltip("Where the strip's centre stands, world metres.")]
        public Vector2 At;

        [Tooltip("A hash of the planks it was baked from (their pictures and places); a mismatch means bake again.")]
        public string Signature = "";
    }

    /// <summary>A piece of the kit laid with a row: one point for a single piece, a line of points for a floor run.</summary>
    [Serializable]
    public sealed class PropPieceExtra
    {
        [Tooltip("The kit's piece key (a single piece, or a floor run such as the plank walk).")]
        public string Piece = "";

        [Tooltip("World metres from the row's point: one point for a single piece, two or more for a run's line.")]
        public Vector2[] Path = Array.Empty<Vector2>();
    }
}
