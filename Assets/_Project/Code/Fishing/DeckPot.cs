using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;   // BaitDef (Fishing → Economy is allowed)

namespace HiddenHarbours.Fishing
{
    /// <summary>Where one deck animal is in the work cycle. Transient, session-only — never saved.</summary>
    public enum DeckAnimalFate
    {
        /// <summary>Still in the pot — grab it out.</summary>
        InPot = 0,
        /// <summary>Picked and sorted a keeper — waiting on the deck for its claws to be banded.</summary>
        OnDeck = 1,
        /// <summary>Banded and stowed — it landed in the hold (the FishCaught path) and counts.</summary>
        Banded = 2,
        /// <summary>Sorted back over the side (a short or a berried hen) — value zero, gone.</summary>
        Returned = 3,
    }

    /// <summary>What one grab attempt resolved to.</summary>
    public enum GrabOutcome
    {
        /// <summary>Nothing left in the pot to grab.</summary>
        None = 0,
        /// <summary>The animal got you — recoil, it stays in the pot, try again (time, never harm).</summary>
        Nipped = 1,
        /// <summary>Out and legal — a keeper, set on the deck awaiting the band.</summary>
        Keeper = 2,
        /// <summary>Out but under the gauge — sorted back over the side.</summary>
        ReturnedShort = 3,
        /// <summary>Out but a berried hen — back she goes regardless of size.</summary>
        ReturnedBerried = 4,
    }

    /// <summary>What a banding attempt resolved to.</summary>
    public enum BandOutcome
    {
        /// <summary>No keeper waiting to band.</summary>
        None = 0,
        /// <summary>The hold is full — the keeper stays on deck; sell and come back.</summary>
        NoRoom = 1,
        /// <summary>Banded and stowed (the caller publishes <see cref="FishCaught"/>).</summary>
        Banded = 2,
    }

    /// <summary>
    /// The pot ON THE DECK — the logical state of the post-haul deck work (trap-fishing arc Build 7),
    /// engine-light and fully EditMode-testable. Built at pot-aboard from the trap's placement facts plus
    /// the catch the deterministic resolver already decided (Build 3 — this class never re-rolls WHAT was
    /// caught); it derives each animal's size / berried / keeper verdict through the pure
    /// <see cref="DeckWork"/> streams, then walks the owner's cycle: grab (nip risk) → sort → band → bait.
    ///
    /// <para><b>Transient by design (the ADR 0020 greybox compromise).</b> Nothing here is saved: the
    /// trap's DTO leaves the save the moment the pot comes aboard, so a reload can never re-haul it (no
    /// dupes), and <see cref="AutoResolve"/> lands whatever is still aboard as if picked + banded per the
    /// deterministic sort (no re-rolls, no loss beyond what the sort would have returned anyway).</para>
    /// </summary>
    public sealed class DeckPot
    {
        /// <summary>One animal in the pot/on the deck, with its deterministic facts derived once at
        /// pot-aboard. A class (not a struct) so fate/attempts mutate in place in the one list.</summary>
        public sealed class Animal
        {
            /// <summary>The catch item as the Build-3 resolver landed it — banding stows EXACTLY this
            /// (same species, weight, value: zero economy change for keepers).</summary>
            public CatchItem Item;
            /// <summary>The animal's index in the resolved catch — part of its stable seed identity.</summary>
            public int Index;
            /// <summary>Deterministic size (mm). 0 when the species has no sort rule.</summary>
            public float SizeMm;
            /// <summary>Deterministic berried flag (egg-carrying hen — always goes back).</summary>
            public bool Berried;
            /// <summary>The sort verdict, pre-derived (keeper = legal size and not berried).</summary>
            public bool Keeper;
            /// <summary>Whether a sort rule existed for the species (no rule ⇒ always-keeper).</summary>
            public bool HasRule;
            /// <summary>Greybox silhouette shape (from the rule; lobster default).</summary>
            public DeckAnimalShape Shape;
            /// <summary>Owner-authored sprite override from the rule (null = code-built silhouette).</summary>
            public UnityEngine.Sprite SpriteOverride;
            /// <summary>The rule's crawl/tail-flip loop frames (null/empty = still sprite). Presentation only.</summary>
            public UnityEngine.Sprite[] CrawlFrames;
            /// <summary>Crawl-loop cadence (frames/second) from the rule. Presentation only.</summary>
            public float CrawlFps;
            /// <summary>The REAR pose shown while this animal is lifted out of the pot (null = still).</summary>
            public UnityEngine.Sprite RearSprite;
            /// <summary>The DEFEND pose (claws up) flashed on a nip (null = still).</summary>
            public UnityEngine.Sprite DefendSprite;
            /// <summary>Where the animal is in the cycle.</summary>
            public DeckAnimalFate Fate;
            /// <summary>How many grab attempts this animal has seen (the nip stream's attempt index).</summary>
            public int GrabAttempts;
        }

