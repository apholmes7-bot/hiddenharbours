using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The fish on the line once she is up</b>: which species and size rung is drawn (resolved once
    /// per fish, never per frame), her dart or thrash at the published offset facing her travel, the
    /// line's attach at the baked mouth anchor, and a hooked jumper clearing the water on
    /// <see cref="FishJumpArc"/>'s schedule. Shared by every presenter that draws a rod fight.
    ///
    /// <para><b>Reads no clock and keeps none.</b> The frame step, the seconds in the phase and the
    /// seconds she has been up are handed in by the caller each frame; a jump's re-entry is handed back
    /// as a splash for the caller's <see cref="RodSplashFlourish"/>. Deterministic (rule 5): the jump
    /// schedule is seeded from the species and the weight (<see cref="FightSeed"/>), never drawn.</para>
    /// </summary>
    public sealed class SurfacedFishDrawer
    {
        private const int Directions = 8;

        private readonly SpriteRenderer _sr;

        private string _resolvedFishId;
        private int _speciesIndex = -1;
        private FishSpeciesVisual _resolvedSpecies;
        private float _resolvedWeightKg = float.NaN;
        private FishRungVisual _rung;
        private FishArtLibrary _mouths;   // the table whose per-frame mouths win (the shared one by default)

        private bool _fishJumps;          // does THIS species clear the water (the Core behaviour seam)
        private float _fightJumpPeriod;   // seconds between her attempts, from the Def or the fallback
        private int _fightJumpSeed;       // this fight's own seed: species + weight, never Random
        private double _jumpStart = double.NegativeInfinity;   // when the live jump began
        private bool _jumpSplashed = true;                     // has this jump's re-entry splash played

        private Vector2 _lastFishPos;
        private Vector2 _lastFishHeading;                      // her travel, so a jump goes somewhere
        private float _fishSpeed;
        private int _fishRow = Directions / 2;
        private int _flankSide;                                // the side of the line she last showed (0 none)
        private int _travelRow = Directions / 2;               // the row her own travel picks: a jump is drawn on it

        /// <summary>The angle off the line inside which she is drawn side-on
        /// (<see cref="RodPresenterMath.FlankRowHeading"/>). Null, the default, reads
        /// <c>GameConfig.FishFeel.FlankWithinLineDegrees</c> live each frame; a test that pins the
        /// travel-only rows sets 0.</summary>
        public float? FlankWithinLineDegreesOverride { get; set; }

        /// <summary>The seconds between churn splashes while she thrashes. Null, the default, reads
        /// <c>GameConfig.FishFeel.ThrashChurnPeriodSeconds</c> live; a test that counts only a jump's
        /// re-entry splashes sets 0.</summary>
        public float? ThrashChurnPeriodSecondsOverride { get; set; }

        private SpriteRenderer _underSr;   // her rows under the waterline, tinted (made on the first cut)
        private long _churnBeat;           // the last churn period a splash was thrown for

        /// <param name="renderer">The renderer she is drawn on (its owner may share it with the deep
        /// shadow; this drawer only writes it from <see cref="Render"/>).</param>
        public SurfacedFishDrawer(SpriteRenderer renderer) { _sr = renderer; }

        /// <summary>The baked frame drawn last (whole, even when it is drawn cut at her waterline), or null.</summary>
        public Sprite DrawnFrame { get; private set; }

        /// <summary>The renderer her rows under the waterline are drawn on, or null before the first cut.</summary>
        public SpriteRenderer UnderwaterRenderer => _underSr;

        /// <summary>The splash handed back by the last <see cref="Render"/> is a churn (the caller plays it from
        /// <c>FishFeel.ThrashChurnFirstSplashFrame</c>), not a jump's re-entry.</summary>
        public bool LastSplashIsChurn { get; private set; }

        /// <summary>Where the caller starts its splash flipbook for the splash the last <see cref="Render"/> handed
        /// back: a churn plays only the open rings (<c>FishFeel.ThrashChurnFirstSplashFrame</c>), a jump's re-entry
        /// the whole splash.</summary>
        public int SplashFirstFrame => LastSplashIsChurn ? Feel().ThrashChurnFirstSplashFrame : 0;

        /// <summary>The last <see cref="Render"/> was darting, but close in by the fisher, so she was drawn
        /// thrashing at the surface with her mouth clear of his figure.</summary>
        public bool DartHeldAtSurface { get; private set; }

        /// <summary>
        /// THE BREAK: while the surfacing splash plays its column and crown (the frames before
        /// <see cref="FishFeelSettings.ThrashChurnFirstSplashFrame"/>, at <paramref name="splashFps"/>) the column
        /// is the break-surface beat and no fish is drawn under it; she is first drawn at the first open ring.
        /// Read from the time she has been up, so a seek and a stepped frame draw the same picture. With no
        /// splash sheet that reaches an open ring (<paramref name="splashFrameCount"/>) there is no column to
        /// hide her under, and she is drawn at once.
        /// </summary>
        public bool InBreakColumn(float surfaceSeconds, float splashFps, int splashFrameCount)
        {
            int ring = Feel().ThrashChurnFirstSplashFrame;
            return splashFps > 0f && splashFrameCount > ring && surfaceSeconds >= 0f && surfaceSeconds < ring / splashFps;
        }

        /// <summary>
        /// LANDED, ON THE LINE: draw her hanging head-up from the line, her grip (the held sheet's baked grip,
        /// <see cref="FishFeelSettings.HangGripRowsAbovePivot"/> rows over its pivot) at <paramref name="grip"/>,
        /// on her last fighting row, the held sheet's frames flipping at <paramref name="secondsPerFrame"/>.
        /// Whole, out of the water: no waterline cut. False (and nothing drawn) when her rung has no held sheet.
        /// </summary>
        public bool RenderHanging(Vector2 grip, float seconds, float secondsPerFrame, int sortingOrder, float drawScale)
        {
            FishRungVisual sp = _rung;
            if (sp == null || sp.HeldFrames == null || sp.HeldFrames.Length == 0 || sp.HeldFramesPerDir <= 0)
            {
                Hide();
                return false;
            }
            int frame = RodPresenterMath.FlipFrame(seconds, secondsPerFrame, sp.HeldFramesPerDir);
            int idx = RodPresenterMath.SheetIndex(_fishRow, frame, sp.HeldFramesPerDir, sp.HeldFrames.Length);
            if (idx < 0 || sp.HeldFrames[idx] == null) { Hide(); return false; }
            Sprite held = sp.HeldFrames[idx];
            float gripM = Feel().HangGripRowsAbovePivot / Mathf.Max(1f, held.pixelsPerUnit) * drawScale;
            DrawnFrame = held;
            FishAirborne = false;
            _sr.sprite = held;
            _sr.transform.position = new Vector3(grip.x, grip.y - gripM, 0f);
            _sr.sortingOrder = sortingOrder;
            _sr.enabled = true;
            HideUnderwater();
            return true;
        }

        /// <summary>Take her out of the picture: the fish and her rows under the water.</summary>
        public void Hide()
        {
            if (_sr != null) _sr.enabled = false;
            HideUnderwater();
        }

        /// <summary>Take only her rows under the water away (the owner is about to draw something else on
        /// her renderer, the deep shadow).</summary>
        public void HideUnderwater()
        {
            if (_underSr != null) _underSr.enabled = false;
        }

        /// <summary>The species entry drawn for the current hook in the owner's table, or −1.</summary>
        public int SpeciesIndex => _speciesIndex;

        /// <summary>The size rung drawn for the current hook, or null.</summary>
        public FishRungVisual Rung => _rung;

        /// <summary>Is she out of the water right now. <b>Meaningful only while she is drawn at the
        /// surface</b> (<see cref="FishingPhase.FightSurface"/>): it is written by <see cref="Render"/> and is
        /// not cleared when the phase moves on (the golden pins that it stays true through a landing that
        /// follows a jump), so no caller may read it as "in the air" outside FightSurface.</summary>
        public bool FishAirborne { get; private set; }

        /// <summary>How many jumps this fight has drawn; reset when the species on the line changes.</summary>
        public int JumpsDrawn { get; private set; }

        /// <summary>
        /// Take a published state: re-resolve the species (and ask <paramref name="facts"/> what she
        /// does on the line) only when the fish id changes, and the rung only when the species or the
        /// weight moves. True when the rung was re-picked, so the owner can re-pick what follows from it.
        /// </summary>
        public bool Resolve(in FishingState next, FishSpeciesVisual[] species, FishArtLibrary fishArt,
                            IFishBehaviourFacts facts, float fallbackJumpPeriodSeconds)
        {
            // Species lookup only when the id actually changes (no per-publish string work), and the
            // RUNG only when the species or the weight moves. Both are sticky for the whole fight.
            bool speciesMoved =
                !string.Equals(next.FishId, _resolvedFishId, System.StringComparison.Ordinal);
            if (speciesMoved)
            {
                _resolvedFishId = next.FishId;
                _speciesIndex = FindSpecies(species, next.FishId);
                // The scene's own ladder when it has rungs, else the shared fish art table's.
                _resolvedSpecies = string.IsNullOrEmpty(next.FishId)
                    ? null
                    : FishArtLibrary.Resolve(
                        species != null && _speciesIndex >= 0 ? species[_speciesIndex] : null,
                        fishArt != null ? fishArt : FishArtLibrary.Shared, next.FishId);

                // WHAT SHE DOES ON THE LINE, asked once per species through the Core seam (rule 4).
                _fishJumps = facts.Jumps(next.FishId);
                float stated = facts.FightJumpPeriodSeconds(next.FishId);
                _fightJumpPeriod = stated > 0f ? stated : fallbackJumpPeriodSeconds;

                // The fight's own seed: fixed for the whole fight, so the same fish jumps at the same
                // moments twice running (rule 5).
                _fightJumpSeed = FightSeed(next.FishId, next.WeightKg);
                _jumpStart = double.NegativeInfinity;
                _jumpSplashed = true;
                _churnBeat = 0;
                JumpsDrawn = 0;
                FishAirborne = false;
            }
            // NaN != NaN, so the first publish after a bite always resolves — 'no weight yet' and 'a
            // weight of zero' are different states.
            if (speciesMoved || next.WeightKg != _resolvedWeightKg)
            {
                _resolvedWeightKg = next.WeightKg;
                // Picked at the weight the fish is DRAWN at (the readability floor), the same rule the
                // hands and the tote use, so the fish he lands is the fish he then holds.
                FishArtLibrary table = fishArt != null ? fishArt : FishArtLibrary.Shared;
                _mouths = table;
                _rung = FishArtLibrary.LineRungFor(_resolvedSpecies, table != null ? table.CatchArt : null,
                                                   next.FishId, next.WeightKg);
                return true;
            }
            return false;
        }

        /// <summary>She has just come up: a jump is scheduled from this moment, so the first cannot land
        /// in the instant the surface phase opens.</summary>
        public void OnSurfaced()
        {
            _jumpStart = double.NegativeInfinity;
            _jumpSplashed = true;
            _churnBeat = 0;
        }

        /// <summary>
        /// Pick up a surface phase already under way, with no flourish: the jump due at
        /// <paramref name="surfaceSeconds"/> is taken as already splashed and already counted, so a drawer
        /// that attaches, or a clock that jumps (a sleep skip, a seek), partway through her time at the
        /// surface fires no re-entry splash and bumps no <see cref="JumpsDrawn"/> on its first frame. A
        /// pull-fed caller (a villager angler) calls it on any frame that does not follow the last drawn one.
        /// The player's own path never calls it.
        /// </summary>
        public void Prime(float surfaceSeconds)
        {
            _jumpStart = double.NegativeInfinity;
            _jumpSplashed = true;
            FishAirborne = false;
            // The churn due now is taken as already thrown, as the jump's splash is.
            _churnBeat = RodPresenterMath.ChurnBeat(surfaceSeconds, ChurnPeriod(Feel()));
            if (!_fishJumps || _fightJumpPeriod <= 0f) return;
            if (!FishJumpArc.TryNextJump(_fightJumpSeed, surfaceSeconds, _fightJumpPeriod, out double start)) return;
            _jumpStart = start;
            // Mid-air at the prime: she is drawn in the air, but the jump was not seen to start, so the
            // count's edge is already passed.
            FishAirborne = FishJumpArc.InAir(start, surfaceSeconds);
        }

        /// <summary>
        /// Forget her motion: her last position becomes <paramref name="pos"/>, her drawn speed zero and her
        /// travel none, so the next frame reads her speed from where she really is rather than from the
        /// origin or the last fight. For a pull-fed caller on surfacing and on any non-consecutive frame, so
        /// its picture does not depend on the frame history. The player's own path never calls it: its
        /// first surfaced frame reads a speed from the last position, as it always has (the golden pins it).
        /// </summary>
        public void ResetMotion(Vector2 pos)
        {
            _lastFishPos = pos;
            _lastFishHeading = Vector2.zero;
            _fishSpeed = 0f;
            _flankSide = 0;
        }

        /// <summary>
        /// Draw her at <paramref name="angler"/> + the published offset and return the line's far end
        /// (the mouth, or her position when the species has no art). <paramref name="dt"/> is this
        /// frame's step. <paramref name="stateSeconds"/> is the flipbook's clock: the seconds in the current
        /// phase. <paramref name="surfaceSeconds"/> is the jump schedule's clock: the seconds since she came
        /// up. The player's two clocks both reset on entering FightSurface and tick together, so they are
        /// equal while she is drawn; a villager's presenter passes its timeline's seconds in the phase for
        /// both. <paramref name="speedSmoothing01"/> eases her drawn speed (dart or thrash) toward this
        /// frame's. A jump that has just come down hands back <paramref name="splashDue"/> at
        /// <paramref name="splashAt"/>. <paramref name="drawScale"/> is the whole-number scale the caller
        /// draws her at (the player 1, a villager his look's FishDrawScale): the mouth is taken at that scale,
        /// so the line leaves the drawn mouth whatever size she is shown.
        ///
        /// <para><b>The mouth is the drawn frame's own</b>: the fish art table's anchor for the very sprite
        /// set this frame (<see cref="FishArtLibrary.TryMouthFor"/>, snapped onto that frame's art at import),
        /// else the rung's own table at the same index.</para>
        /// </summary>
        public Vector2 Render(Vector2 angler, in FishingState s, float dt, float stateSeconds,
                              float surfaceSeconds, float secondsPerFrame, float dartSpeedMps,
                              float speedSmoothing01, int sortingOrder, float drawScale,
                              out bool splashDue, out Vector2 splashAt)
        {
            splashDue = false;
            splashAt = default;
            LastSplashIsChurn = false;
            DartHeldAtSurface = false;
            Vector2 pos = angler + new Vector2(s.FishOffsetX, s.FishOffsetY);

            // Her speed picks dart vs thrash; her travel picks the row (held when she pauses), turned
            // across the line when she is coming straight down it so she shows her flank.
            if (dt > 1e-5f)
            {
                float instant = (pos - _lastFishPos).magnitude / dt;
                _fishSpeed = Mathf.Lerp(_fishSpeed, instant, speedSmoothing01);   // light smoothing, no state pop
                Vector2 vel = pos - _lastFishPos;
                if (vel.sqrMagnitude > 1e-6f)
                {
                    _lastFishHeading = vel;             // the way she is already going — a jump carries on it
                    Vector2 row = RodPresenterMath.FlankRowHeading(
                        vel, new Vector2(s.FishOffsetX, s.FishOffsetY), FlankWithinLineDegrees(), ref _flankSide);
                    _fishRow = IsoFacing.HeadingToFacingIndex(
                        RodPresenterMath.HeadingDegrees(row.x, row.y), Directions, 0f, false);
                    _travelRow = IsoFacing.HeadingToFacingIndex(
                        RodPresenterMath.HeadingDegrees(vel.x, vel.y), Directions, 0f, false);
                }
            }
            _lastFishPos = pos;

            FishRungVisual sp = _rung;
            if (sp == null) { Hide(); return pos; }

            // SHE CLEARS THE WATER (owner's ruling 2026-09-06), on the same half-sine arc a free fish
            // leaves the water on (FishJumpArc). A cod cannot get here: _fishJumps is her own flag.
            bool jumping = UpdateJump(angler, in s, surfaceSeconds, out float lift, out float travel01,
                                      out splashDue, out splashAt);

            bool dart = RodPresenterMath.IsDarting(_fishSpeed, dartSpeedMps);
            Sprite[] frames = jumping ? sp.JumpFrames : (dart ? sp.DartFrames : sp.ThrashFrames);
            int perDir = jumping ? sp.JumpFramesPerDir : (dart ? sp.DartFramesPerDir : sp.ThrashFramesPerDir);
            Vector2[] mouths = jumping ? sp.JumpMouthOffsets
                                       : (dart ? sp.DartMouthOffsets : sp.ThrashMouthOffsets);

            // No jump art baked for this rung? She fights on as she always did rather than vanishing.
            if (jumping && (frames == null || frames.Length == 0 || perDir <= 0))
            {
                jumping = false;
                lift = 0f;
                frames = dart ? sp.DartFrames : sp.ThrashFrames;
                perDir = dart ? sp.DartFramesPerDir : sp.ThrashFramesPerDir;
                mouths = dart ? sp.DartMouthOffsets : sp.ThrashMouthOffsets;
            }
            if (frames == null || frames.Length == 0) { Hide(); return pos; }

            // ⚠ The jump strip is played ACROSS its own travel, never on the spot (the rig's
            // MOTION.jump.travel). She carries on the way she was already going.
            if (jumping)
            {
                Vector2 heading = _lastFishHeading;
                if (heading.sqrMagnitude > 1e-6f)
                    pos += heading.normalized * (FishJumpArc.TravelMetres * travel01);
            }

            int frame = jumping
                ? FishJumpArc.FrameAt(_jumpStart, surfaceSeconds)
                : RodPresenterMath.FlipFrame(stateSeconds, secondsPerFrame, perDir);
            // A jump flies along her travel, so it is drawn on her travel's row; only the fight at the
            // surface is turned side-on to the line.
            int idx = RodPresenterMath.SheetIndex(jumping ? _travelRow : _fishRow, frame, perDir, frames.Length);
            if (idx < 0 || frames[idx] == null) { Hide(); return pos; }

            // BY THE FISHER: the dart art bakes her under the surface, drawn low on the frame. Close in, that
            // puts her drawn mouth inside his figure, where the line (behind him) cannot be seen. There she is
            // drawn thrashing at the surface instead, her mouth clear of him and the line to it in sight.
            FishFeelSettings feel = Feel();
            if (dart && !jumping && sp.ThrashFrames != null && sp.ThrashFrames.Length > 0 && sp.ThrashFramesPerDir > 0
                && RodPresenterMath.InFisherFigure(MouthOf(frames, mouths, idx, pos, 0f, drawScale), angler,
                                                   feel.FisherFigureHalfWidthM, feel.FisherFigureHeightM))
            {
                int thrashIdx = RodPresenterMath.SheetIndex(
                    _fishRow, RodPresenterMath.FlipFrame(stateSeconds, secondsPerFrame, sp.ThrashFramesPerDir),
                    sp.ThrashFramesPerDir, sp.ThrashFrames.Length);
                if (thrashIdx >= 0 && sp.ThrashFrames[thrashIdx] != null)
                {
                    dart = false;
                    DartHeldAtSurface = true;
                    frames = sp.ThrashFrames;
                    mouths = sp.ThrashMouthOffsets;
                    idx = thrashIdx;
                }
            }

            // IN THE WATER AT THE SURFACE: her rows under the waterline are drawn under it, tinted toward the
            // sea, on her own art grid (FishWaterlineCut). A jump is out of the water and is drawn whole.
            bool cuts = !jumping && (dart ? feel.DartCutsAtWaterline : feel.ThrashCutsAtWaterline);
            Sprite baked = frames[idx];
            DrawnFrame = baked;
            _sr.transform.position = new Vector3(pos.x, pos.y + lift, 0f);
            if (cuts && FishWaterlineCut.TrySplit(baked, feel.WaterlineRowsAbovePivot, out Sprite above, out Sprite below))
            {
                _sr.sprite = above;
                SpriteRenderer under = EnsureUnderwater();
                under.sprite = below;
                under.color = feel.FishUnderwaterTint;
                under.sortingLayerID = _sr.sortingLayerID;
                under.sortingOrder = sortingOrder;   // her two halves share no pixel, so one order serves both
                under.enabled = true;
            }
            else
            {
                _sr.sprite = baked;
                HideUnderwater();
            }
            _sr.sortingOrder = sortingOrder;
            _sr.enabled = true;

            // THE CHURN: while she thrashes, a small splash at her on the fight's own clock. A jump's re-entry
            // splash, due this frame, wins.
            if (!splashDue && !jumping && !dart)
            {
                long beat = RodPresenterMath.ChurnBeat(surfaceSeconds, ChurnPeriod(feel));
                if (beat < _churnBeat) _churnBeat = beat;   // a fresh fight's clock started again
                else if (beat > _churnBeat)
                {
                    _churnBeat = beat;
                    splashDue = true;
                    splashAt = pos;
                    LastSplashIsChurn = true;
                }
            }

            // The line follows her UP — the mouth anchor rides the same lift the sprite does.
            return MouthOf(frames, mouths, idx, pos, lift, drawScale);
        }

        /// <summary>The drawn mouth of <paramref name="frames"/>[<paramref name="idx"/>] drawn at
        /// <paramref name="pos"/> raised by <paramref name="lift"/>: the art table's anchor for that very sprite,
        /// else the rung's own table at the same index, else her position.</summary>
        private Vector2 MouthOf(Sprite[] frames, Vector2[] mouths, int idx, Vector2 pos, float lift, float drawScale)
        {
            Vector2 mouth = pos + new Vector2(0f, lift);
            if (_mouths != null && _mouths.TryMouthFor(frames[idx], out Vector2 drawn)) mouth += drawn * drawScale;
            else if (mouths != null && idx < mouths.Length) mouth += mouths[idx] * drawScale;
            return mouth;
        }

        /// <summary>
        /// THE HOOKED JUMPER'S SCHEDULE: one jump per period, its moment inside the period hashed off the
        /// fight's own seed (<see cref="FishJumpArc.TryNextJump"/>). The re-entry splash is handed back
        /// exactly once per jump.
        /// </summary>
        private bool UpdateJump(Vector2 angler, in FishingState s, float surfaceSeconds,
                                out float lift, out float travel01, out bool splashDue, out Vector2 splashAt)
        {
            lift = 0f;
            travel01 = 0f;
            splashDue = false;
            splashAt = default;

            if (!_fishJumps || _fightJumpPeriod <= 0f) { FishAirborne = false; return false; }

            double now = surfaceSeconds;
            if (!FishJumpArc.TryNextJump(_fightJumpSeed, surfaceSeconds, _fightJumpPeriod, out double start))
            {
                FishAirborne = false;
                return false;
            }

            if (start > _jumpStart)                       // a new period's jump has come due
            {
                _jumpStart = start;
                _jumpSplashed = false;
            }

            if (FishJumpArc.InAir(_jumpStart, now))
            {
                // Counted on the EDGE, so one jump is one count however many frames it spans.
                if (!FishAirborne) JumpsDrawn++;
                FishAirborne = true;
                lift = FishJumpArc.Arc01(_jumpStart, now) * FishJumpArc.TravelMetres;
                travel01 = FishJumpArc.Travel01(_jumpStart, now);
                return true;
            }
            FishAirborne = false;

            // She has come down: one splash where she went back in, then nothing until the next period.
            if (!_jumpSplashed && now >= _jumpStart)
            {
                _jumpSplashed = true;
                splashDue = true;
                splashAt = angler + new Vector2(s.FishOffsetX, s.FishOffsetY);
            }
            return false;
        }

        /// <summary>
        /// A fight's own seed, from the two things the sim fixes at the bite and never changes: which
        /// species and how heavy. Stable for the whole fight, different between fights, and computed —
        /// never drawn — so the jumps replay (rule 5).
        /// </summary>
        public static int FightSeed(string fishId, float weightKg) => FishFightSeed.For(fishId, weightKg);

        private float ChurnPeriod(in FishFeelSettings feel) =>
            ThrashChurnPeriodSecondsOverride ?? feel.ThrashChurnPeriodSeconds;

        private static FishFeelSettings Feel()
        {
            GameConfig config = GameServices.Config;
            return config != null ? config.FishFeel : FishFeelSettings.Default;
        }

        /// <summary>The renderer for her rows under the waterline: a child of hers, so it rides her position
        /// and her owner's draw scale, on her material.</summary>
        private SpriteRenderer EnsureUnderwater()
        {
            if (_underSr != null) return _underSr;
            var go = new GameObject("UnderTheWaterline");
            go.transform.SetParent(_sr.transform, false);
            _underSr = go.AddComponent<SpriteRenderer>();
            _underSr.sharedMaterial = _sr.sharedMaterial;
            _underSr.enabled = false;
            return _underSr;
        }

        private float FlankWithinLineDegrees()
        {
            if (FlankWithinLineDegreesOverride.HasValue) return FlankWithinLineDegreesOverride.Value;
            GameConfig config = GameServices.Config;
            return config != null ? config.FishFeel.FlankWithinLineDegrees
                                  : FishFeelSettings.Default.FlankWithinLineDegrees;
        }

        private static int FindSpecies(FishSpeciesVisual[] species, string fishId)
        {
            if (string.IsNullOrEmpty(fishId) || species == null) return -1;
            for (int i = 0; i < species.Length; i++)
                if (species[i] != null && string.Equals(species[i].FishId, fishId, System.StringComparison.Ordinal))
                    return i;
            return -1;
        }
    }
}
