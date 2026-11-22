using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>One wave source laid on the water: a ring of crests born at a place and a time.</summary>
    public struct HullWaveSource
    {
        /// <summary>Where it was laid, world metres. It stays there: the ring belongs to the water.</summary>
        public Vector2 Position;
        /// <summary>Game seconds (the clock's flow seconds) when it was laid.</summary>
        public double BirthSeconds;
        /// <summary>Its height at birth, metres.</summary>
        public float Amplitude;
        /// <summary>2π/λ of its crests.</summary>
        public float Wavenumber;
        /// <summary>How fast its crests run outward, m/s.</summary>
        public float PhaseSpeed;
        /// <summary>How fast its band of crests spreads, m/s.</summary>
        public float RingSpeed;
        /// <summary>Its band's width, metres.</summary>
        public float PacketWidth;
        /// <summary>Seconds it lives; its height is exactly 0 from then on.</summary>
        public float Lifetime;
        /// <summary>Its e-folding time, seconds.</summary>
        public float DecaySeconds;
        /// <summary>Seconds it takes to rise to full height after birth (0 = born at full).</summary>
        public float RiseSeconds;
        /// <summary>Seconds at the end of its life over which it fades out (0 = all its life).</summary>
        public float EndFadeSeconds;
        /// <summary>Who laid it (0 = nobody in particular). A hull never rides her own waves.</summary>
        public int Owner;
        /// <summary>Its crests' fixed phase offset, radians (0 = in step with every ring laid like it).
        /// A short hull's rings each take their own (<see cref="HullWaveMath.ScatterRing"/>).</summary>
        public float PhaseOffset;
        /// <summary>True when the water draws it as broken arcs, not a whole ring (a short hull's rings, from
        /// <see cref="HullWaveMath.ScatterRing"/>; how broken is the shader's _HHHullWaveArcs). Packed as the
        /// sign of its 1/width.</summary>
        public bool Arcs;
        /// <summary>The way the hull that laid it was going, as a unit vector, or zero. Non-zero (a hull
        /// under motor or sail, owner 11-21: "make motored boats a v wake") the water draws the ring as the
        /// straight piece of her V's arm it touches (<see cref="HullWaveMath.ArmHeight"/>); zero (a rowed hull,
        /// a splash) the ring is drawn whole, or as <see cref="Arcs"/>. Packed in the shader's b.w slot.</summary>
        public Vector2 Travel;
        /// <summary>A V ring only: the metres of arm it stands for (<see cref="HullWaveMath.VArmSpan"/>), which
        /// scales its piece so the pieces sum to her crest height. Packed in the shader's 1/width slot.</summary>
        public float ArmSpan;

        /// <summary>Its height at <paramref name="now"/>: birth height × <see cref="HullWaveMath.TimeFade"/>.</summary>
        public readonly float AmplitudeAt(double now)
        {
            return Amplitude * HullWaveMath.TimeFade((float)(now - BirthSeconds), Lifetime, DecaySeconds,
                                                     RiseSeconds, EndFadeSeconds);
        }

        /// <summary>What it is worth to the pool at <paramref name="now"/>: <see cref="AmplitudeAt"/>
        /// without the rise, so a ring still rising is not mistaken for the weakest and swapped out at
        /// birth.</summary>
        public readonly float StrengthAt(double now)
        {
            return Amplitude * HullWaveMath.TimeFade((float)(now - BirthSeconds), Lifetime, DecaySeconds,
                                                     0f, EndFadeSeconds);
        }

        /// <summary>True from the end of its life on.</summary>
        public readonly bool ExpiredAt(double now) => now - BirthSeconds >= Lifetime;
    }

    /// <summary>
    /// The fixed pool of live wave sources every hull and splash shares. A plain array sized once, so
    /// adding, ageing, sampling and packing never allocate (rule 7); when it is full the WEAKEST source
    /// (lowest height now) gives way, which is the one that would be missed least. Every operation is a
    /// pure function of the calls made, in order, so it is deterministic (rule 5).
    /// </summary>
    public sealed class HullWaveField
    {
        /// <summary>The water shader's compile-time bound (<c>HH_HULL_WAVE_MAX</c>); tests pin the two.</summary>
        public const int ShaderCapacity = 64;

        private readonly HullWaveSource[] _sources;
        private int _count;
        private int _limit;

        public HullWaveField(int capacity = ShaderCapacity)
        {
            _sources = new HullWaveSource[Mathf.Max(1, capacity)];
            _limit = _sources.Length;
        }

        /// <summary>The array's size.</summary>
        public int Capacity => _sources.Length;

        /// <summary>How many sources may live at once (the owner's <c>MaxSources</c>, at most
        /// <see cref="Capacity"/>). Lowering it drops the weakest at once.</summary>
        public int Limit
        {
            get => _limit;
            set
            {
                _limit = Mathf.Clamp(value, 0, _sources.Length);
                while (_count > _limit) RemoveAt(WeakestIndex());
            }
        }

        /// <summary>Live sources.</summary>
        public int Count => _count;

        /// <summary>The V's frame the samples draw a ring that carries a direction with
        /// (<see cref="HullWaveMath.VArmFrame"/>), set by the publisher each frame with the shader's.</summary>
        public Vector4 VArmFrame { get; set; }

        /// <summary>The V's mix (<see cref="HullWaveMath.VArmMix"/>: share, 1 / (2π · PacketWavelengths)), set by
        /// the publisher with <see cref="VArmFrame"/>. Zero share (unset) draws every ring whole, as the shader's
        /// unset global.</summary>
        public Vector4 VArmMix { get; set; }

        /// <summary>The clock reading the field was last advanced to.</summary>
        public double NowSeconds { get; private set; }

        /// <summary>The live source at <paramref name="index"/> (0 .. Count-1), oldest first.</summary>
        public HullWaveSource this[int index] => _sources[index];

        /// <summary>Forget every source.</summary>
        public void Clear()
        {
            _count = 0;
        }

        /// <summary>
        /// Move the field's clock to <paramref name="now"/> and retire the sources whose life is over.
        /// A clock that runs BACKWARDS (a load, a seek) clears the field: those waves were never laid
        /// in the world being shown.
        /// </summary>
        public void Advance(double now)
        {
            if (now < NowSeconds) Clear();
            NowSeconds = now;
            int w = 0;
            for (int r = 0; r < _count; r++)
            {
                if (_sources[r].ExpiredAt(now) || _sources[r].BirthSeconds > now) continue;
                if (w != r) _sources[w] = _sources[r];
                w++;
            }
            _count = w;
        }

        /// <summary>
        /// Lay a source. When the field is at its limit the weakest live source gives way, unless the new
        /// one is weaker still, in which case it is dropped. Returns whether it was kept.
        /// </summary>
        public bool Add(in HullWaveSource source)
        {
            if (_limit <= 0 || source.Amplitude <= 0f || source.Lifetime <= 0f) return false;
            if (_count >= _limit)
            {
                int weakest = WeakestIndex();
                double now = NowSeconds > source.BirthSeconds ? NowSeconds : source.BirthSeconds;
                if (_sources[weakest].StrengthAt(now) >= source.StrengthAt(now)) return false;
                RemoveAt(weakest);
            }
            _sources[_count++] = source;
            return true;
        }

        /// <summary>
        /// Lay a source only into a free slot: at the limit it is dropped and nothing gives way, so a
        /// splash never pushes a hull's wave out of the pool mid-life (no pop). Returns whether it was kept.
        /// </summary>
        public bool AddIfRoom(in HullWaveSource source)
        {
            if (_count >= _limit) return false;
            return Add(in source);
        }

        private int WeakestIndex()
        {
            int best = 0;
            float bestAmp = float.MaxValue;
            for (int i = 0; i < _count; i++)
            {
                float a = _sources[i].StrengthAt(NowSeconds);
                // Strictly less: on a tie the OLDEST (lowest index) goes, deterministically.
                if (a < bestAmp) { bestAmp = a; best = i; }
            }
            return best;
        }

        private void RemoveAt(int index)
        {
            for (int i = index; i < _count - 1; i++) _sources[i] = _sources[i + 1];
            _count--;
        }

        /// <summary>How many live sources <paramref name="owner"/> laid.</summary>
        public int CountOwnedBy(int owner)
        {
            int n = 0;
            for (int i = 0; i < _count; i++) if (_sources[i].Owner == owner) n++;
            return n;
        }

        /// <summary>
        /// The waves' height at <paramref name="worldXY"/> at the field's clock, metres, leaving out the
        /// sources <paramref name="excludeOwner"/> laid (0 leaves out none). Exactly 0 with no sources.
        /// </summary>
        public float SampleHeight(Vector2 worldXY, float spreadMetres, int excludeOwner = 0)
        {
            if (_count == 0) return 0f;
            float invSpread = spreadMetres > 0f ? 1f / spreadMetres : 0f;
            double now = NowSeconds;
            float h = 0f;
            for (int i = 0; i < _count; i++)
            {
                ref readonly HullWaveSource s = ref _sources[i];
                if (excludeOwner != 0 && s.Owner == excludeOwner) continue;
                h += SampleOne(in s, worldXY, now, invSpread, out _);
            }
            return h;
        }

        // One source's height and slope at a point: a whole ring (or broken arcs), or for a ring that carries
        // a direction the straight piece of her V's arm it touches, blended by the V's share. The shader's
        // HullWaveSample loop body is its twin.
        private float SampleOne(in HullWaveSource s, Vector2 worldXY, double now, float invSpread, out Vector2 slope)
        {
            slope = Vector2.zero;
            double age = now - s.BirthSeconds;
            float amp = s.AmplitudeAt(now);
            if (amp <= 0f || s.PacketWidth <= 0f) return 0f;
            Vector2 d = worldXY - s.Position;
            float r = d.magnitude;
            float front = s.RingSpeed * (float)age;
            if (!IsV(in s))
            {
                float phase = HullWaveMath.Phase(s.Wavenumber, s.PhaseSpeed, age, s.PhaseOffset);
                float invWidth = 1f / s.PacketWidth;
                if (r >= 1e-4f)
                    slope = d * (HullWaveMath.SourceSlope(r, amp, front, s.Wavenumber, phase, invWidth, invSpread) / r);
                return HullWaveMath.SourceHeight(r, amp, front, s.Wavenumber, phase, invWidth, invSpread);
            }
            Vector4 frame = VArmFrame, mix = VArmMix;
            float vPhase = HullWaveMath.VArmPhase(s.Wavenumber, front);
            float share = Mathf.Clamp01(mix.x);
            float h = HullWaveMath.ArmHeight(d, s.Travel, amp, front, s.Wavenumber, vPhase, s.ArmSpan, invSpread,
                                             frame, mix, out Vector2 g) * share;
            slope = g * share;
            if (share < 1f)
            {
                float invWidth = HullWaveMath.VArmInvWidth(s.Wavenumber, mix);
                float keep = 1f - share;
                h += HullWaveMath.SourceHeight(r, amp, front, s.Wavenumber, vPhase, invWidth, invSpread) * keep;
                if (r >= 1e-4f)
                    slope += d * (HullWaveMath.SourceSlope(r, amp, front, s.Wavenumber, vPhase, invWidth, invSpread) * keep / r);
            }
            return h;
        }

        // A ring the water draws as V arms: it carries a direction and the V's share is above 0.
        private bool IsV(in HullWaveSource s)
        {
            return VArmMix.x > 0f && (s.Travel.x != 0f || s.Travel.y != 0f);
        }

        /// <summary>The waves' slope (∂h/∂x, ∂h/∂y) at <paramref name="worldXY"/>; analytic, the same
        /// law the water lights the faces with.</summary>
        public Vector2 SampleSlope(Vector2 worldXY, float spreadMetres, int excludeOwner = 0)
        {
            if (_count == 0) return Vector2.zero;
            float invSpread = spreadMetres > 0f ? 1f / spreadMetres : 0f;
            double now = NowSeconds;
            Vector2 g = Vector2.zero;
            for (int i = 0; i < _count; i++)
            {
                ref readonly HullWaveSource s = ref _sources[i];
                if (excludeOwner != 0 && s.Owner == excludeOwner) continue;
                SampleOne(in s, worldXY, now, invSpread, out Vector2 one);
                g += one;
            }
            return g;
        }

        /// <summary>
        /// Write the live sources the water shader needs into <paramref name="a"/> and <paramref name="b"/>,
        /// already aged to the field's clock, so the GPU sees only small numbers:
        /// <c>a = (x, y, height now, band radius now)</c>, <c>b = (k, phase now, ±1/width, 0)</c> (negative for
        /// a ring drawn as broken arcs, <see cref="HullWaveSource.Arcs"/>); a V ring (one that carries a direction
        /// while the V's share is above 0) packs <c>b = (k, k · front, its ArmSpan, travel)</c>, travel being
        /// <see cref="HullWaveMath.PackTravel"/> of <see cref="HullWaveSource.Travel"/>, and the shader works its
        /// width out of k (<see cref="HullWaveMath.VArmInvWidth"/>). The shader works the reach (the radius past
        /// which the ring draws nothing) out of the same numbers this culls with (<c>front + PacketReach ×
        /// width</c>; <see cref="HullWaveMath.VArmReach"/> for a V ring). Sources whose reach misses
        /// <paramref name="view"/> (when <paramref name="cullToView"/>) are skipped, which is most of the
        /// cost saved. Returns how many were written; nothing past that is touched.
        /// </summary>
        public int Pack(Vector4[] a, Vector4[] b, Rect view, bool cullToView)
        {
            if (a == null || b == null) return 0;
            int max = Mathf.Min(a.Length, b.Length);
            int n = 0;
            double now = NowSeconds;
            for (int i = 0; i < _count && n < max; i++)
            {
                ref readonly HullWaveSource s = ref _sources[i];
                float amp = s.AmplitudeAt(now);
                if (amp <= 0f || s.PacketWidth <= 0f) continue;
                double age = now - s.BirthSeconds;
                float front = s.RingSpeed * (float)age;
                bool v = IsV(in s);
                float reach = v ? HullWaveMath.VArmReach(front, s.Wavenumber, s.ArmSpan, VArmFrame, VArmMix)
                                : front + HullWaveMath.PacketReach * s.PacketWidth;
                if (cullToView && !CircleTouchesRect(s.Position, reach, view)) continue;
                a[n] = new Vector4(s.Position.x, s.Position.y, amp, front);
                b[n] = v
                    ? new Vector4(s.Wavenumber, HullWaveMath.VArmPhase(s.Wavenumber, front), s.ArmSpan,
                                  HullWaveMath.PackTravel(s.Travel))
                    : new Vector4(s.Wavenumber, HullWaveMath.Phase(s.Wavenumber, s.PhaseSpeed, age, s.PhaseOffset),
                                  (s.Arcs ? -1f : 1f) / s.PacketWidth, 0f);
                n++;
            }
            return n;
        }

        /// <summary>True when a circle overlaps a rectangle.</summary>
        public static bool CircleTouchesRect(Vector2 centre, float radius, Rect rect)
        {
            float cx = Mathf.Clamp(centre.x, rect.xMin, rect.xMax);
            float cy = Mathf.Clamp(centre.y, rect.yMin, rect.yMax);
            float dx = centre.x - cx, dy = centre.y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }
    }

    /// <summary>
    /// One chain of sources a hull lays from one point of herself (her stern, or her bow). It keeps only
    /// where and when it last looked; each <see cref="Step"/> lays the sources due since then at their
    /// exact times, each placed where she was at that time along the straight line between the two
    /// looks. So the same voyage lays the same sources at 30, 60 or 144 frames a second.
    /// </summary>
    public struct HullWaveChain
    {
        private Vector2 _lastPosition;
        private double _lastSeconds;
        private double _nextSeconds;
        private bool _primed;

        /// <summary>Start again from the next look (a teleport, a scene change, her ends swapped).</summary>
        public void Reset()
        {
            _primed = false;
        }

        /// <summary>
        /// Look at the hull's point now and lay what is due. Returns how many sources were laid.
        /// </summary>
        /// <param name="field">The shared pool.</param>
        /// <param name="position">The point, world metres, now.</param>
        /// <param name="now">The clock's flow seconds now.</param>
        /// <param name="speed">Her speed through the water, m/s.</param>
        /// <param name="lengthMetres">Her length, metres (sizes and times her waves).</param>
        /// <param name="share">This chain's share of her crest height (1 at the stern, BowShare at the bow).</param>
        /// <param name="budget">Most live sources this chain may hold.</param>
        /// <param name="owner">Who she is (0 = nobody).</param>
        public int Step(HullWaveField field, Vector2 position, double now, float speed, float lengthMetres,
                        float share, int budget, int owner, in HullWaveSettings s)
            => Step(field, position, now, speed, lengthMetres, share, budget, owner, Vector2.zero, in s);

        /// <summary>
        /// <see cref="Step(HullWaveField, Vector2, double, float, float, float, int, int, in HullWaveSettings)"/>
        /// for a hull going <paramref name="travel"/> (any length; zero = rowed). Non-zero, and
        /// <c>MotorVShare</c> above 0, she lays the clean rings of her V (owner 11-21: "make motored boats a v
        /// wake"): each carries her direction (<see cref="HullWaveSource.Travel"/>) and is laid as a long
        /// hull's, never scattered or broken (<see cref="HullWaveMath.ForMotor"/>), whatever her length. Zero
        /// lays exactly what the shorter overload always did.
        /// </summary>
        public int Step(HullWaveField field, Vector2 position, double now, float speed, float lengthMetres,
                        float share, int budget, int owner, Vector2 travel, in HullWaveSettings settings)
        {
            if (field == null) return 0;
            bool motor = HullWaveMath.LaysV(travel, in settings);
            HullWaveSettings s = motor ? HullWaveMath.ForMotor(in settings) : settings;
            Vector2 heading = motor ? travel.normalized : Vector2.zero;
            if (!_primed || now < _lastSeconds)
            {
                _lastPosition = position;
                _lastSeconds = now;
                _nextSeconds = now;
                _primed = true;
                return 0;
            }

            double span = now - _lastSeconds;
            if (span <= 0.0) return 0;   // a held clock lays nothing and loses nothing

            // A short hull lays her sources at her own spacing (HullWaveSettings.SmallHullSpacingWavelengths).
            HullWaveSettings hs = HullWaveMath.ForHull(lengthMetres, in s);
            float wavelength = HullWaveMath.Wavelength(speed, lengthMetres, in hs);
            float lifetime = HullWaveMath.KeptLifetime(HullWaveMath.Lifetime(lengthMetres, in hs), speed,
                                                       wavelength, budget, in hs);
            float crest = HullWaveMath.CrestHeight(speed, lengthMetres, in hs) * Mathf.Max(share, 0f);
            float interval = HullWaveMath.DepositInterval(speed, wavelength, lifetime, budget,
                                                          hs.SpacingWavelengths);
            int laid = 0;
            if (crest <= 0f || float.IsInfinity(interval))
            {
                // Not making waves: the next one is due a full interval after she starts again.
                _nextSeconds = now;
            }
            else
            {
                if (_nextSeconds < _lastSeconds) _nextSeconds = _lastSeconds;
                // A long hitch could owe more than the chain may hold; lay at most its budget, the
                // newest ones, which are the ones still near her.
                double owed = (now - _nextSeconds) / interval;
                if (owed > budget) _nextSeconds = now - (budget - 1) * (double)interval;

                float k = 2f * Mathf.PI / wavelength;
                float amp = HullWaveMath.SourceAmplitude(crest, speed * interval, wavelength, s.SourceGain);
                var source = new HullWaveSource
                {
                    Amplitude = amp,
                    Wavenumber = k,
                    // Her own speed: the crests along her track keep station with her (the transverse
                    // waves), which is the deep-water speed of the wave that keeps pace with her; held
                    // to MaxCrestHertz × λ so a short hull's short waves do not flash (owner 11-21).
                    PhaseSpeed = HullWaveMath.CrestSpeed(speed, wavelength, in s),
                    RingSpeed = HullWaveMath.RingSpeed(speed, s.KelvinHalfAngleDegrees),
                    PacketWidth = HullWaveMath.PacketWidth(wavelength, in s),
                    Lifetime = lifetime,
                    DecaySeconds = lifetime * Mathf.Max(s.DecayShare, 0.01f),
                    RiseSeconds = lifetime * Mathf.Clamp01(s.RiseShare),
                    EndFadeSeconds = lifetime * Mathf.Clamp01(s.EndFadeShare),
                    Owner = owner,
                    Travel = heading,
                    // A V ring's piece of the arm stands for its spacing (HullWaveMath.VArmSpan): the pieces
                    // overlap and sum to her crest height, so the arm is one line whatever the spacing.
                    ArmSpan = motor ? HullWaveMath.VArmSpan(speed * interval, wavelength, s.SourceGain, in s) : 0f,
                };
                // A short hull's rings each differ a little, and are laid a little unevenly, so her V and
                // track read as disturbed water, not a chain of stacked loops (HullWaveMath.ScatterRing).
                while (_nextSeconds <= now && laid < budget)
                {
                    float t = (float)((_nextSeconds - _lastSeconds) / span);
                    source.Position = Vector2.LerpUnclamped(_lastPosition, position, t);
                    source.BirthSeconds = _nextSeconds;
                    HullWaveSource ring = HullWaveMath.ScatterRing(in source, lengthMetres, share, in s);
                    field.Add(in ring);
                    laid++;
                    _nextSeconds += interval * HullWaveMath.SpacingScatter(source.BirthSeconds, lengthMetres, share, in s);
                }
            }

            _lastPosition = position;
            _lastSeconds = now;
            return laid;
        }
    }

    /// <summary>
    /// The Core seam for the live hull-wave field (rule 4): the Art-side publisher installs the field it
    /// ages and hands to the water shader; hulls lay sources into it; a hull's drawn ride and any splash
    /// read or add through here, never through each other's classes. Absent (no publisher) every read
    /// is exactly 0 and every add is a no-op.
    /// </summary>
    public static class HullWaves
    {
        private static HullWaveField s_Field;

        /// <summary>The live field, or null.</summary>
        public static HullWaveField Field => s_Field;

        /// <summary>True while a publisher has installed a field.</summary>
        public static bool IsActive => s_Field != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_Field = null;

        /// <summary>Install <paramref name="field"/> as the live one.</summary>
        public static void Install(HullWaveField field) => s_Field = field;

        /// <summary>Remove <paramref name="field"/> if it is the live one.</summary>
        public static void Uninstall(HullWaveField field)
        {
            if (ReferenceEquals(s_Field, field)) s_Field = null;
        }

        /// <summary>
        /// The owner id a hull lays her waves under and reads her ride with, from her root object: the
        /// layer and the reader must pass the SAME object so she never rides her own waves. Never 0
        /// (0 means "nobody"), and 0 for no object.
        /// </summary>
        public static int OwnerOf(Object hull)
        {
            if (hull == null) return 0;
            int id = hull.GetEntityId().GetHashCode();
            return id != 0 ? id : 1;
        }

        /// <summary>The waves' height at a point, metres, leaving out <paramref name="excludeOwner"/>'s
        /// own (0 = none). 0 with no field.</summary>
        public static float HeightAt(Vector2 worldXY, int excludeOwner = 0)
        {
            HullWaveField f = s_Field;
            if (f == null || f.Count == 0) return 0f;
            HullWaveSettings s = GameServices.HullWaves;
            if (!s.Enabled) return 0f;
            return f.SampleHeight(worldXY, s.SpreadMetres, excludeOwner);
        }

        /// <summary>
        /// Drop a small ring of waves at a point now — an oar blade, a splash, something falling in. Its
        /// crests run at the deep-water speed of <paramref name="wavelengthMetres"/> and its band spreads
        /// at half that (the group speed), as a stone's ripples do. Returns whether it was laid.
        /// </summary>
        /// <param name="worldXY">Where, world metres.</param>
        /// <param name="heightMetres">Its crest height at birth, metres.</param>
        /// <param name="wavelengthMetres">Its crest spacing, metres.</param>
        public static bool AddRipple(Vector2 worldXY, float heightMetres, float wavelengthMetres)
            => AddRipple(worldXY, heightMetres, wavelengthMetres, 0);

        /// <summary>
        /// <see cref="AddRipple(Vector2, float, float)"/> laid under <paramref name="owner"/>
        /// (<see cref="OwnerOf"/>), so the hull whose oar made it does not ride her own ring.
        /// </summary>
        public static bool AddRipple(Vector2 worldXY, float heightMetres, float wavelengthMetres, int owner)
        {
            HullWaveField f = s_Field;
            if (f == null || heightMetres <= 0f || wavelengthMetres <= 0f) return false;
            HullWaveSettings s = GameServices.HullWaves;
            if (!s.Enabled) return false;
            double now = GameServices.Clock != null ? GameServices.Clock.FlowSeconds : f.NowSeconds;
            HullWaveSource ripple = MakeRipple(worldXY, heightMetres, wavelengthMetres, now, in s, owner);
            return s.RipplesNeverEvict ? f.AddIfRoom(in ripple) : f.Add(in ripple);
        }

        /// <summary>The source <see cref="AddRipple(Vector2, float, float)"/> lays. Public for tests.</summary>
        public static HullWaveSource MakeRipple(Vector2 worldXY, float heightMetres, float wavelengthMetres,
                                                double now, in HullWaveSettings s)
            => MakeRipple(worldXY, heightMetres, wavelengthMetres, now, in s, 0);

        /// <summary>The same source, laid under <paramref name="owner"/> (0 = nobody's).</summary>
        public static HullWaveSource MakeRipple(Vector2 worldXY, float heightMetres, float wavelengthMetres,
                                                double now, in HullWaveSettings s, int owner)
        {
            float c = HullWaveMath.CrestSpeed(HullWaveMath.PhaseSpeed(wavelengthMetres), wavelengthMetres, in s);
            float life = Mathf.Max(s.RippleLifetimeSeconds, 0.1f);
            return new HullWaveSource
            {
                Position = worldXY,
                BirthSeconds = now,
                Amplitude = heightMetres,
                Wavenumber = 2f * Mathf.PI / wavelengthMetres,
                PhaseSpeed = c,
                RingSpeed = 0.5f * c,
                PacketWidth = HullWaveMath.PacketWidth(wavelengthMetres, in s),
                Lifetime = life,
                DecaySeconds = life * Mathf.Max(s.DecayShare, 0.01f),
                RiseSeconds = life * Mathf.Clamp(s.RippleRiseShare, 0f, 0.5f),
                Owner = owner,
            };
        }
    }
}