        private readonly List<Animal> _animals;

        /// <summary>The trap kind aboard (its bait requirement drives the re-bait).</summary>
        public TrapDef Trap { get; }

        /// <summary>The deck-work ruleset (feel + sort rules — all data).</summary>
        public DeckWorkDef Def { get; }

        /// <summary>The hauled trap's stable instance id (the seed identity).</summary>
        public string InstanceId { get; }

        /// <summary>The hauled trap's placement instant (the seed identity).</summary>
        public double PlacementGameTimeSeconds { get; }

        /// <summary>The world seed the trap was placed under (the seed identity).</summary>
        public int WorldSeed { get; }

        /// <summary>True once the emptied pot has been re-baited (the bait was consumed from stock).</summary>
        public bool Baited { get; private set; }

        /// <summary>The bait loaded by the re-bait (its favours ride into the next set), or null.</summary>
        public BaitDef LoadedBait { get; private set; }

        /// <summary>All animals, in catch order (read-only view for the controller/tests).</summary>
        public IReadOnlyList<Animal> Animals => _animals;

        /// <summary>Build the deck pot from the placement facts + the ALREADY-RESOLVED catch. Derives each
        /// animal's deterministic size / berried / keeper verdict here, once — the same facts always build
        /// the same animals (the EditMode-pinned guarantee).</summary>
        public DeckPot(TrapDef trap, DeckWorkDef def, string instanceId, double placementGameTimeSeconds,
                       int worldSeed, IReadOnlyList<CatchItem> catchItems)
        {
            Trap = trap;
            Def = def;
            InstanceId = instanceId;
            PlacementGameTimeSeconds = placementGameTimeSeconds;
            WorldSeed = worldSeed;

            int n = catchItems != null ? catchItems.Count : 0;
            _animals = new List<Animal>(n);
            for (int i = 0; i < n; i++)
            {
                CatchItem item = catchItems[i];
                var a = new Animal { Item = item, Index = i, Fate = DeckAnimalFate.InPot };

                if (def != null && def.TryGetRule(item.SpeciesId, out SpeciesDeckRule rule))
                {
                    a.HasRule = true;
                    a.Shape = rule.Shape;
                    a.SpriteOverride = rule.AnimalSprite;
                    a.CrawlFrames = rule.CrawlFrames;
                    a.CrawlFps = rule.CrawlFps;
                    a.RearSprite = rule.RearSprite;
                    a.DefendSprite = rule.DefendSprite;
                    uint sizeHash = DeckWork.AnimalHash(worldSeed, instanceId, placementGameTimeSeconds,
                                                        item.SpeciesId, i, DeckWork.SizeChannel);
                    uint berriedHash = DeckWork.AnimalHash(worldSeed, instanceId, placementGameTimeSeconds,
                                                           item.SpeciesId, i, DeckWork.BerriedChannel);
                    a.SizeMm = DeckWork.SizeMm(sizeHash, rule.SizeMinMm, rule.SizeMaxMm);
                    a.Berried = DeckWork.RollBerried(berriedHash, rule.CanBeBerried, rule.BerriedChance01);
                    a.Keeper = DeckWork.IsKeeper(a.SizeMm, a.Berried, rule.MinKeepSizeMm);
                }
                else
                {
                    // No rule for the species — nothing gates it back. Always a keeper (documented on the
                    // Def; content validation requires rules for the shipped traps' species).
                    a.HasRule = false;
                    a.Keeper = true;
                }
                _animals.Add(a);
            }
        }

        // ---- counts / lookups (no LINQ — rule 7 discipline even off the hot path) ----------------

        /// <summary>How many animals are still IN the pot.</summary>
        public int InPotCount => CountFate(DeckAnimalFate.InPot);

        /// <summary>How many keepers wait on the deck, unbanded.</summary>
        public int OnDeckCount => CountFate(DeckAnimalFate.OnDeck);

        /// <summary>How many keepers have been banded + stowed.</summary>
        public int BandedCount => CountFate(DeckAnimalFate.Banded);

        /// <summary>How many went back over the side.</summary>
        public int ReturnedCount => CountFate(DeckAnimalFate.Returned);

        private int CountFate(DeckAnimalFate fate)
        {
            int c = 0;
            for (int i = 0; i < _animals.Count; i++)
                if (_animals[i].Fate == fate) c++;
            return c;
        }

        /// <summary>The next animal a grab would take (catch order), or null when the pot is empty.</summary>
        public Animal NextInPot()
        {
            for (int i = 0; i < _animals.Count; i++)
                if (_animals[i].Fate == DeckAnimalFate.InPot) return _animals[i];
            return null;
        }

        /// <summary>The next keeper a band would take (catch order), or null when none wait.</summary>
        public Animal NextOnDeck()
        {
            for (int i = 0; i < _animals.Count; i++)
                if (_animals[i].Fate == DeckAnimalFate.OnDeck) return _animals[i];
            return null;
        }

