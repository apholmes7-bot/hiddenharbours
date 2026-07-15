using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// SELF-INSTALLING (mirrors the AudioDirector / DayNightController / BoatWakeEmitter pattern):
    /// makes sure every vendor stall in every loaded scene has a buy-screen driver, with NO builder
    /// edit and NO builder re-run. The Greywick builder deliberately left the St Peters opening
    /// vendors (harbourmaster's cod licence, the general store's rod, the dory yard's damaged dory)
    /// without an input driver — "the named seam ui-ux attaches its driver to". This is that
    /// attachment: after each scene load it scans the scene's <see cref="Shipwright"/>/<see
    /// cref="GearShop"/>/<see cref="LicenseVendor"/> components and adds a <see cref="DevBuyInput"/>
    /// (P, on-foot + in-reach) to any stall that lacks one.
    ///
    /// <para>Runs only on scene loads — never per frame. Idempotent: stalls that already carry a
    /// driver (the Punt shipwrights wired by the builders) are left untouched. Removable wholesale
    /// when the real Interact intent lands and world-content wires interactions properly.</para>
    /// </summary>
    public static class BuyPointInstaller
    {
        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Scan();   // the first scene has already loaded by AfterSceneLoad
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Scan();

        // FindObjectsByType sweeps ALL loaded scenes (the additive region-travel model), so a scan
        // after any load also repairs stalls in scenes toggled back on. Ensure() is idempotent.
        private static void Scan()
        {
            Ensure(Object.FindObjectsByType<Shipwright>(FindObjectsSortMode.None));
            Ensure(Object.FindObjectsByType<GearShop>(FindObjectsSortMode.None));
            Ensure(Object.FindObjectsByType<LicenseVendor>(FindObjectsSortMode.None));
            Ensure(Object.FindObjectsByType<PotShop>(FindObjectsSortMode.None));
        }

        private static void Ensure<T>(T[] vendors) where T : Component
        {
            for (int i = 0; i < vendors.Length; i++)
            {
                var go = vendors[i].gameObject;
                if (go.GetComponent<DevBuyInput>() == null)
                    go.AddComponent<DevBuyInput>();
            }
        }
    }
}
