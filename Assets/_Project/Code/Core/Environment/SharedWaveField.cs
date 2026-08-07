namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE eased sea — the one the shader was actually handed</b> (owner playtest 2026-08-07:
    /// <i>"the hulls of most boats do not tend to stay at the waterline level"</i>). The Art-side
    /// <c>WaveFieldBridge</c> publishes the phase-continuous <see cref="WaveTrains"/> it packs into
    /// the shader globals each tick; every water-riding consumer on the Boats side reads THOSE
    /// trains instead of deriving a lookalike of its own. Same seam shape, same reasoning and the
    /// same lifetime rules as <see cref="DisplacedSea"/> — the two feature modules meet in Core and
    /// never each other (CLAUDE.md rule 4).
    ///
    /// <para><b>Why this had to exist: "same code, same inputs" is not the same sea.</b>
    /// <see cref="WaveFieldAnimator"/> is stateful by design — it removes the phase JUMP that the
    /// closed form <c>θ = k·(d·pos − c·t)</c> suffers whenever the weather drifts k and c, by
    /// accumulating travel phase INCREMENTALLY (<c>Φ += k·c·dt</c>) instead. That accumulator starts
    /// at <b>zero on each animator's own first tick, and again on every <see cref="WaveFieldAnimator.Reset"/></b>.
    /// The bridge resets on wake and on every scene load; a <c>BoatWaveMotion</c> resets in its own
    /// <c>OnEnable</c> — when she is skinned, when a region loads, when a hull is swapped, whenever
    /// she is re-enabled at all. Two animators seeded at two different moments hold two different Φ,
    /// so their trains differ by an arbitrary constant phase: <b>the same wave, at the wrong
    /// moment</b>. The hull then heaves with a sea of exactly the right size and period that is not
    /// the sea drawn around her — she bobs up as the water goes down, and never settles at her
    /// waterline however well her flotation data is tuned. The bridge's own comment asserted the
    /// opposite ("the same animator code the boat ticks with the same inputs — the identical eased
    /// sea"): true of the code and the inputs, false of the STATE, and nothing was measuring it.
    /// It is the last unclosed half of the ONE-SEA rule, after ADR 0023 closed the frequency scale
    /// (<see cref="DisplacedSeaState.FreqScale"/>) and the watertight clamp closed its own reads at
    /// the published globals.</para>
    ///
    /// <para><b>Sample the published trains at <c>timeSeconds = 0</c></b>, exactly as
    /// <see cref="WaveFieldAnimator.Sample"/> does: the accumulated travel already rides in each
    /// train's <see cref="WaveTrain.PhaseOffset"/>, which is the whole point of the animator's
    /// output convention. Passing a real game time would add the travel twice.</para>
    ///
    /// <para><b>Nothing published ⇒ nothing changes.</b> <see cref="TryGet"/> returns false and every
    /// consumer falls back to the local animator it ticks today, byte-identically: EditMode tests,
    /// bare demo scenes, edit-time builders and any run without the bridge keep exactly the
    /// behaviour they have. That is the A/B contract this seam ships under.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Presentation-only, never saved, and — like the animator
    /// it carries — deterministic for a given tick sequence rather than a pure function of game
    /// time. That is precisely why it must be SHARED rather than reproduced: a stateful smoother
    /// can only be relied on to agree with itself. The simulation contract is untouched:
    /// seakeeping FORCES and anything gameplay-consequential still read the pure
    /// <c>WaveMath.TrainsFrom</c> + <c>Sample(pos, gameTime)</c> path (the ADR 0018 addendum
    /// boundary), and nothing here feeds them.</para>
    /// </summary>
    public static class SharedWaveField
    {
        private static object s_Owner;
        private static WaveTrains s_Trains;

        /// <summary>True while a publisher has handed over the eased field — the gate a rider uses
        /// to choose the shared sea over its own local animator.</summary>
        public static bool IsPublished => s_Owner != null;

        /// <summary>
        /// The published eased trains, to be sampled at <c>timeSeconds = 0</c> (see the class doc).
        /// False (and <see cref="WaveTrains.None"/>) when nothing has published — the fallback
        /// contract.
        /// </summary>
        public static bool TryGet(out WaveTrains trains)
        {
            trains = s_Trains;
            return s_Owner != null;
        }

        /// <summary>Publish the eased field for this tick — called by the bridge with the SAME
        /// trains it packs into the shader globals, immediately around that push, so the sea the
        /// riders read and the sea the shader draws cannot be one tick apart. One sea per region:
        /// with multiple publishers the last one wins, exactly like <see cref="DisplacedSea"/>.</summary>
        public static void Publish(object owner, in WaveTrains trains)
        {
            if (owner == null) return;
            s_Owner = owner;
            s_Trains = trains;
        }

        /// <summary>Clear the field — only by its current owner, so a stale publisher going away
        /// cannot silence a newer one. Riders fall back to their local animators, which is the
        /// pre-seam behaviour exactly.</summary>
        public static void Clear(object owner)
        {
            if (!ReferenceEquals(s_Owner, owner)) return;
            s_Owner = null;
            s_Trains = WaveTrains.None;
        }
    }
}