        /// <summary>The pot has been picked empty (no animals left inside).</summary>
        public bool PotEmpty => InPotCount == 0;

        /// <summary>Empty pot, everything sorted and banded, but not yet re-baited — the bait verb is next.</summary>
        public bool NeedsBait => PotEmpty && OnDeckCount == 0 && !Baited;

        /// <summary>Baited and squared away — T sets her, as today.</summary>
        public bool ReadyToSet => PotEmpty && OnDeckCount == 0 && Baited;

        // ---- the verbs (pure state — the controller supplies input/toasts/visuals) ---------------

        /// <summary>
        /// Resolve one GRAB that was held for <paramref name="heldSeconds"/>: the care read eases the nip
        /// chance (<see cref="DeckWork.Care01"/>), the nip roll is the animal's deterministic attempt
        /// stream. A nip leaves the animal in the pot (attempt counted — the next roll is a fresh stream
        /// value, still deterministic); a clean grab sorts it on the spot: keeper → the deck, short /
        /// berried → returned. <paramref name="grabbed"/> is the animal worked (null on
        /// <see cref="GrabOutcome.None"/>).
        /// </summary>
        public GrabOutcome TryGrabNext(float heldSeconds, out Animal grabbed)
        {
            grabbed = NextInPot();
            if (grabbed == null || Def == null) return GrabOutcome.None;

            int attempt = grabbed.GrabAttempts;
            grabbed.GrabAttempts = attempt + 1;

            float care = DeckWork.Care01(heldSeconds, Def.QuickGrabSeconds, Def.FullGrabSeconds);
            float chance = DeckWork.NipChance01(care, Def.NipChanceRushed01, Def.NipChanceCareful01);
            uint nipHash = DeckWork.AnimalHash(WorldSeed, InstanceId, PlacementGameTimeSeconds,
                                               grabbed.Item.SpeciesId, grabbed.Index, DeckWork.NipChannel, attempt);
            if (DeckWork.RollNip(nipHash, chance)) return GrabOutcome.Nipped;

            if (grabbed.Keeper)
            {
                grabbed.Fate = DeckAnimalFate.OnDeck;
                return GrabOutcome.Keeper;
            }
            grabbed.Fate = DeckAnimalFate.Returned;
            return grabbed.Berried ? GrabOutcome.ReturnedBerried : GrabOutcome.ReturnedShort;
        }

        /// <summary>
        /// Band the next waiting keeper and stow it: <see cref="IHold.TryAdd"/> with EXACTLY the resolved
        /// <see cref="CatchItem"/> — the same downstream path the old instant-land used (the caller
        /// publishes <see cref="FishCaught"/> on <see cref="BandOutcome.Banded"/>). A full hold leaves the
        /// keeper on deck, no penalty.
        /// </summary>
        public BandOutcome TryBandNext(IHold hold, out Animal banded)
        {
            banded = NextOnDeck();
            if (banded == null) return BandOutcome.None;
            if (hold == null || !hold.TryAdd(banded.Item)) return BandOutcome.NoRoom;
            banded.Fate = DeckAnimalFate.Banded;
            return BandOutcome.Banded;
        }

        /// <summary>Mark the pot re-baited (the controller has already consumed one bait from stock).
        /// The loaded bait's favours ride into the next set.</summary>
        public void MarkBaited(BaitDef bait)
        {
            Baited = true;
            LoadedBait = bait;
        }

        /// <summary>
        /// The cozy AUTO-RESOLVE (save/region-change/teardown — the ADR 0020 greybox compromise):
        /// everything still aboard lands as if picked + banded per the ALREADY-DERIVED deterministic sort.
        /// Keepers go into the hold (each appended to <paramref name="landed"/> so the caller can publish
        /// <see cref="FishCaught"/> — the unchanged land path); shorts/berried count as returned. No
        /// re-rolls, no dupes (banded animals are skipped), and anything a full hold refuses is counted in
        /// <paramref name="noRoom"/> so the one toast can say so — never silent.
        /// </summary>
        public void AutoResolve(IHold hold, List<CatchItem> landed, out int kept, out int returned, out int noRoom)
        {
            kept = 0; returned = 0; noRoom = 0;
            for (int i = 0; i < _animals.Count; i++)
            {
                Animal a = _animals[i];
                if (a.Fate != DeckAnimalFate.InPot && a.Fate != DeckAnimalFate.OnDeck) continue;

                if (!a.Keeper)
                {
                    a.Fate = DeckAnimalFate.Returned;
                    returned++;
                    continue;
                }
                if (hold != null && hold.TryAdd(a.Item))
                {
                    a.Fate = DeckAnimalFate.Banded;
                    landed?.Add(a.Item);
                    kept++;
                }
                else
                {
                    noRoom++;   // honest count — the toast says it, nothing vanishes silently
                }
            }
        }
    }
}
