using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Draws the player in what they are wearing</b> — the one place the wardrobe's decision reaches
    /// a pixel. It resolves the worn outfit to a list of colour substitutions
    /// (<see cref="CharacterPalette"/>) and pushes them onto the player's <see cref="SpriteRenderer"/>,
    /// then does nothing at all until the outfit changes.
    ///
    /// <para><b>Why a MaterialPropertyBlock rather than a material instance.</b>
    /// <see cref="PlayerSubmergeVisual"/> already owns the player's material — it swaps in a
    /// <c>PlayerSubmerge</c> instance so the waterline can be per-player. Two components racing to own
    /// one <c>sharedMaterial</c> is a bug waiting for a load order to expose it, and a property block
    /// is exactly the seam that avoids it: per-renderer values layered over whatever material is
    /// there, set by one component and read by another's shader with neither holding the other. It
    /// also means this component composes with any future character shader that includes
    /// <c>CharacterPalette.hlsl</c>, rather than only with the one the wading effect happens to
    /// install.</para>
    ///
    /// <para><b>It re-skins on a SIGNAL, never on a poll.</b> Clothes change when the player changes
    /// them, which is a handful of times in a save — so the steady-state cost of this component is
    /// zero: no <c>Update</c>, no allocation, no per-frame work of any kind (rule 7). The one-off cost
    /// when it does fire is two <c>SetVectorArray</c> calls.</para>
    ///
    /// <para><b>It fails to the BAKED look, loudly.</b> An outfit id that no longer resolves, a
    /// wardrobe asset that failed to load, a ramp key retired by a drop — every one of them clears the
    /// palette and logs, so the fisher appears in the outfit her sheets were baked in rather than in
    /// half a costume or in magenta. The player's saved choice is NOT rewritten by any of this: it sits
    /// in the save waiting for the content to come back (see <see cref="SaveMigration"/>'s v11→v12
    /// note).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerOutfitVisual : MonoBehaviour
    {
        [Tooltip("The wardrobe to resolve outfit ids in. Left empty (the runtime case) it is loaded " +
                 "from Resources, the same way the icon / fish / seaweed libraries reach the game.")]
        [SerializeField] private CharacterWardrobeDef _wardrobe;

        [Tooltip("Squared distance, in LINEAR colour space, within which a pixel counts as one of the " +
                 "baked palette's colours. Measured, not chosen: the closest a swappable colour comes " +
                 "to any other colour in the fisher's sheets is 0.00353, so this radius of 0.00176 " +
                 "cannot capture a neighbour, and float error (~1e-7) cannot escape a match.")]
        [SerializeField, Min(0f)] private float _matchEpsilonSquared = 3.1e-06f;

        private static readonly int IdPaletteFrom = Shader.PropertyToID("_CharPaletteFrom");
        private static readonly int IdPaletteTo = Shader.PropertyToID("_CharPaletteTo");
        private static readonly int IdPaletteCount = Shader.PropertyToID("_CharPaletteCount");
        private static readonly int IdPaletteEpsSq = Shader.PropertyToID("_CharPaletteEpsSq");

        /// <summary>A colour no sprite can hold, so an unused array slot can never match a pixel even
        /// if a stale count outlived its palette. Negative components are unreachable for any texture
        /// sample, linear or not.</summary>
        private static readonly Vector4 NoColour = new Vector4(-1f, -1f, -1f, 0f);

        private SpriteRenderer _renderer;
        private MaterialPropertyBlock _block;
        private readonly List<CharacterColourSwap> _swaps = new(CharacterPalette.MaxSwaps);
        private readonly Vector4[] _from = new Vector4[CharacterPalette.MaxSwaps];
        private readonly Vector4[] _to = new Vector4[CharacterPalette.MaxSwaps];
        private string _appliedOutfitId;
        private bool _applied;

        /// <summary>How many colour substitutions are currently on the renderer. For tests / tooling —
        /// 0 is the baked look and a perfectly ordinary state, not a failure.</summary>
        public int AppliedSwapCount { get; private set; }

        /// <summary>The outfit id currently drawn, or empty for the baked look. For tests / tooling.</summary>
        public string AppliedOutfitId => _appliedOutfitId ?? "";

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_wardrobe == null) _wardrobe = Resources.Load<CharacterWardrobeDef>(CharacterWardrobeDef.ResourcesPath);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OutfitChanged>(OnOutfitChanged);
            Apply(OutfitLocker.WornOutfitId());
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OutfitChanged>(OnOutfitChanged);
        }

        private void OnOutfitChanged(OutfitChanged e) => Apply(e.OutfitId);

        /// <summary>
        /// Dress the fisher in <paramref name="outfitId"/>. Public so a picker can PREVIEW an outfit on
        /// the live player without committing it to the save — which is the whole of the wardrobe's
        /// interaction: the fisher standing there IS the preview, so there is no paper-doll screen to
        /// build and nothing to keep in sync with her (the diegetic-UI canon).
        ///
        /// <para>Idempotent: re-applying what is already drawn touches nothing.</para>
        /// </summary>
        public void Apply(string outfitId)
        {
            string wanted = string.IsNullOrEmpty(outfitId) ? "" : outfitId;
            if (_applied && string.Equals(_appliedOutfitId, wanted, System.StringComparison.Ordinal)) return;

            _swaps.Clear();

            if (wanted.Length > 0)
            {
                if (_wardrobe == null)
                {
                    Debug.LogError(
                        $"[PlayerOutfitVisual] Wearing '{wanted}' but no CharacterWardrobeDef was found " +
                        $"(Resources/{CharacterWardrobeDef.ResourcesPath}). Drawing the baked outfit; the " +
                        "player's choice is untouched in the save.", this);
                }
                else
                {
                    var status = _wardrobe.TryBuildPalette(wanted, _swaps, out string reason);
                    if (status != CharacterPaletteStatus.Ok)
                    {
                        _swaps.Clear();
                        Debug.LogError(
                            $"[PlayerOutfitVisual] Refusing outfit '{wanted}' ({status}): {reason}. " +
                            "Drawing the baked outfit; the player's choice is untouched in the save.", this);
                    }
                }
            }

            Push();
            _appliedOutfitId = wanted;
            _applied = true;
        }

        /// <summary>Upload whatever is in <see cref="_swaps"/>. An empty list is the passthrough — count
        /// 0 makes the shader's substitution one compare against zero and no work at all.</summary>
        private void Push()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) return;

            int n = Mathf.Min(_swaps.Count, CharacterPalette.MaxSwaps);
            for (int i = 0; i < CharacterPalette.MaxSwaps; i++)
            {
                if (i < n)
                {
                    _from[i] = Linear(_swaps[i].From);
                    _to[i] = Linear(_swaps[i].To);
                }
                else
                {
                    _from[i] = NoColour;
                    _to[i] = NoColour;
                }
            }

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetVectorArray(IdPaletteFrom, _from);
            _block.SetVectorArray(IdPaletteTo, _to);
            _block.SetFloat(IdPaletteCount, n);
            _block.SetFloat(IdPaletteEpsSq, Mathf.Max(0f, _matchEpsilonSquared));
            _renderer.SetPropertyBlock(_block);

            AppliedSwapCount = n;
        }

        /// <summary>
        /// The colour as the FRAGMENT SHADER will see it. The project renders in Linear colour space,
        /// so an sRGB sprite is linearised by the sampler before the substitution compares anything —
        /// uploading the raw bytes would compare two different spaces and match nothing at all. This is
        /// the single most likely way for this feature to fail silently, so it has a test of its own.
        /// </summary>
        private static Vector4 Linear(Color32 c)
        {
            Color lin = ((Color)c).linear;
            return new Vector4(lin.r, lin.g, lin.b, 1f);
        }
    }

    /// <summary>
    /// Attaches <see cref="PlayerOutfitVisual"/> to the on-foot player once it exists — the SELF-INSTALL
    /// path, so the wardrobe needs no builder wiring and survives a scene rebuild. A copy of
    /// <see cref="PlayerSubmergeInstaller"/>'s shape on purpose: the two solve the same problem, and a
    /// second spelling of it would be a second thing to keep in step.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerOutfitInstaller : MonoBehaviour
    {
        [SerializeField] private string _playerObjectName = "Player";
        private float _timer;

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("PlayerOutfitInstaller") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<PlayerOutfitInstaller>();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.5f;   // cheap poll — the player appears within a scene or two of boot

            var go = GameObject.Find(_playerObjectName);
            if (go == null) return;   // not up yet (menu scene / mid-load) — try again

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null && go.GetComponent<PlayerOutfitVisual>() == null)
                go.AddComponent<PlayerOutfitVisual>();

            Destroy(gameObject);
        }
    }
}
