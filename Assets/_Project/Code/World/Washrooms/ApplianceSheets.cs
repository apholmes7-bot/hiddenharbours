using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>The bathroom and kitchen fixtures that carry baked appliance art.</summary>
    public enum ApplianceKind
    {
        Bath = 0,
        Toilet = 1,
        Basin = 2,
        Shower = 3,
        WharfShower = 4,
        CookStove = 5,
        PrepCounter = 6,
        Microwave = 7,
    }

    /// <summary>
    /// One appliance's sheet: which rig piece it is, how its rows (states) are laid out, and where a figure
    /// using it sits. The sheet is <c>rows × facings</c> sprites named <c>&lt;Stem&gt;_s&lt;row&gt;_d&lt;cell&gt;</c>;
    /// the baker (<c>ApplianceSheetBaker</c>) renders exactly these rows and the runtime picks from them by
    /// the named indices below (-1 = the piece has no such state).
    ///
    /// <para>The seat is in the rig's PROP frame (metres; +Y is the back, the side against the wall) and must
    /// equal <c>ApplianceIso.PROPS[RigName].seat</c>: the rig splits its near layer on the plane through it,
    /// and an EditMode test holds the two equal.</para>
    /// </summary>
    public readonly struct ApplianceLayout
    {
        public readonly ApplianceKind Kind;
        public readonly string RigName;
        public readonly string Stem;
        public readonly int Rows;
        /// <summary>The piece's ground footprint in metres: x across its face, y its depth out from the wall. Equals the rig's <c>foot</c>.</summary>
        public readonly Vector2 Foot;

        public readonly int Idle;
        public readonly int FillLow, FillMid, Full;
        public readonly int PourLow, PourMid;
        public readonly int FrothFirst, NearFull, NearFrothFirst;
        public readonly int RunFirst, NearRunFirst;
        public readonly int FlushFirst;

        public readonly FixturePose Pose;
        public readonly Vector3 Seat;
        /// <summary>Which way he faces while using it, as a PROP-frame ground direction (the bath: along the tub toward the taps; the rest: out from the wall).</summary>
        public readonly Vector2 SeatFacing;

        /// <summary>The frames every looping state (suds, a running shower, a flush) cycles through.</summary>
        public const int LoopFrames = 3;

        public ApplianceLayout(ApplianceKind kind, string rigName, int rows, Vector2 foot,
                               int fillLow = -1, int fillMid = -1, int full = -1, int pourLow = -1, int pourMid = -1,
                               int frothFirst = -1, int nearFull = -1, int nearFrothFirst = -1,
                               int runFirst = -1, int nearRunFirst = -1, int flushFirst = -1,
                               FixturePose pose = FixturePose.None, Vector3 seat = default, Vector2 seatFacing = default)
        {
            Kind = kind; RigName = rigName; Stem = ApplianceSheets.StemPrefix + rigName; Rows = rows; Foot = foot;
            Idle = 0;
            FillLow = fillLow; FillMid = fillMid; Full = full; PourLow = pourLow; PourMid = pourMid;
            FrothFirst = frothFirst; NearFull = nearFull; NearFrothFirst = nearFrothFirst;
            RunFirst = runFirst; NearRunFirst = nearRunFirst; FlushFirst = flushFirst;
            Pose = pose; Seat = seat; SeatFacing = seatFacing == default ? new Vector2(0f, -1f) : seatFacing;
        }

        public bool Fills => Full >= 0;
        public bool Froths => FrothFirst >= 0;
        public bool Runs => RunFirst >= 0;
        public bool Flushes => FlushFirst >= 0;
        public bool HasNear => NearFull >= 0 || NearRunFirst >= 0;
        public bool HasSeat => Pose != FixturePose.None || Runs;

        /// <summary>The rows a fill plays through, empty to full (the tap running on all but the last).</summary>
        public int[] FillFrames()
        {
            if (!Fills) return new int[0];
            if (PourLow >= 0) return new[] { PourLow, PourMid, Full };
            return new[] { PourMid, Full };
        }

        /// <summary>The rows a drain plays through, full to empty (no tap).</summary>
        public int[] DrainFrames()
        {
            if (!Fills) return new int[0];
            if (FillLow >= 0) return new[] { FillMid, FillLow, Idle };
            return new[] { FillMid, Idle };
        }

        /// <summary>The sprite name of one row at one facing cell.</summary>
        public string SpriteName(int row, int cell) => $"{Stem}_s{row}_d{cell}";
    }

    /// <summary>The one table of appliance sheets, shared by the baker and the runtime.</summary>
    public static class ApplianceSheets
    {
        /// <summary>Every sprite name starts with this, then the rig piece's name.</summary>
        public const string StemPrefix = "Appliance_";

        /// <summary>The <c>Resources</c> folder the sheets live in (Art/Resources/Appliances).</summary>
        public const string ResourcesFolder = "Appliances";

        /// <summary>Facings per sheet: the rooms' own eight.</summary>
        public const int Facings = 8;

        public static readonly ApplianceLayout Bath = new ApplianceLayout(
            ApplianceKind.Bath, "bath", rows: 13, foot: new Vector2(1.6f, 0.78f),
            fillLow: 1, fillMid: 2, full: 3, pourLow: 4, pourMid: 5,
            frothFirst: 6, nearFull: 9, nearFrothFirst: 10,
            pose: FixturePose.Bathe, seat: new Vector3(-0.26f, 0f, 0.24f), seatFacing: new Vector2(1f, 0f));

        public static readonly ApplianceLayout Toilet = new ApplianceLayout(
            ApplianceKind.Toilet, "toilet", rows: 4, foot: new Vector2(0.46f, 0.7f), flushFirst: 1,
            pose: FixturePose.Sit, seat: new Vector3(0f, -0.14f, 0.43f), seatFacing: new Vector2(0f, -1f));

        public static readonly ApplianceLayout Basin = new ApplianceLayout(
            ApplianceKind.Basin, "basin", rows: 7, foot: new Vector2(0.58f, 0.44f), fillMid: 1, full: 2, pourMid: 3, frothFirst: 4);

        public static readonly ApplianceLayout Shower = new ApplianceLayout(
            ApplianceKind.Shower, "shower", rows: 7, foot: new Vector2(0.85f, 0.85f), runFirst: 1, nearRunFirst: 4,
            pose: FixturePose.Stand, seat: new Vector3(0f, -0.02f, 0.02f), seatFacing: new Vector2(0f, -1f));

        public static readonly ApplianceLayout WharfShower = new ApplianceLayout(
            ApplianceKind.WharfShower, "wharfShower", rows: 4, foot: new Vector2(0.96f, 0.84f), runFirst: 1,
            pose: FixturePose.Stand, seat: new Vector3(0f, -0.02f, 0.08f), seatFacing: new Vector2(0f, -1f));

        public static readonly ApplianceLayout CookStove = new ApplianceLayout(ApplianceKind.CookStove, "cookStove", rows: 1, foot: new Vector2(0.74f, 0.58f));
        public static readonly ApplianceLayout PrepCounter = new ApplianceLayout(ApplianceKind.PrepCounter, "prepCounter", rows: 1, foot: new Vector2(0.9f, 0.6f));
        public static readonly ApplianceLayout Microwave = new ApplianceLayout(ApplianceKind.Microwave, "microwave", rows: 1, foot: new Vector2(0.5f, 0.44f));

        public static readonly ApplianceLayout[] All =
            { Bath, Toilet, Basin, Shower, WharfShower, CookStove, PrepCounter, Microwave };

        public static ApplianceLayout For(ApplianceKind kind) => All[(int)kind];
    }
}
