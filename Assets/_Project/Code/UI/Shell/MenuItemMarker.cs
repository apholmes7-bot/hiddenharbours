using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The chalk tick that appears beside the shell menu line the keyboard or gamepad is sitting on.
    ///
    /// <para><b>Why a marker at all.</b> The strip behind a selected line brightens, and brightness alone
    /// is the one channel this project never relies on (charter DoD, accessibility §8). The tick is the
    /// shape channel: the line you are on is the line with a mark against it.</para>
    ///
    /// <para>Event-driven — it moves on <see cref="ISelectHandler"/> / <see cref="IDeselectHandler"/>,
    /// which fire exactly when the selection moves, rather than polling
    /// <c>EventSystem.currentSelectedGameObject</c> every frame. The shell does no per-frame work
    /// (rule 7). Added by <see cref="PaperUi.MakeMenuItem"/>; nothing else should need it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuItemMarker : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private Text _marker;

        /// <summary>Point it at the (initially blank) label that holds the tick.</summary>
        public void Bind(Text marker) => _marker = marker;

        public void OnSelect(BaseEventData eventData)
        {
            if (_marker != null) _marker.text = ShellStrings.MenuMarker;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (_marker != null) _marker.text = string.Empty;
        }
    }
}
