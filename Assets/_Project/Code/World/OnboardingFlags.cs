namespace HiddenHarbours.World
{
    /// <summary>
    /// The three opening-sequence flags (VS-21), as named accessors over an <see cref="IFlagStore"/>.
    /// They make the inheritance opening play once: met Aunt Ginny, read Ned's logbook, and finished
    /// the first full loop (cast off → fish → return → sell). Persisted via the store so a quit +
    /// reload doesn't re-trigger the intro.
    ///
    /// Pure logic over an injected store, so it is fully unit-testable: tests use an
    /// <see cref="InMemoryFlagStore"/> for semantics and a <see cref="PlayerPrefsFlagStore"/> to
    /// prove persistence across a fresh instance. Keys are stable strings (the same ids the greybox
    /// wires into each Interactable's completion flag).
    /// </summary>
    public sealed class OnboardingFlags
    {
        public const string MetGinnyKey    = "met_ginny";
        public const string ReadLogbookKey = "read_logbook";
        public const string OnboardedKey   = "onboarded";

        // ⭐ A FOURTH FLAG LIVES IN THIS SAME STORE AND IS NOT DECLARED HERE:
        // HiddenHarbours.App.ArrivalOpening.ArrivedFlagKey ("arrived_st_peters"), which says the player
        // has been piloted in (owner's ruling, 2026-08-19 — the opening's step ZERO, the one before
        // MetGinny). It is written through ISaveService.SetFlag into the very same OnboardingFlags list,
        // so a reader on either side sees it; it is DECLARED over there only because HiddenHarbours.App
        // may not reference HiddenHarbours.World (rule 4) and the arrival holds a boat, a player and a
        // save. Named here so the set of opening flags can still be found in one place.

        private readonly IFlagStore _store;

        public OnboardingFlags(IFlagStore store)
        {
            _store = store ?? new InMemoryFlagStore();
        }

        public bool MetGinny    { get => _store.Get(MetGinnyKey);    set => _store.Set(MetGinnyKey, value); }
        public bool ReadLogbook { get => _store.Get(ReadLogbookKey); set => _store.Set(ReadLogbookKey, value); }
        public bool Onboarded   { get => _store.Get(OnboardedKey);   set => _store.Set(OnboardedKey, value); }

        /// <summary>Read a flag by its stable key (e.g. an Interactable's completion flag).</summary>
        public bool Get(string key) => !string.IsNullOrEmpty(key) && _store.Get(key);

        /// <summary>Set a flag by its stable key. Empty/null keys are ignored (no-op).</summary>
        public void Set(string key, bool value)
        {
            if (!string.IsNullOrEmpty(key)) _store.Set(key, value);
        }
    }
}
