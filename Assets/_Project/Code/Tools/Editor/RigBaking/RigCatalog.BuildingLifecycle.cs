using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The BUILDING LIFECYCLE PASS — construction phases and dereliction for every iso building.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entry below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> BuildingLifecycleRigs() =>
            new RigRegistration
            {
                // ⭐ NOT A RIG — A PASS, and the only entry in the catalog that draws nothing of its
                // own. It runs between a host rig's build(b) and its paint(), reads the finished
                // building's REAL faces through survey(), and hands back the same building at an
                // earlier or a later point in its life. Because it reads the faces rather than a
                // preset table, every build config is covered with no per-config authoring.
                //
                // It installs globalThis.BuildingLifecycle and exposes no W/H/pivot triple, so it
                // loads through InstallModule like the head and eye rigs — Install would throw on the
                // missing pivot. The convention is the placeholder every non-directional entry
                // carries (dialogueBubble, shellfish, catchKit): nothing probes it, because the pass
                // has no azimuth of its own — it inherits whatever facing the host is rendering.
                //
                // ⚠️ IT IS DECLARED AS A PREREQUISITE OF ALL THREE HOSTS BELOW, AND THAT IS THE WHOLE
                // POINT. The hook in each host reads `root.BuildingLifecycle` and no-ops when it is
                // absent — so a bake that forgets to load the pass does not throw, it renders the
                // FINISHED building and writes it to a sheet named `collapsing`. That is the rigs'
                // shared failure mode (resolve as opts[k] ?? fallback, never complain) and exactly
                // what the prerequisite mechanism exists to prevent.
                //
                // ⭐ LOADING IT COSTS THE EXISTING BAKES NOTHING — MEASURED, not assumed. With the
                // pass present and no lifecycle keys in the options, all three hosts render
                // BYTE-IDENTICAL to their pre-hook selves (wharfBuilding netShed/cannery/default,
                // houseIso redSaltbox/default, shopfront default — same V8, same sha256).
                // BuildingLifecyclePassTests re-measures that every run rather than trusting this note.
                ["buildingLifecycle"] = new RigEntry(
                    $"{RigFolder}/building-lifecycle-kit/buildingLifecycleRig.js",
                    "BuildingLifecycle", AzimuthConvention.Clockwise),
            };
    }
}
