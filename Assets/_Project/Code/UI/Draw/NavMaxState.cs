using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>Which tool the MAX face's rail has selected (navRig.js:258 the <c>tool:i&gt;=8</c> half of
    /// the rail, and the README's <c>tool</c> param: <c>pan|mark|route|measure</c>). Values are transient
    /// — a tool is where your hand is, never a saved preference.</summary>
    public enum NavChartTool
    {
        /// <summary>PAN — the neutral tool. A chart tap SELECTS the waypoint under it (feeding the
        /// manager's <c>selWpt</c>) and mutates nothing, so "look at the chart" is never one slip away
        /// from editing it.</summary>
        Pan = 0,

        /// <summary>MRK — a chart tap drops a waypoint AT THE TAP. This is the tap-to-place the console
        /// face deliberately deferred: it has no rail, so it had no selected tool for a tap to mean
        /// anything against (<c>ChartplotterOverlayHost.MarkHere</c>'s remarks).</summary>
        Mark = 1,

        /// <summary>RTE — a chart tap appends a point to the planned route, which is exactly what the
        /// rig's own empty-route legend instructs (navRig.js:415 "SELECT ROUTE TOOL &amp; TAP THE
        /// CHART").</summary>
        Route = 2,

        /// <summary>MSR — drag across the chart for a range and bearing line (navRig.js:362-367).</summary>
        Measure = 3,
    }

    /// <summary>
    /// The rail's layer switches (navRig.js:258 the first seven ids, mapped at navRig.js:531).
    ///
    /// <para><b>Three of these steer a pixel and four cannot</b>, and which is which is a fact about
    /// THIS world rather than a scope cut:</para>
    /// <list type="bullet">
    /// <item><see cref="Depth"/>, <see cref="Poi"/>, <see cref="Track"/> — live. The rig reads each of
    /// them in <c>drawChart</c> (js:310, js:359, js:328) and every one has a real source here.</item>
    /// <item><see cref="Land"/> — <b>the rig authors the button and then never reads the flag.</b>
    /// <c>layerOn</c> maps LAND→<c>'land'</c> (js:531) but <c>drawChart</c> consults
    /// <c>layers.depth/names/track/route/rocks/buoys/traffic/poi</c> and never <c>layers.land</c>. It
    /// lights, and it moves nothing, in the source too.</item>
    /// <item><see cref="Rocks"/>, <see cref="Buoys"/> — the rig's <c>ROCKS</c>/<c>BUOYS</c> arrays
    /// (js:147-156) are hand-authored furniture for its fictional chart. This world publishes no rock
    /// or buoy seam, so there is nothing to switch off.</item>
    /// <item><see cref="Traffic"/> — no contacts seam exists (S5's blocker); the layer already ships
    /// empty on the console face for the same reason.</item>
    /// </list>
    ///
    /// <para>The four dormant rows are still DRAWN, lit, in the rail — that is the S6 precedent (a rig
    /// element with no source renders rather than vanishing), and hiding them would misreport the
    /// instrument's own kit. They are simply not interactive, and the tests pin that: a dormant row
    /// must move NO pixel on the chart, which is the only assertion that can tell "inert on purpose"
    /// apart from "wired and broken".</para>
    /// </summary>
    [System.Flags]
    public enum NavChartLayers
    {
        None = 0,
        Land = 1 << 0,
        Rocks = 1 << 1,
        Buoys = 1 << 2,
        Poi = 1 << 3,
        Track = 1 << 4,
        Depth = 1 << 5,
        Traffic = 1 << 6,

        /// <summary>Everything on — how a plotter wakes up.</summary>
        All = Land | Rocks | Buoys | Poi | Track | Depth | Traffic,

        /// <summary>
        /// The layers with a real source in THIS world, and therefore the only ones a rail press may
        /// move. The one list — the renderer honours exactly these, the host's rail handler refuses
        /// everything else, and the tests assert that a dormant row moves no pixel. Three copies of
        /// this set would be three chances for a switch to light up and do nothing (#421).
        /// </summary>
        Live = Poi | Track | Depth,
    }

    /// <summary>How far the measure line has got (navRig.js:362 — the rig draws an anchored line with
    /// no far end as a degenerate one, and only labels it once both ends exist).</summary>
    public enum NavMeasureState
    {
        /// <summary>No line — the column shows the rig's own prompt.</summary>
        Off = 0,

        /// <summary>One end down, dragging: the line draws, the range/bearing label does not.</summary>
        Anchored = 1,

        /// <summary>Both ends: line, label, and the manager's RNG/BRG rows.</summary>
        Complete = 2,
    }

    /// <summary>
    /// Everything the MAX face adds to <see cref="NavRigState"/> — the rail's switches, the manager's
    /// selection, the measure line, the tidal set, and the name editor's buffer. A pure value for the
    /// same reason <see cref="NavRigState"/> is one: the renderer never reaches for a service, and the
    /// host's repaint key is a handful of field compares rather than a reflective struct equality.
    ///
    /// <para><b>The tide HEIGHT is not here</b> — it is <see cref="NavRigState.TideMetres"/>, which has
    /// existed since PR 2 and been fed zero ever since because no shipped pixel read it. This slice
    /// ports the field that reads it (navRig.js:399, the MAX top bar), so the existing member finally
    /// carries a real number. <see cref="float.NaN"/> means "no tide service", and the glass prints the
    /// same "--" it prints for a depth off the survey.</para>
    /// </summary>
    public readonly struct NavMaxState
    {
        public readonly NavChartTool Tool;
        public readonly NavChartLayers Layers;

        /// <summary>Index into the region's waypoint list that the manager has selected, or −1
        /// (navRig.js:426 <c>selWpt</c>).</summary>
        public readonly int SelectedWaypoint;

        public readonly NavMeasureState Measure;
        public readonly Vector2 MeasureFrom, MeasureTo;      // world metres

        /// <summary>
        /// The tidal set (the compass bearing the current runs TOWARD) and its drift in knots —
        /// navRig.js:400 and :444.
        ///
        /// <para><b>Real sim data, not a placeholder.</b> <c>EnvironmentSample.CurrentVector</c> is the
        /// deterministic tidal set the whole game already runs on: <c>BoatController</c> sails on
        /// <c>vel − CurrentVector</c>, the water surface flows along it, the wake and the weed ride it.
        /// The plotter reads that same vector, so the number on the glass is the current the hull is
        /// actually in.</para>
        ///
        /// <para><see cref="float.NaN"/> when no environment service is registered.</para>
        /// </summary>
        public readonly float SetDeg, DriftKnots;

        /// <summary>True while the waypoint NAME editor is taking characters. The glass draws the
        /// buffer with a cursor; the helm's keys are deaf for as long as it is set
        /// (<c>HelmKeyCapture</c>).</summary>
        public readonly bool Editing;

        /// <summary>The name being typed, already sanitised to the font's charset by
        /// <see cref="NavNameEditor"/>. Never null while <see cref="Editing"/>.</summary>
        public readonly string EditText;

        public NavMaxState(NavChartTool tool, NavChartLayers layers, int selectedWaypoint,
                           NavMeasureState measure, Vector2 measureFrom, Vector2 measureTo,
                           float setDeg, float driftKnots, bool editing, string editText)
        {
            Tool = tool;
            Layers = layers;
            SelectedWaypoint = selectedWaypoint;
            Measure = measure;
            MeasureFrom = measureFrom;
            MeasureTo = measureTo;
            SetDeg = setDeg;
            DriftKnots = driftKnots;
            Editing = editing;
            EditText = editText ?? "";
        }

        /// <summary>How a plotter's MAX face wakes: every layer on, the neutral tool, nothing selected,
        /// no line, no set. Used by the renderer's console path (where none of it is drawn) and as the
        /// starting point a host mutates.</summary>
        public static NavMaxState Default => new NavMaxState(
            NavChartTool.Pan, NavChartLayers.All, -1, NavMeasureState.Off, Vector2.zero, Vector2.zero,
            float.NaN, float.NaN, false, "");

        public bool LayerOn(NavChartLayers layer) => (Layers & layer) != 0;

        /// <summary>Is there a set and drift to print? A missing environment service is honest as
        /// "--", never as a confident 000°/0.0KN.</summary>
        public bool HasSet => !float.IsNaN(SetDeg) && !float.IsNaN(DriftKnots);

        public NavMaxState WithTool(NavChartTool tool) => new NavMaxState(
            tool, Layers, SelectedWaypoint, Measure, MeasureFrom, MeasureTo, SetDeg, DriftKnots,
            Editing, EditText);

        public NavMaxState WithLayerToggled(NavChartLayers layer) => new NavMaxState(
            Tool, Layers ^ layer, SelectedWaypoint, Measure, MeasureFrom, MeasureTo, SetDeg, DriftKnots,
            Editing, EditText);

        public NavMaxState WithSelection(int index) => new NavMaxState(
            Tool, Layers, index, Measure, MeasureFrom, MeasureTo, SetDeg, DriftKnots, Editing, EditText);

        public NavMaxState WithMeasure(NavMeasureState state, Vector2 from, Vector2 to) => new NavMaxState(
            Tool, Layers, SelectedWaypoint, state, from, to, SetDeg, DriftKnots, Editing, EditText);

        public NavMaxState WithSet(float setDeg, float driftKnots) => new NavMaxState(
            Tool, Layers, SelectedWaypoint, Measure, MeasureFrom, MeasureTo, setDeg, driftKnots,
            Editing, EditText);

        public NavMaxState WithEdit(bool editing, string text) => new NavMaxState(
            Tool, Layers, SelectedWaypoint, Measure, MeasureFrom, MeasureTo, SetDeg, DriftKnots,
            editing, text);
    }
}
