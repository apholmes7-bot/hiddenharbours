using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The ONE place a boat gets its directional skin.</b> Installs (or re-installs) the rig described
    /// by a <see cref="BoatVisualDef"/> onto a boat root: a screen-aligned visual child that snaps through
    /// the hull compass, the wave-coupled rock grid, and the baked per-side oar overlays.
    ///
    /// <para>Three call sites used to re-implement this rig by hand — the player-boat builder (with its
    /// art paths as <c>const string</c>), the ambient fleet presenter (whose comment read "the player's
    /// rig, verbatim"), and the rotation-test harness. They now converge here, so the rig can only be
    /// wrong in one place, and a hull's look is chosen by DATA (which <see cref="BoatVisualDef"/> the
    /// <see cref="BoatHullDef"/> points at), not by a compile-time flag.</para>
    ///
    /// <para><b>Runtime-callable on purpose.</b> This is not editor-only: <see cref="OwnedFleet"/> calls
    /// <see cref="ApplyHull"/> when a purchase or a save-restore swaps the hull, which is what closes the
    /// long-standing swap gap (the fleet used to write <c>.sprite</c> onto the base renderer that the skin
    /// had disabled, so buying a boat changed its feel, hold and camera while the picture stayed the
    /// dory). Uses no editor API and allocates only on an actual re-skin.</para>
    ///
    /// <para><b>Idempotent.</b> Applying twice reuses the existing child and components rather than
    /// stacking a second rig, so a re-skin is a swap, not a pile.</para>
    ///
    /// <para><b>Invariants it upholds</b> — break these and the boat breaks:
    /// bow = <c>transform.up</c>; the <see cref="DirectionalBoatSprite"/> lives on the PHYSICS ROOT and
    /// STOMPS the visual child's world rotation to identity every LateUpdate, so anything additive must
    /// route through <see cref="DirectionalBoatSprite.VisualTiltDegrees"/> and anything that must follow
    /// the bow must ride the ROOT (this once ate the boat spotlight). The visual child keeps the name
    /// <see cref="VisualChildName"/> because <c>BoatSpotlight</c> finds it BY NAME to read its rock
    /// without referencing the Boats module (rule 4) — renaming it silently kills the beam bounce.</para>
    /// </summary>
    public static class BoatHullSkinner
    {
        /// <summary>
        /// Name of the visual child the skin builds under the boat root. <b>Load-bearing:</b>
        /// <c>HiddenHarbours.Art.BoatSpotlight</c> looks this child up BY NAME to read the hull's wave
        /// rock for its beam bounce (Art must not reference Boats — rule 4), and the EditMode builder
        /// tests assert on it. Historic name from the #93/#94 pass when the skin was a fishing boat; kept
        /// deliberately, because renaming it buys nothing and breaks the spotlight silently.
        /// </summary>
        public const string VisualChildName = "FishingBoatVisual";

        /// <summary>Names of the two oar overlay children, layered over the hull picture.</summary>
        public const string PortOarChildName = "OarPort";
        /// <summary>Names of the two oar overlay children, layered over the hull picture.</summary>
        public const string StarOarChildName = "OarStar";

        /// <summary>Engine A's lower layer (leg + plate + skeg + prop). A = the PORT engine of a twin fit,
        /// or the only centreline engine of a single fit.</summary>
        public const string LowerMotorAChildName = "MotorLowerA";
        /// <summary>Engine A's upper layer (clamp bracket + cowl).</summary>
        public const string UpperMotorAChildName = "MotorUpperA";
        /// <summary>Engine B's lower layer — the STARBOARD engine. Twin fit only.</summary>
        public const string LowerMotorBChildName = "MotorLowerB";
        /// <summary>Engine B's upper layer. Twin fit only.</summary>
        public const string UpperMotorBChildName = "MotorUpperB";

        /// <summary>Optional knobs a caller varies. Defaults are the player boat's rig, exactly.</summary>
        public struct Options
        {
            /// <summary>Name of the visual child. Null/empty = <see cref="VisualChildName"/>, which is what
            /// the PLAYER's boat must use (BoatSpotlight finds it by name). Decor rigs that no one looks up
            /// — the ambient fleet's "Visual" — pass their own historic name so converging on this installer
            /// doesn't silently rename their children.</summary>
            public string ChildName;

            /// <summary>Multiply-tint written ONCE onto the hull picture at install (rule 7 — no per-frame
            /// colour churn). The ambient fleet paints each fisher's identity colour through this; the
            /// player's boat leaves it clear. Default (a == 0) = untinted white.</summary>
            public Color Tint;

            /// <summary>Install <see cref="BoatWaveMotion"/> so the hull rides the shared wave field
            /// (ADR 0018 B2). Default (false) installs it — the field is inverted so <c>default</c> is the
            /// shipped rig.</summary>
            public bool SkipWaveMotion;

            /// <summary>Skip the oar overlays even when the visual binds them. Default (false) draws them
            /// whenever <see cref="BoatVisualDef.HasOarSheets"/> and a controller are both present.</summary>
            public bool SkipOars;
        }

        /// <summary>
        /// What the skin built, handed back so a caller can layer onto it without re-deriving the rig by
        /// name or by <c>GetComponentInChildren</c>. <b>This is the seam a new overlay binds to</b> — the
        /// coming motor layer takes <see cref="Visual"/> as its parent, <see cref="Renderer"/> for the
        /// sorting layer + base order, and <see cref="Directional"/> for the drawn heading.
        /// </summary>
        public struct Rig
        {
            /// <summary>True when a directional skin is installed. False = the plain rotating hull stands.</summary>
            public bool Skinned;
            /// <summary>The screen-aligned visual child (<see cref="VisualChildName"/>). Overlays parent here.</summary>
            public Transform Visual;
            /// <summary>The hull picture's renderer. Overlays copy its sorting layer and take orders above it.</summary>
            public SpriteRenderer Renderer;
            /// <summary>The compass component on the ROOT. Read <c>DrawnHeadingDegrees()</c>; write additive
            /// rotation ONLY through <c>VisualTiltDegrees</c>.</summary>
            public DirectionalBoatSprite Directional;
            /// <summary>The wave rider, unless the caller skipped it.</summary>
            public BoatWaveMotion Wave;
            /// <summary>The oar overlay, when this visual binds oar sheets and a controller was supplied.</summary>
            public DoryOarLayer Oars;
            /// <summary>The outboard overlay, when this visual binds a complete pair of motor sheets.</summary>
            public OutboardMotorLayer Motor;
        }

        /// <summary>
        /// Skin a boat root for a HULL: the data-driven entry point. Chooses the directional skin when the
        /// hull's <see cref="BoatHullDef.Visual"/> binds a full compass, and otherwise falls back to the
        /// plain rotating <see cref="BoatHullDef.Sprite"/> on <paramref name="baseRenderer"/> — the
        /// pre-skin behaviour, preserved so hulls with no facings (the Punt, the FishingSkiff) are never
        /// stranded picture-less.
        ///
        /// <para>Because it handles BOTH directions, this is safe to call on every hull swap: skinned →
        /// unskinned removes the compass child and brings the base renderer back with the new hull's
        /// sprite; unskinned → skinned hides the base renderer and installs the compass.</para>
        /// </summary>
        /// <param name="root">The boat's PHYSICS root (the Rigidbody2D that turns with the helm).</param>
        /// <param name="baseRenderer">The root's own hull renderer — the plain rotating picture. Disabled
        /// while a directional skin is worn, but its sprite ref is left intact.</param>
        /// <param name="hull">The hull being worn. Null = no-op.</param>
        /// <param name="boat">The controller the oar overlay animates from. Null = no oars.</param>
        public static Rig ApplyHull(GameObject root, SpriteRenderer baseRenderer, BoatHullDef hull,
                                    BoatController boat, Options options = default)
        {
            if (root == null || hull == null) return default;

            var visual = hull.Visual;
            if (visual == null || !visual.HasFullCompass())
            {
                // No skin for this hull → the plain rotating hull picture, exactly as before skins existed.
                RemoveSkin(root);
                if (baseRenderer != null)
                {
                    baseRenderer.enabled = true;
                    if (hull.Sprite != null) baseRenderer.sprite = hull.Sprite;   // null-safe: keep what's there
                }
                return default;
            }

            // Hide the plain hull PICTURE only — never clear its sprite ref, so falling back to it later
            // (an unskinned hull) still has something to draw.
            if (baseRenderer != null) baseRenderer.enabled = false;
            return Apply(root, visual, boat, options);
        }

        /// <summary>
        /// Install a skin from a <see cref="BoatVisualDef"/> directly, for callers that carry facings as
        /// their own data rather than as a hull (the ambient fleet, the rotation-test harness). Prefer
        /// <see cref="ApplyHull"/> when a <see cref="BoatHullDef"/> is in hand — it also handles the
        /// unskinned fallback.
        /// </summary>
        public static Rig Apply(GameObject root, BoatVisualDef visual, BoatController boat,
                                Options options = default)
        {
            if (root == null || visual == null || !visual.HasFullCompass()) return default;

            // (1) The visual CHILD: screen-aligned, carries the hull picture. Reused if a skin is already
            // worn, so a re-skin swaps the art instead of stacking a second rig.
            string childName = string.IsNullOrEmpty(options.ChildName) ? VisualChildName : options.ChildName;
            var child = root.transform.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(root.transform, false);
                child = go.transform;
            }
            child.gameObject.SetActive(true);
            child.localPosition = Vector3.zero;

            var sr = child.GetComponent<SpriteRenderer>();
            if (sr == null) sr = child.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = visual.Facings[0];          // North until the first LateUpdate snaps to the heading
            sr.sortingOrder = visual.SortingOrder;
            // ONE colour write, at install time (rule 7). Multiply-tint shifts the WHOLE sprite, which is
            // why the ambient fleet's default strength is subtle. A default (zero-alpha) Tint = untinted.
            sr.color = options.Tint.a > 0f ? options.Tint : Color.white;

            // (2) The compass, on the ROOT (it counter-rotates the child back to screen-identity).
            var directional = root.GetComponent<DirectionalBoatSprite>();
            if (directional == null) directional = root.AddComponent<DirectionalBoatSprite>();
            directional.Configure(
                visual.Facings, sr,
                zeroHeadingDegrees: visual.ZeroHeadingDegrees,   // element 0 is the North-facing sprite
                smoothModeSprite: visual.Facings[0],             // unused in Snap; same art if toggled
                mode: DirectionalBoatSprite.RotationMode.SnapDirectional,
                // Per-ARTWORK, never global: the iso sheets are baked CCW; the FishingBoat compass is CW.
                facingsAreCounterClockwise: visual.FacingsAreCounterClockwise);

            // (3) The rock grid: DirectionalBoatSprite draws rockGrid[heading·frames + RockFrame] instead
            // of the static facing, and BoatWaveMotion sets RockFrame from the wave phase under the hull —
            // so the rock is DRAWN from the real sea, not faked on the transform. Clearing it (a visual
            // with no grid) restores the static compass + the legacy transform rock.
            if (visual.HasRockGrid()) directional.ConfigureRock(visual.RockGrid, visual.RockFrameCount);
            else directional.ConfigureRock(null, 1);

            // (4) The hull rides the shared wave field (ADR 0018 B2) — VISUAL only: roll/pitch/bob go to
            // the CHILD, and the roll routes through DirectionalBoatSprite.VisualTiltDegrees because that
            // component stomps the child's world rotation every LateUpdate (a direct write is eaten).
            BoatWaveMotion wave = null;
            if (!options.SkipWaveMotion)
            {
                wave = root.GetComponent<BoatWaveMotion>();
                if (wave == null) wave = root.AddComponent<BoatWaveMotion>();
                wave.Configure(child, directional);
            }

            // (5) The oars: baked per-side overlays, animated from the boat's REAL per-oar state.
            var oars = (!options.SkipOars && boat != null && visual.HasOarSheets())
                ? WireOars(root, child, sr, visual, boat, directional)
                : RemoveOars(root, child);

            // (6) The outboard: the skiffs' remote-steer engine, swivelled from the boat's REAL helm. Same
            // shape as the oars — a complete pair of sheets, or nothing. Note this needs no controller to
            // INSTALL (an unmanned skiff still draws its engine, dead ahead); the layer's own manned-helm
            // gate handles the driving. Mutually exclusive with the oars: their sorting bands overlap, so a
            // visual binding both would z-fight — we drop the MOTOR and shout, because the oars are the
            // older, load-bearing rig and a rowboat that grew an engine is the authoring mistake.
            OutboardMotorLayer motor;
            if (visual.HasMotor() && visual.HasConflictingOverlays())
            {
                Debug.LogError($"[BoatHullSkinner] Visual '{visual.Id}' binds BOTH oar sheets and motor " +
                               "sheets. Their sorting bands overlap (oars hull+1/+2 vs the motor's lower " +
                               "layer hull+1/+2 when it draws over the hull), so the engine leg and the " +
                               "port oar would fight for the same order. The MOTOR is dropped. Author the " +
                               "hull as rowed OR powered, not both.");
                motor = RemoveMotor(root, child);
            }
            else
            {
                motor = visual.HasMotor()
                    ? WireMotor(root, child, sr, visual, boat, directional)
                    : RemoveMotor(root, child);
            }

            return new Rig
            {
                Skinned = true, Visual = child, Renderer = sr,
                Directional = directional, Wave = wave, Oars = oars, Motor = motor,
            };
        }

        /// <summary>
        /// Strip a directional skin off a boat root (the skinned → unskinned half of a hull swap). Leaves
        /// the base hull renderer to the caller. Safe on a root that was never skinned.
        /// </summary>
        public static void RemoveSkin(GameObject root)
        {
            if (root == null) return;

            var directional = root.GetComponent<DirectionalBoatSprite>();
            if (directional != null) DestroyComponent(directional);

            // BoatWaveMotion idles harmlessly with a null visual, but re-point it anyway so it can never
            // drive a destroyed transform.
            var wave = root.GetComponent<BoatWaveMotion>();
            if (wave != null) wave.Configure(null, null);

            var oars = root.GetComponent<DoryOarLayer>();
            if (oars != null) DestroyComponent(oars);

            var motor = root.GetComponent<OutboardMotorLayer>();
            if (motor != null) DestroyComponent(motor);

            // Destroying the visual child takes every overlay's renderer with it, so the layers above only
            // need their COMPONENTS stripped off the root — they'd otherwise idle pointing at dead renderers.
            var child = root.transform.Find(VisualChildName);
            if (child != null) DestroyObject(child.gameObject);
        }

        // ---- oars --------------------------------------------------------------------------------

        // Both oar renderers are CHILDREN of the hull's visual child, so they inherit the exact snap /
        // counter-rotation treatment the hull gets (they must never smooth-rotate while the hull snaps) and
        // register pixel-perfect on it — the sheets share the hull's cell + waterline pivot, so
        // localPosition is zero. Draw order follows the art README (hull → port oar → star oar) on the
        // hull's own sorting layer, so the oars always draw ON the hull and never against each other.
        private static DoryOarLayer WireOars(GameObject root, Transform visual, SpriteRenderer hullVisual,
                                             BoatVisualDef def, BoatController boat,
                                             DirectionalBoatSprite directional)
        {
            var portSr = MakeOarRenderer(visual, PortOarChildName, hullVisual,
                                         def.OarPort[DoryOarMath.RestingColumn], hullVisual.sortingOrder + 1);
            var starSr = MakeOarRenderer(visual, StarOarChildName, hullVisual,
                                         def.OarStar[DoryOarMath.RestingColumn], hullVisual.sortingOrder + 2);

            var layer = root.GetComponent<DoryOarLayer>();
            if (layer == null) layer = root.AddComponent<DoryOarLayer>();
            layer.Configure(def.OarPort, def.OarStar, portSr, starSr, boat, directional,
                            def.HeadingCount, def.OarColumnCount);
            return layer;
        }

        private static SpriteRenderer MakeOarRenderer(Transform visual, string name, SpriteRenderer hullVisual,
                                                      Sprite first, int sortingOrder)
        {
            var existing = visual.Find(name);
            var go = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null) go.transform.SetParent(visual, false);
            go.SetActive(true);
            go.transform.localPosition = Vector3.zero;    // shared pivot ⇒ pixel-perfect registration

            var r = go.GetComponent<SpriteRenderer>();
            if (r == null) r = go.AddComponent<SpriteRenderer>();
            r.sprite = first;
            r.sortingLayerID = hullVisual.sortingLayerID; // same layer as the hull — only the order differs
            r.sortingOrder = sortingOrder;
            return r;
        }

        // A hull whose visual binds no oar sheets must not keep the previous hull's oars floating over it.
        private static DoryOarLayer RemoveOars(GameObject root, Transform visual)
        {
            var layer = root.GetComponent<DoryOarLayer>();
            if (layer != null) DestroyComponent(layer);
            if (visual != null)
            {
                var port = visual.Find(PortOarChildName);
                if (port != null) DestroyObject(port.gameObject);
                var star = visual.Find(StarOarChildName);
                if (star != null) DestroyObject(star.gameObject);
            }
            return null;
        }

        // ---- the outboard ------------------------------------------------------------------------

        // The four (or two) motor renderers are CHILDREN of the hull's visual child, exactly like the oars
        // and for exactly the same two reasons: they inherit the hull's snap / counter-rotation treatment
        // for free (DirectionalBoatSprite stomps that child's world rotation to screen-identity every
        // LateUpdate, so a sibling would have to re-derive it), and the motor cell shares the hull's pivot
        // (motor 272×216 pivot (136,120) vs hull 244×216 pivot (122,120) — both normalise to the same
        // origin), so localPosition zero registers pixel-perfect. Pin by PIVOT, never by corners.
        //
        // Sorting is NOT set here: OutboardMotorLayer re-decides each renderer's order every frame, because
        // the lower layer's band FLIPS under the hull across the stern-away headings (SE/S/SW). Handing it
        // a static order would be a lie that only shows up when the owner turns the boat.
        private static OutboardMotorLayer WireMotor(GameObject root, Transform visual, SpriteRenderer hullVisual,
                                                 BoatVisualDef def, BoatController boat,
                                                 DirectionalBoatSprite directional)
        {
            bool twin = def.MotorFit == OutboardMotorLayer.MotorFit.Twin;
            int center = OutboardMotorMath.CenterColumn(def.MotorColumnCount);   // wake dead ahead, never hard-over

            var lowerA = MakeMotorRenderer(visual, LowerMotorAChildName, hullVisual, def.MotorLower[center]);
            var upperA = MakeMotorRenderer(visual, UpperMotorAChildName, hullVisual, def.MotorUpper[center]);
            var lowerB = twin ? MakeMotorRenderer(visual, LowerMotorBChildName, hullVisual, def.MotorLower[center]) : null;
            var upperB = twin ? MakeMotorRenderer(visual, UpperMotorBChildName, hullVisual, def.MotorUpper[center]) : null;

            // A Single fit must not keep a previous TWIN hull's second engine hanging off the transom.
            if (!twin) RemoveMotorEngineB(visual);

            var layer = root.GetComponent<OutboardMotorLayer>();
            if (layer == null) layer = root.AddComponent<OutboardMotorLayer>();
            layer.Configure(def.MotorLower, def.MotorUpper, lowerA, upperA, lowerB, upperB,
                            boat, directional, hullVisual, def.MotorVariant, def.MotorFit,
                            def.HeadingCount, def.MotorColumnCount, def.MotorMaxSteerDegrees);
            layer.ConfigureRock(def.MotorRockRollDegrees, def.MotorRockPitchOffsetMeters,
                                def.MotorRockHeavePixels, def.RockFrameCount);
            return layer;
        }

        private static SpriteRenderer MakeMotorRenderer(Transform visual, string name, SpriteRenderer hullVisual,
                                                        Sprite first)
        {
            var existing = visual.Find(name);
            var go = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null) go.transform.SetParent(visual, false);
            go.SetActive(true);
            go.transform.localPosition = Vector3.zero;    // shared pivot ⇒ pixel-perfect registration

            var r = go.GetComponent<SpriteRenderer>();
            if (r == null) r = go.AddComponent<SpriteRenderer>();
            r.sprite = first;
            r.sortingLayerID = hullVisual.sortingLayerID; // same layer as the hull — the layer drives the order
            return r;
        }

        // A hull whose visual binds no motor must not keep the previous hull's engine bolted to its transom.
        private static OutboardMotorLayer RemoveMotor(GameObject root, Transform visual)
        {
            var layer = root.GetComponent<OutboardMotorLayer>();
            if (layer != null) DestroyComponent(layer);
            if (visual != null)
            {
                DestroyChild(visual, LowerMotorAChildName);
                DestroyChild(visual, UpperMotorAChildName);
                RemoveMotorEngineB(visual);
            }
            return null;
        }

        private static void RemoveMotorEngineB(Transform visual)
        {
            DestroyChild(visual, LowerMotorBChildName);
            DestroyChild(visual, UpperMotorBChildName);
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) DestroyObject(t.gameObject);
        }

        // ---- teardown ----------------------------------------------------------------------------

        // Destroy is deferred to end-of-frame in play mode but immediate in the editor; EditMode tests and
        // the scene builders run outside play mode, where Object.Destroy throws. One helper so every
        // teardown path picks the right one and a re-skin is not left half-torn-down.
        private static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        private static void DestroyComponent(Component c)
        {
            if (Application.isPlaying) Object.Destroy(c);
            else Object.DestroyImmediate(c);
        }
    }
}
