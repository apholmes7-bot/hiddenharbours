using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What kind of washroom a <see cref="WashroomDef"/> describes. Append-only, explicit values.</summary>
    public enum WashroomKind
    {
        /// <summary>A bathroom inside a building (home, shop, plant).</summary>
        Bathroom = 0,

        /// <summary>A small outdoor building with a door and one toilet seat.</summary>
        Outhouse = 1,

        /// <summary>A boat's head (toilet, and on bigger hulls a sink and a shower).</summary>
        BoatHead = 2,
    }

    /// <summary>
    /// <b>A WASHROOM, AS DATA</b> (cleanliness slice 1, owner ask 2026-11-15: "add the missing bathrooms to
    /// buildings... add outhouses to areas which don't have them with openable doors and players can sit on
    /// them, npcs will use the washrooms"). One asset per washroom TYPE under
    /// <c>Data/Resources/Washrooms/</c>, id <c>washroom.snake_case</c>: which fixtures it holds and whether it
    /// has a door. The placement lanes stand it in a scene or a hull; this slice ships the outhouse type only.
    ///
    /// <para><b>Not a building system.</b> Buildings on main are not data-driven enough to declare one
    /// without a scene edit, so <see cref="Prefab"/> stays empty until the outhouse prefab is authored
    /// (<c>docs/world/washrooms-plan.md</c>).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Washroom", fileName = "Washroom")]
    public sealed class WashroomDef : ScriptableObject
    {
        /// <summary>The shipped door frame time, for a booth with no Def wired (tests, a stray prefab).</summary>
        public const float DefaultDoorFrameSeconds = 0.12f;

        /// <summary>The shipped open-door beat, for a booth with no Def wired.</summary>
        public const float DefaultDoorHoldOpenSeconds = 0.4f;

        [Tooltip("Stable id, append-only (washroom.snake_case, e.g. washroom.outhouse).")]
        public string Id = "washroom.example";

        [Tooltip("Shown name.")]
        public string DisplayName = "";

        [Tooltip("What kind of washroom this is.")]
        public WashroomKind Kind = WashroomKind.Bathroom;

        [Tooltip("The fixtures inside, one entry each (an outhouse: one toilet).")]
        public WashStationDef[] Fixtures = new WashStationDef[0];

        [Tooltip("Has a door of its own.")]
        public bool HasDoor = true;

        [Tooltip("The door opens and closes (owner: outhouses 'with openable doors'). Uses the seamless-" +
                 "interiors door (press moves the leaf).")]
        public bool DoorOpens = true;

        [Tooltip("Real seconds each door frame shows as the door swings (shut, ajar, open). Presentation only.")]
        [Min(0.01f)] public float DoorFrameSeconds = DefaultDoorFrameSeconds;

        [Tooltip("Real seconds the door stands wide open while he steps in or out, before it swings shut.")]
        [Min(0f)] public float DoorHoldOpenSeconds = DefaultDoorHoldOpenSeconds;

        [Tooltip("The prefab the placement lane stands in the world. Empty until it is authored.")]
        public GameObject Prefab;

        /// <summary>True when this washroom holds at least one fixture of <paramref name="kind"/>.</summary>
        public bool Has(WashStationKind kind)
        {
            if (Fixtures == null) return false;
            for (int i = 0; i < Fixtures.Length; i++)
                if (Fixtures[i] != null && Fixtures[i].Kind == kind) return true;
            return false;
        }
    }
}
