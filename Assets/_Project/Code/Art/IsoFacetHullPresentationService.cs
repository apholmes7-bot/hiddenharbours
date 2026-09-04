using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Art's side of the mesh-hull seam (ADR 0022 phase 4)</b> — the
    /// <see cref="IHullMeshPresentationService"/> that installs an <see cref="IsoFacetHullRenderer"/>
    /// on a host GameObject from a committed <see cref="HullMeshDef"/>. Boats calls it through
    /// <see cref="HullMeshPresentation.Service"/> and never sees a URP type (rule 4).
    ///
    /// <para><b>Self-registering at runtime</b> (<see cref="RuntimeInitializeOnLoadMethod"/>, before
    /// the first scene — the same pattern as the ambient Art hosts), so a player build and PlayMode
    /// tests get the mesh path with no wiring. EditMode tests and editor tooling call
    /// <see cref="EnsureRegistered"/> explicitly; edit-time scene BUILDERS deliberately do not, so a
    /// built scene serialises the sprite rig and the mesh path is chosen live, per run, by the
    /// skinner (builder-generated scenes must not bake a renderer whose setup is runtime-owned).</para>
    /// </summary>
    public sealed class IsoFacetHullPresentationService : IHullMeshPresentationService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAtLoad() => EnsureRegistered();

        /// <summary>Idempotent registration. Never replaces a live service (a test double stays).</summary>
        public static void EnsureRegistered()
        {
            HullMeshPresentation.Service ??= new IsoFacetHullPresentationService();
        }

        /// <inheritdoc/>
        public IHullMeshRenderer Install(GameObject host, HullMeshDef def, HullPaintSchemeDef scheme = null)
        {
            if (host == null) return null;
            if (def == null || !def.IsUsable())
            {
                Debug.LogError($"[IsoFacetHullPresentationService] '{host.name}': hull mesh def " +
                               $"'{(def != null ? def.Id : "<null>")}' is unusable (missing mesh/ramps/" +
                               "bayer or bad cell geometry). No mesh renderer installed — the caller " +
                               "should fall back to the sprite path.");
                return null;
            }

            // A refused repaint costs the paint, never the boat: log it and stand on the def's own
            // ramps, which are a complete, shipped look on their own.
            if (scheme != null && !scheme.IsUsableFor(def))
            {
                Debug.LogError($"[IsoFacetHullPresentationService] '{host.name}': paint scheme refused — " +
                               $"{scheme.ExplainUnusableFor(def)}. Drawing '{def.Id}' in her own colours.");
                scheme = null;
            }

            var renderer = host.GetComponent<IsoFacetHullRenderer>();
            if (renderer == null) renderer = host.AddComponent<IsoFacetHullRenderer>();
            renderer.Configure(ToSetup(def, scheme));
            MakeReflective(renderer);
            MakeChurn(host, def);
            MakeLit(host, def);
            MakeShadowCasting(host);
            return renderer;
        }

        /// <summary>
        /// (ADR 0016, lights PR B) Every mesh hull THROWS a lamp shadow — the owner's 2026-08-05
        /// ruling that boats cast, applied to the fleet the way the reflector and the churn are:
        /// here, where a mesh hull is made, so every hull in every region carries it with no scene
        /// wiring. The caster is a registration and an id hand-off, and costs nothing until a lamp
        /// is in range of her (see <see cref="HullLampShadowCaster"/>).
        /// </summary>
        static void MakeShadowCasting(GameObject host) => HullLampShadowCaster.Fit(host);

        /// <summary>
        /// (ADR 0016) Give this hull the LAMPS her def says she wears.
        ///
        /// <para><b>Here, for the same reason the reflector and the churn are here</b> — this service
        /// is the one place a mesh hull is ever installed, the fleet being built at runtime from Defs
        /// rather than authored into a scene, so "wire it where the thing is made" lands in this
        /// method. Every hull the game builds therefore lights up if her def says she should, in every
        /// region, with no scene wiring and no builder re-run.</para>
        ///
        /// <para><b>Absence is data, and it is checked HERE.</b> A def with no lamps gets no component
        /// at all — not a disabled one, not one holding an empty array — so the overwhelming majority
        /// of the fleet, which has not been measured for lamps yet, costs exactly nothing: no
        /// component, no LateUpdate, no quad. An unlit boat is the shipped picture, not a degraded
        /// one. A hull that ALREADY carries the component keeps it (she may have just been re-skinned
        /// from one lamp-bearing hull to another, and BoatLamps rebuilds off the renderer's own table);
        /// a hull re-skinned to a hull with NO lamps has hers removed, because a lamp table that is
        /// gone must take its lights with it.</para>
        /// </summary>
        static void MakeLit(GameObject host, HullMeshDef def)
        {
            MakeWindowed(host, def);

            bool wantsLamps = def.Lamps != null && def.Lamps.Length > 0;
            var lamps = host.GetComponent<BoatLamps>();

            if (!wantsLamps)
            {
                if (lamps != null) Destroy(lamps);
                return;
            }
            if (lamps == null) host.AddComponent<BoatLamps>();

            MakeSearchlit(host, def);
        }

        /// <summary>
        /// (Owner's ruling, 2026-09-03) Give this hull the WINDOWS her def says her lit room has —
        /// what the cabin glow became when it stopped being a disc over the roof.
        ///
        /// <para><b>Installed BEFORE the lamps, and that ordering is load-bearing.</b>
        /// <see cref="BoatLamps"/> pushes the cabin's state to this component the moment it enables,
        /// and a listener that does not exist yet hears nothing. The window glow also pulls the state
        /// back for itself on enable — belt and braces, because a hull re-skinned in either order
        /// must end up lit the same way — but installing the listener first is what makes the common
        /// path need no second chance.</para>
        ///
        /// <para><b>Absence is data here too.</b> A def with no panes gets no component: the five open
        /// boats in the fleet have no room to light, and that is the answer rather than a gap. A hull
        /// re-skinned onto one that has none has hers removed, for the same reason a lost lamp table
        /// takes its lights with it.</para>
        /// </summary>
        static void MakeWindowed(GameObject host, HullMeshDef def)
        {
            bool wantsWindows = def.Panes != null && def.Panes.Length > 0;
            var windows = host.GetComponent<BoatWindowGlow>();

            if (!wantsWindows)
            {
                if (windows != null) Destroy(windows);
                return;
            }
            if (windows == null) host.AddComponent<BoatWindowGlow>();
        }

        /// <summary>
        /// (ADR 0016) Give this hull the SEARCHLIGHT her def declares — the one lamp that is a beam
        /// rather than a glow, and so is drawn by the bespoke <see cref="BoatSpotlight"/> the ADR
        /// already ships rather than by <see cref="BoatLamps"/>.
        ///
        /// <para><b>Lit by the RULE OF THE ROAD, not by a flag — PR 2's change.</b> A hull whose DEF
        /// carries a searchlight is a working boat being worked by somebody else: her skipper has the
        /// lamp going while he is running, and it is out while she lies at her berth. So the beam is
        /// marked <see cref="BoatSpotlight.MintedFromDef"/> and follows her
        /// <see cref="HiddenHarbours.Core.IVesselWay"/> — which is also what stops seven moored boats
        /// at Nine Mile Creek and a whole review anchorage burning searchlights all night, the thing
        /// this method did before the regime existed.</para>
        ///
        /// <para><b>And she DOES answer the switch once the player is aboard her.</b> The old rule was
        /// "def-minted beams are deaf to the key", because the key read was unconditional and an NPC
        /// boat left listening would flip her beam every time the player reached for their own. That
        /// blunt instrument also meant a hull the player BOUGHT could never work her own searchlight.
        /// The question is now asked properly, of Core's helm slot, every frame
        /// (<see cref="BoatSpotlight.PlayerSwitchesThisBeam"/>): the key reaches the boat the player is
        /// standing on and no other. The player's own dory still gets hers from
        /// <c>PersistentCoreBuilder</c>, off by default and switch-only forever.</para>
        ///
        /// <para><b>What this does to a spotlight that is ALREADY there</b> (the player's, if she ever
        /// steps aboard a hull that declares one): it moves the MOUNT to where this hull wears her
        /// lamp, and nothing else. That much is right — the lamp is a fact of the hull, not of the
        /// person steering — while the switch and the key stay exactly as the player left them.</para>
        ///
        /// <para><b>The one thing PR 2 owes this method:</b> when the player is given a hull that
        /// declares a searchlight, "is this the boat whose wheel the player is holding" becomes a
        /// real question and the honest answer is the Core helm slot, not a per-install policy.
        /// Today no such hull exists, so the policy is exact.</para>
        /// </summary>
        static void MakeSearchlit(GameObject host, HullMeshDef def)
        {
            int at = -1;
            for (int i = 0; i < def.Lamps.Length; i++)
                if (def.Lamps[i].Kind == HullLampKind.Spotlight) { at = i; break; }

            // ⚠️ ON THE BOAT ROOT, NOT ON THE HOST. This service is handed the hull's VISUAL CHILD,
            // and that child is stomped back to world-identity every LateUpdate — it does not turn
            // with her. A searchlight there would aim along a fixed screen axis forever while the boat
            // turned underneath it, which is the exact reason PersistentCoreBuilder puts the player's
            // own beam on her root. The beam reads its carrier's transform for BOTH the heading it
            // throws along and the speed its way-gate dims by, so it has to sit on the thing that
            // actually turns and actually moves.
            Transform rootT = BoatLamps.BoatRootOf(host.transform);
            GameObject root = rootT != null ? rootT.gameObject : host;

            var beam = root.GetComponent<BoatSpotlight>();
            if (at < 0)
            {
                // Re-skinned onto a hull that declares none, so the beam goes with the hull it came
                // with — but ONLY if it is OURS. A spotlight the BUILDER bolted on is the player's own,
                // switched by hand; destroying it because she stepped aboard a hull whose def says
                // nothing about searchlights would take her light away for good, with no way to get it
                // back.
                //
                // ⚠️ The mark used to be "does it answer the toggle key", which stopped being a sound
                // discriminator the moment a boarded hull was allowed to answer her too — every beam
                // in the game is now key-capable and ownership is a question asked of Core. So the
                // mark is now what it always meant: did THIS method make it.
                if (beam != null && beam.MintedFromDef) Destroy(beam);
                return;
            }

            bool minted = beam == null;
            if (minted) beam = root.AddComponent<BoatSpotlight>();

            HullLamp lamp = def.Lamps[at];
            beam.MountOffsetMetres = new Vector2(lamp.RigLocalMetres.x, lamp.RigLocalMetres.y);

            // Only for a beam this method MINTED. A spotlight that was already on the root is the
            // player's own — the builder puts one on the dory, off and on her key — and re-skinning
            // her must not take her switch away, light her up without her asking, or hand her over to
            // a regime that would relight her every time she stepped ashore.
            if (minted)
            {
                beam.MintedFromDef = true;
                // Lit or dark is the REGIME's call from here (BoatSpotlight.Update reads her way): a
                // hull under way burns her searchlight, a hull at her berth does not. Seeding her ON
                // unconditionally is what would light the whole moored fleet for the one frame before
                // the regime's first tick, and a wharf that flashes on and off at wake is worse than
                // one that never lit.
                //
                // Asked HERE rather than through the beam's own cached source: that cache is filled in
                // OnEnable, and EditMode never runs one — a builder or a fixture that installs a hull
                // would seed every beam from an unresolved reference and get "under way" for a boat
                // tied to a wall.
                var way = root.GetComponent<HiddenHarbours.Core.IVesselWay>();
                beam.SetBeam(way == null || way.Way == HiddenHarbours.Core.VesselWay.UnderWay);
            }
        }

        /// <summary>
        /// (ADR 0027 #6) Make this hull CHURN the advected foam buffer.
        ///
        /// <para><b>Why it lives here, beside <see cref="MakeReflective"/>.</b> Same argument, same
        /// place: the fleet is constructed at runtime from Defs rather than authored into a scene, so
        /// "wire it where the thing is made" lands in this service — and #383 shipped the buffer, the
        /// injector, the advect pass and the shader read with <b>nothing attached to anything</b>. No
        /// prefab and no scene carried a <see cref="FoamInjector"/>, so the buffer had no source at
        /// all; it was not merely dialled down, it was unsourced. That is half of why the owner still
        /// could not see foam that behaves like foam (2026-08-05). The other half was
        /// <c>_WakeFoamStrength</c> 0, now dialled in on all nine water materials.</para>
        ///
        /// <para><b>On the HOST, not the overlay</b> (the opposite of the reflector): the injector reads
        /// <c>transform.position</c> as the hull's position on the water and derives speed through the
        /// water from it. The host is the physics root that actually moves; the overlay quad
        /// counter-rotates and is the wrong transform to ask.</para>
        ///
        /// <para><b>The churned band is DATA, not a constant</b> (rule 6): its half-width comes from the
        /// def's <c>WatertightHalfBeamMeters</c> — the hull's own half-beam, already authored per hull
        /// and already the number the injector's tooltip describes ("roughly the hull's beam — a dory
        /// wants ~0.9"; the dory's def says 0.85). So a dory lays a narrow ribbon and a tanker a broad
        /// one, with no per-hull tuning table to keep in sync. A def that never had a half-beam
        /// authored (0) keeps the injector's own serialized default rather than collapsing the band to
        /// nothing.</para>
        ///
        /// <para>Zero-cost-when-idle survives: the injector unregisters the moment the hull is off the
        /// water, and <see cref="IsoFacetHullFeature"/> records no pass when nothing is registered.</para>
        /// </summary>
        static void MakeChurn(GameObject host, HullMeshDef def)
        {
            var injector = host.GetComponent<FoamInjector>();
            if (injector == null) injector = host.AddComponent<FoamInjector>();
            if (def != null && def.WatertightHalfBeamMeters > 0f)
                injector.ConfigureRadius(def.WatertightHalfBeamMeters);
        }

        /// <summary>
        /// (ADR 0027 #8) Make this hull reflect in the water.
        ///
        /// <para><b>On the OVERLAY quad, not the host.</b> The overlay is the renderer that carries
        /// <c>HiddenHarbours/IsoFacetOverlay</c> — the shader with the <c>HHReflect</c> pass — and it
        /// is also the thing whose pixels ARE the hull's visible image (re-composed from the keyline
        /// resolve). The host GameObject has no renderer at all, and the facet mesh child draws only
        /// into the off-screen MRT.</para>
        ///
        /// <para><b>Here rather than in a builder,</b> because this service is the one place a mesh
        /// hull is ever installed — the fleet is constructed at runtime from Defs, not authored into
        /// a scene, so "wire it where the thing is made" lands here. Every hull the game builds
        /// therefore reflects, and the water's own <c>_ObjectReflectStrength</c> is the single dial
        /// that decides whether that shows.</para>
        ///
        /// <para>No pivot override: the overlay quad is built around the hull's PIVOT, which the rig
        /// convention puts at her waterline contact (ADR 0026) — already the axis a reflection wants.</para>
        /// </summary>
        static void MakeReflective(IsoFacetHullRenderer hull)
        {
            MeshRenderer overlay = hull != null ? hull.OverlayRenderer : null;
            if (overlay == null) return;
            var reflector = overlay.GetComponent<ReflectiveObject>();
            if (reflector == null) reflector = overlay.gameObject.AddComponent<ReflectiveObject>();
            reflector.Refresh();
        }

        /// <summary>
        /// The child that carries a hull's heading, rock and heave — asked of the renderer, never
        /// found by name. See <see cref="IsoFacetHullRenderer.PosedMesh"/> for why the name lookup was
        /// a real defect: a hull swap leaves two children called "FacetMesh" alive for one frame, and
        /// <c>Find</c> returns the one that is about to be destroyed.
        /// </summary>
        static Transform PosedMeshOf(IsoFacetHullRenderer hull) => hull != null ? hull.PosedMesh : null;

        /// <inheritdoc/>
        public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot)
        {
            if (host == null) return null;
            if (def == null || !def.IsUsable())
            {
                Debug.LogError($"[IsoFacetHullPresentationService] '{host.name}': fitting def " +
                               $"'{(def != null ? def.Id : "<null>")}' is unusable. Not attached — " +
                               "the caller should keep the sprite path, where the fitting exists.");
                return null;
            }

            var hull = host.GetComponent<IsoFacetHullRenderer>();
            if (hull == null)
            {
                Debug.LogError($"[IsoFacetHullPresentationService] '{host.name}': no mesh hull is " +
                               $"installed, so fitting '{def.Id}' has nothing to bolt to. Install the " +
                               "hull first — a fitting posed against no hull would sit at the world " +
                               "origin rocking on its own.");
                return null;
            }

            // ⚠️ PARENT TO THE POSED CHILD, not to the host. The host does not turn; "FacetMesh"
            // carries heading, rock and heave, and a fitting must inherit all three or it shears off
            // the boat the instant she moves — which is exactly the failure the sprite path's layers
            // had whenever a rock frame and a steer column disagreed.
            Transform posed = PosedMeshOf(hull);
            if (posed == null)
            {
                Debug.LogError($"[IsoFacetHullPresentationService] '{host.name}': the hull renderer has " +
                               $"no posed mesh child to hang '{def.Id}' from. The renderer was never " +
                               "configured, or its child layout changed and this attach point must be " +
                               "re-aimed.");
                return null;
            }

            Transform existing = posed.Find(slot);
            var prop = existing != null
                ? existing.GetComponent<IsoFacetPropRenderer>()
                : null;
            if (prop == null)
            {
                var go = new GameObject(slot) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(posed, false);
                prop = go.AddComponent<IsoFacetPropRenderer>();
            }
            prop.Configure(ToPropSetup(def));
            return prop;
        }

        /// <inheritdoc/>
        public void DetachProps(GameObject host)
        {
            if (host == null) return;
            var hull = host.GetComponent<IsoFacetHullRenderer>();
            Transform posed = PosedMeshOf(hull);
            if (posed == null) return;

            for (int i = posed.childCount - 1; i >= 0; i--)
            {
                var prop = posed.GetChild(i).GetComponent<IsoFacetPropRenderer>();
                if (prop != null) Destroy(prop.gameObject);
            }
        }

        /// <inheritdoc/>
        public void DetachProp(GameObject host, string slot)
        {
            if (host == null || string.IsNullOrEmpty(slot)) return;
            var hull = host.GetComponent<IsoFacetHullRenderer>();
            Transform posed = PosedMeshOf(hull);
            Transform existing = posed != null ? posed.Find(slot) : null;
            if (existing != null && existing.GetComponent<IsoFacetPropRenderer>() != null)
                Destroy(existing.gameObject);
        }

        /// <summary>The fitting def, converted to the renderer's runtime setup — plain copies.</summary>
        public static IsoFacetPropSetup ToPropSetup(HullPropMeshDef def)
        {
            var ramps = new Color32[def.Ramps.Length][];
            var offsets = new int[def.Ramps.Length];
            for (int m = 0; m < def.Ramps.Length; m++)
            {
                ramps[m] = def.Ramps[m].Colors;
                offsets[m] = def.Ramps[m].Offset;
            }

            return new IsoFacetPropSetup
            {
                Mesh = def.Mesh,
                FixedMesh = def.FixedMesh,
                Ramps = ramps,
                RampOffsets = offsets,
                LightN = def.LightN,
                Gain = def.Gain,
                Bias = def.Bias,
                Bayer16 = def.Bayer16,
                Keyline = def.Keyline,
                PivotPx = def.PivotPx,
                PxPerMetre = def.PxPerMetre,
                CellW = def.CellW,
                CellH = def.CellH,
                ElevationDeg = def.ElevationDeg,
                PivotLocalMeters = def.PivotLocalMeters,
            };
        }

        /// <inheritdoc/>
        public void Remove(GameObject host)
        {
            if (host == null) return;
            DetachProps(host);
            var renderer = host.GetComponent<IsoFacetHullRenderer>();
            if (renderer != null) Destroy(renderer);
            // The renderer adds a SortingGroup for the sprite-sorting workaround; a host going back
            // to the sprite path must not keep sorting as a group.
            var group = host.GetComponent<SortingGroup>();
            if (group != null) Destroy(group);
            // And the lamp-shadow caster goes with the renderer it casts from (ADR 0016, lights PR B):
            // a sprite-path host has no id block in the screen texture to cast with.
            var caster = host.GetComponent<HullLampShadowCaster>();
            if (caster != null) Destroy(caster);
        }

        /// <summary>
        /// The def, converted to the renderer's runtime setup — plain copies, no rescaling.
        ///
        /// <para><paramref name="scheme"/>, when given and usable, supplies the ramp table INSTEAD of
        /// the def's. That is the entire repaint: everything else on the setup — mesh, light, gain,
        /// bias, dither, keyline, cell — comes from the def either way, because a scheme is a colour
        /// and nothing else. A null or unusable scheme yields a setup byte-identical to the
        /// one-argument call, which is the A/B contract <c>HullPaintSchemeApplicationTests</c> pins.</para>
        /// </summary>
        public static IsoFacetHullSetup ToSetup(HullMeshDef def, HullPaintSchemeDef scheme = null)
        {
            var table = scheme != null && scheme.IsUsableFor(def) ? scheme.Ramps : def.Ramps;

            var ramps = new Color32[table.Length][];
            var offsets = new int[table.Length];
            for (int m = 0; m < table.Length; m++)
            {
                ramps[m] = table[m].Colors;
                offsets[m] = table[m].Offset;
            }

            // THE INTERIOR PALETTE IS NOT REPAINTED BY AN EXTERIOR SCHEME. A paint scheme is a
            // hull colour job (HullPaintSchemeDef.Ramps is indexed against def.Ramps); it says
            // nothing about the cabin liner, and swapping the room's colours when the owner picks a
            // new topside would be a change nobody asked for. Read from the def either way.
            var interiorTable = def.InteriorRamps ?? System.Array.Empty<HullMeshDef.Ramp>();
            var interiorRamps = new Color32[interiorTable.Length][];
            var interiorOffsets = new int[interiorTable.Length];
            for (int m = 0; m < interiorTable.Length; m++)
            {
                interiorRamps[m] = interiorTable[m].Colors;
                interiorOffsets[m] = interiorTable[m].Offset;
            }

            return new IsoFacetHullSetup
            {
                Mesh = def.Mesh,
                Ramps = ramps,
                RampOffsets = offsets,
                InteriorRamps = interiorRamps,
                InteriorRampOffsets = interiorOffsets,
                LightN = def.LightN,
                Gain = def.Gain,
                Bias = def.Bias,
                Bayer16 = def.Bayer16,
                Keyline = def.Keyline,
                PivotPx = def.PivotPx,
                PxPerMetre = def.PxPerMetre,
                CellW = def.CellW,
                CellH = def.CellH,
                ElevationDeg = def.ElevationDeg,
                WatertightDeckHeightMeters = def.WatertightDeckHeightMeters,
                WatertightHalfBeamMeters = def.WatertightHalfBeamMeters,
                Lamps = def.Lamps,
                Panes = def.Panes,
            };
        }

        // Editor-safe destroy: the A/B toggle runs in play mode, but tests and tooling call Remove
        // outside it, where Object.Destroy throws.
        static void Destroy(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
