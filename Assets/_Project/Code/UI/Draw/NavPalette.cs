using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The chartplotter's two authored colourways — a bright paper-chart DAY face and the real
    /// plotter's dusk face with dark water (navRig.js:26-53). Straight transcription: where the rig
    /// authors a colour this carries it, and where it authors none this invents none.
    ///
    /// <para>Two palettes rather than a tint, because that is how the source is written — night is not
    /// day dimmed. The water ramp inverts its whole character (pale shallows over white deeps by day;
    /// deep blues going black at night), and the route, track and buoy colours all shift to stay
    /// legible against it.</para>
    ///
    /// <para>The few entries the rig writes as <c>rgba(...)</c> are split into a colour and an alpha,
    /// since <see cref="DrawSurface"/> composites with an explicit coverage rather than a packed
    /// alpha.</para>
    /// </summary>
    public readonly struct NavPalette
    {
        public readonly Color32 Frame, Strip, StripFg, StripDim, StripHi, Plate;
        public readonly float PlateAlpha;
        public readonly Color32 Land, LandEdge, Dry, DryDot;
        public readonly Color32[] Water;                       // [0] shoalest … [4] deepest
        public readonly Color32 Grid;
        public readonly float GridAlpha;
        public readonly Color32 Label, LabelDim;
        public readonly Color32 Route, RouteDim, Track;
        public readonly Color32 WptMark, WptAnchor, WptFuel, WptHazard, WptDest;
        public readonly Color32 BuoyPort, BuoyStbd, BuoyCard, BuoySafe, BuoySpecial;
        public readonly Color32 Rock, Traffic, TrafficSel;
        public readonly Color32 Ship, ShipEdge, Head, Current;
        public readonly Color32 RailPlate, RailEdge, RailLit, RailTxt, RailDim;
        public readonly Color32 ProfBg, ProfBed, ProfBedEdge, ProfSky, ProfTxt;
        public readonly Color32 KeyPlate, KeyEdge, Legend, Mark;

        private NavPalette(bool night)
        {
            if (!night)
            {
                Frame = H("0a1418"); Strip = H("0b1a22"); StripFg = H("bfe6df");
                StripDim = H("4c6f74"); StripHi = H("7fd6c9");
                Plate = H("061014"); PlateAlpha = 0.60f;
                Land = H("e6d3a0"); LandEdge = H("b28a4e"); Dry = H("b7cf92"); DryDot = H("8a9a5e");
                Water = new[] { H("6fb8cf"), H("98cee0"), H("bfe0ea"), H("ddeef3"), H("f1f7f8") };
                Grid = H("28465a"); GridAlpha = 0.18f;
                Label = H("33506a"); LabelDim = H("5f7f8c");
                Route = H("d23ccf"); RouteDim = H("8a2f88"); Track = H("3f86c0");
                WptMark = H("c2417a"); WptAnchor = H("2f6fae"); WptFuel = H("e0902f");
                WptHazard = H("d8443a"); WptDest = H("2f9e57");
                BuoyPort = H("d8443a"); BuoyStbd = H("2f9e57"); BuoyCard = H("e6b53f");
                BuoySafe = H("d8443a"); BuoySpecial = H("e6b53f");
                Rock = H("2a4152"); Traffic = H("2f7d4a"); TrafficSel = H("e0554a");
                Ship = H("16324a"); ShipEdge = H("f2f7f8"); Head = H("d8443a"); Current = H("2f6fae");
                RailPlate = H("123038"); RailEdge = H("0a1a20"); RailLit = H("1d5a52");
                RailTxt = H("bfe6df"); RailDim = H("4c6f74");
                ProfBg = H("0b1a22"); ProfBed = H("1c3a2a"); ProfBedEdge = H("2f9e57");
                ProfSky = H("0d2230"); ProfTxt = H("7fd6c9");
                KeyPlate = H("245a4a"); KeyEdge = H("123528"); Legend = H("dff3e6"); Mark = H("bfe6cf");
            }
            else
            {
                Frame = H("0a0d10"); Strip = H("120c04"); StripFg = H("ffd77a");
                StripDim = H("6b501c"); StripHi = H("ffbe3d");
                Plate = H("0a0602"); PlateAlpha = 0.62f;
                Land = H("2a2413"); LandEdge = H("5a4a28"); Dry = H("233018"); DryDot = H("3f5228");
                Water = new[] { H("1c4256"), H("153446"), H("102a3a"), H("0c2130"), H("081824") };
                Grid = H("7896aa"); GridAlpha = 0.10f;   // rgba(120,150,170,0.10)
                Label = H("8fb0c0"); LabelDim = H("5f7f8c");
                Route = H("ff6fe0"); RouteDim = H("a83f98"); Track = H("7fb0ff");
                WptMark = H("ff8fc0"); WptAnchor = H("6fa8e0"); WptFuel = H("ffb84f");
                WptHazard = H("ff6f5c"); WptDest = H("6fe0a0");
                BuoyPort = H("ff6f5c"); BuoyStbd = H("6fe0a0"); BuoyCard = H("ffd77a");
                BuoySafe = H("ff6f5c"); BuoySpecial = H("ffd77a");
                Rock = H("6f8ea0"); Traffic = H("6fe0a0"); TrafficSel = H("ff8f7a");
                Ship = H("cfe0ea"); ShipEdge = H("0a0d10"); Head = H("ff6f5c"); Current = H("6fa8e0");
                RailPlate = H("241a06"); RailEdge = H("120c02"); RailLit = H("5a4210");
                RailTxt = H("ffd77a"); RailDim = H("6b501c");
                ProfBg = H("0d0a04"); ProfBed = H("2a2008"); ProfBedEdge = H("ffbe3d");
                ProfSky = H("0a1420"); ProfTxt = H("ffd77a");
                KeyPlate = H("3a2c0c"); KeyEdge = H("201604"); Legend = H("ffe0a0"); Mark = H("ffd77a");
            }
        }

        private static Color32 H(string hex) => RigDrawUtil.Hex(hex);

        private static readonly NavPalette _day = new NavPalette(false);
        private static readonly NavPalette _night = new NavPalette(true);

        /// <summary>The authored colourway for the panel-light state (built once each, never per paint).</summary>
        public static NavPalette For(bool night) => night ? _night : _day;

        /// <summary>The waypoint symbol's colour for a kind (navRig.js:32 <c>wpt</c>).</summary>
        public Color32 WaypointColour(HiddenHarbours.Core.NavWaypointKind kind) => kind switch
        {
            HiddenHarbours.Core.NavWaypointKind.Anchor => WptAnchor,
            HiddenHarbours.Core.NavWaypointKind.Fuel => WptFuel,
            HiddenHarbours.Core.NavWaypointKind.Hazard => WptHazard,
            HiddenHarbours.Core.NavWaypointKind.Dest => WptDest,
            _ => WptMark,
        };
    }
}
