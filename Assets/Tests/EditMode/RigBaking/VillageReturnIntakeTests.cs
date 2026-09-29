using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The intake guard for Claude Design's village return (drop 14, 2026-09-26), landed as delivered in
    /// <c>docs/art/rigs/village-return/</c> BESIDE today's rigs.
    ///
    /// <para>Phase A staged the returned house and room beside today's; Phase B (#898) switched the
    /// catalog to them in the same commit as the re-bake (RigCatalog.CoastalHeritage.cs says why). These
    /// tests measure what the switch rests on — the companion's and the manors' load order, the storey
    /// reader and the rises the contract now holds, the room-under-shell registration at offset 0, the
    /// classic look against main's own rigs (kept in <c>docs/art/rigs/</c> for exactly this), the
    /// building probe's reading of the general store's eave door, and the landed bytes. Like
    /// <see cref="InteriorRigBakeTests"/>, they need no graphics device, only ClearScript, so CI runs the
    /// lot.</para>
    /// </summary>
    public class VillageReturnIntakeTests
    {
        const string Folder = "docs/art/rigs/village-return";
        const string Art = Folder + "/houses-kit/Art";
        const string House = "HouseIso";
        const string Interior = "InteriorIso";
        const int Shown = 20;

        /// <summary>
        /// Today's house before the switch: main's <c>houseIsoRig.js</c> after the lifecycle pass, as
        /// <c>house</c> named it until #898. It stays in <c>docs/art/rigs/</c> as the classic pin's
        /// reference.
        /// </summary>
        static readonly RigEntry TodaysHouse = new RigEntry(
            "docs/art/rigs/houseIsoRig.js", House, AzimuthConvention.CounterClockwise,
            new[] { "buildingLifecycle" });

        /// <summary>Today's room before the switch: main's <c>interiorIsoRig.js</c>, no prerequisites.</summary>
        static readonly RigEntry TodaysInterior = new RigEntry(
            "docs/art/rigs/interiorIsoRig.js", Interior, AzimuthConvention.CounterClockwise);

        /// <summary>
        /// Floor-to-floor rise of the four shipped rooms on the returned rig, in metres — measured at
        /// intake on Node and V8, identical with the companion on, off and not loaded. The contract held
        /// 3.1–3.5 m for the same rooms before the switch, read off the old rig's anchor; these are what
        /// the re-bake wrote.
        /// </summary>
        static readonly (string Key, double Rise)[] ReturnedRises =
        {
            ("sageCottage", 2.65), ("school", 2.59), ("redSaltbox", 2.74), ("whiteFarmhouse", 2.92),
        };

        static IRigScriptHost ReturnedHost()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            try
            {
                RigCatalog.Install(host, RigCatalog.Get("house"));
                RigCatalog.Install(host, RigCatalog.Get("interior"));
                return host;
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        static IRigScriptHost TodaysHost()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            try
            {
                RigCatalog.Install(host, TodaysHouse);
                RigCatalog.Install(host, TodaysInterior);
                return host;
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        static InteriorKit.Build Room(string key)
        {
            foreach (InteriorKit.Build b in InteriorKit.RoomSet)
                if (b.Key == key) return b;
            throw new AssertionException($"InteriorKit.RoomSet has no room '{key}'.");
        }

        // =============================================================================
        //  the catalog: what loads before what
        // =============================================================================

        [Test]
        public void TheCompanionLoadsBehindThePropRigAndNeverBehindTheHouse()
        {
            RigEntry pass = RigCatalog.Get("coastalPass");
            Assert.AreEqual(Art + "/coastalPass.js", pass.ScriptPath);
            CollectionAssert.AreEqual(new[] { "interiorProp" }, pass.Prerequisites.ToArray(),
                "the companion's one hard dependency is PropIso — its prop emitter throws by name without " +
                "it. The house it also reads is reached only while dressing a house, whose rig names the " +
                "companion; declaring \"house\" here would close house → coastalPass → house, a cycle " +
                "InstallPrerequisites does not guard.");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, pass);
            Assert.IsTrue(host.EvaluateBool("typeof PropIso === 'object' && PropIso !== null"),
                          "installing the companion installs the prop rig ahead of it");
            Assert.IsFalse(host.EvaluateBool("typeof HouseIso !== 'undefined'"),
                           "and nothing pulls a house in behind it");
            Assert.AreEqual("3.2.0", host.EvaluateString("CoastalPass.version"),
                            "the companion this intake measured. A new one re-opens the measurements in " +
                            "RigCatalog.CoastalHeritage.cs — the rises, the classic pin, the light.");
            Assert.IsTrue(host.EvaluateBool("typeof CoastalPass.light === 'object' && CoastalPass.light !== null"),
                          "the companion is also the shared light engine (CoastalPass.light)");
        }

        [Test]
        public void TheManorShellIsDefinedBeforeTheManorUnit()
        {
            RigEntry unit = RigCatalog.Get("manorUnitIso");
            Assert.AreEqual("manorIso", unit.Prerequisites[0],
                            "InstallPrerequisites is depth first AND in order, so naming the shell first is " +
                            "what defines ManorIso before the unit rig's source runs");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, unit);
            string order = host.EvaluateString(
                "Object.keys(globalThis).filter(k => ['PropIso','CoastalPass','ManorIso','ManorUnitIso']" +
                ".indexOf(k) >= 0).join(' ')");
            Assert.AreEqual("PropIso CoastalPass ManorIso ManorUnitIso", order,
                            "the order the globals were defined in. ManorUnitIso reads root.ManorIso in five " +
                            "places and resolves a missing one to a plausible fallback rather than throwing.");
        }

        // =============================================================================
        //  the storey reader (#853's fix, carried)
        // =============================================================================

        [Test]
        public void TheStoreyReaderTakesTheReturnedRoomsRiseAsADifference()
        {
            using IRigScriptHost host = ReturnedHost();
            var failures = new List<string>();
            foreach ((string key, double rise) in ReturnedRises)
            {
                string opts = InteriorBakeMenu.OptionsLiteralFor(Room(key));
                double read = InteriorRigBaker.StoreyRiseMetres(host, Interior, opts);
                double anchor = host.EvaluateNumber($"{Interior}.anchors(0,{opts}).storeyZ");
                if (Math.Abs(read - rise) > 1e-6)
                    failures.Add($"{key}: the reader gives {read:F4} m, the rig's floors are {rise:F2} m apart");
                if (Math.Abs(anchor - rise) < 0.5)
                    failures.Add($"{key}: anchors().storeyZ is {anchor:F4} m, close to the rise — the " +
                                 "difference no longer distinguishes the two readings");
            }
            AssertNone(failures,
                "the returned room reports storeyZ as the floor's height above GRADE (0.55 m on every " +
                "room), so the rise is dims({storey:'upper'}).storeyZ − dims().storeyZ, never the anchor");
        }

        [Test]
        public void TheContractHoldsTheReturnedRises_AsTheReaderTakesThem()
        {
            InteriorKit.Contract contract = InteriorKit.Load();
            Assert.IsNotNull(contract, $"no contract at {InteriorKit.ContractPath}");

            using IRigScriptHost host = ReturnedHost();
            var failures = new List<string>();
            foreach ((string key, double rise) in ReturnedRises)
            {
                InteriorKit.Entry entry = contract.rooms?.FirstOrDefault(e => e.key == key);
                if (entry == null)
                {
                    failures.Add($"{key}: not in the contract");
                    continue;
                }
                double read = InteriorRigBaker.StoreyRiseMetres(host, Interior, InteriorBakeMenu.OptionsLiteralFor(Room(key)));
                if (Math.Abs((float)read - entry.storeyHeightMetres) > 1e-6f || Math.Abs(read - rise) > 1e-6)
                    failures.Add($"{key}: the reader gives {read:F4} m, the contract holds " +
                                 $"{entry.storeyHeightMetres:F4} m, the intake measured {rise:F2} m");
            }
            AssertNone(failures,
                "the re-bake writes each room's rise through the storey reader, off the rig the catalog names");
        }

        [Test]
        public void TodaysRigsReadTheirRoomsOffTheAnchor_AsTheOldContractHeldThem()
        {
            // The reader's fallback, kept honest: a rig with no dims() reads anchors().storeyZ, which is
            // what main's contract was written from (3.02–3.48 m) before the switch.
            using IRigScriptHost host = TodaysHost();
            foreach (InteriorKit.Build room in InteriorKit.RoomSet)
            {
                string opts = InteriorBakeMenu.OptionsLiteralFor(room);
                double read = InteriorRigBaker.StoreyRiseMetres(host, Interior, opts);
                Assert.AreEqual(host.EvaluateNumber($"{Interior}.anchors(0,{opts}).storeyZ"), read, 1e-9, room.Key);
                Assert.That(read, Is.InRange(3.0, 3.5), room.Key);
            }
        }

        // =============================================================================
        //  ⭐ the registration: every room against its own shell
        // =============================================================================

        [Test]
        public void EveryRoomIsRegisteredAgainstItsOwnShell()
        {
            foreach (InteriorKit.Build room in InteriorKit.RoomSet)
            {
                VillageBuildingKit.Build? shell = VillageBuildingKit.FindBuild(InteriorKit.ExteriorKeyFor(room.Key));
                Assert.IsNotNull(shell, $"{room.Key} stands inside a village shell of the same key");

                string own = VillageBuildingBakeMenu.BaseOptionsLiteralFor(
                    shell.Value, RigCatalog.Get(shell.Value.RigKey).GlobalName);
                InteriorBakeRequest req = InteriorBakeMenu.RequestFor(room);

                Assert.AreEqual(own, InteriorBakeMenu.ExteriorOptionsFor(room.Key), room.Key);
                Assert.AreEqual(own, req.ExteriorOptsJs, room.Key);
                Assert.AreEqual(own, InteriorRigBaker.ExteriorOptsFor(req),
                                $"{room.Key}: the room is measured against its shell's own options, not " +
                                "{size} alone — the returned house puts a size-only door on an eave");
            }
        }

        [Test]
        public void TodaysRigsRegisteredEveryRoomAtOffsetFour()
        {
            // The number the switch retires, measured on main's rigs against each room's own shell: the
            // offset main's contract carried. The returned rigs measure 0 (below), and the re-bake wrote it.
            using IRigScriptHost host = TodaysHost();
            foreach (InteriorKit.Build room in InteriorKit.RoomSet)
            {
                InteriorRigAzimuthProbe.Registration reg = InteriorRigAzimuthProbe.MeasureRegistration(
                    host, House, InteriorBakeMenu.ExteriorOptionsFor(room.Key),
                    Interior, InteriorBakeMenu.OptionsLiteralFor(room), InteriorKit.Facings);
                Assert.AreEqual(4, reg.FacingOffset, $"{room.Key}\n" + reg.Report);
            }
        }

        [Test]
        public void TheReturnedRoomsStandUnderTheirOwnShellsAtOffsetZero()
        {
            using IRigScriptHost host = ReturnedHost();
            foreach (InteriorKit.Build room in InteriorKit.RoomSet)
            {
                string shell = InteriorBakeMenu.ExteriorOptionsFor(room.Key);
                string opts = InteriorBakeMenu.OptionsLiteralFor(room);

                // As the contract dials the room today, and with its house's options as `shell` — the
                // room rig's own hook for the house's outside, which also moves its doorway to the door.
                foreach (string io in new[] { opts, $"Object.assign({{}},{opts},{{shell:{shell}}})" })
                {
                    InteriorRigAzimuthProbe.Registration reg = InteriorRigAzimuthProbe.MeasureRegistration(
                        host, House, shell, Interior, io, InteriorKit.Facings);
                    Assert.AreEqual(0, reg.FacingOffset,
                                    $"{room.Key}: the returned room puts its doorway on the gable the house " +
                                    "puts its door on, so each facing stands under the same facing\n" + reg.Report);
                    Assert.AreEqual(+1, reg.ExteriorGable, room.Key);
                    Assert.AreEqual(+1, reg.InteriorGable, room.Key);
                }
            }

            InteriorKit.Contract contract = InteriorKit.Load();
            Assert.IsNotNull(contract, $"no contract at {InteriorKit.ContractPath}");
            Assert.AreEqual(0, contract.exteriorFacingOffset, "and the re-bake wrote the offset it measured");
        }

        [Test]
        public void TheReturnedRigsRefuseASizeOnlyShell()
        {
            using IRigScriptHost host = ReturnedHost();
            foreach (InteriorKit.Build room in InteriorKit.RoomSet)
            {
                string opts = InteriorBakeMenu.OptionsLiteralFor(room);
                string sizeOnly = InteriorRigBaker.ExteriorOptsFor(InteriorBakeMenu.RequestFor(room).WithExterior(null));
                var ex = Assert.Throws<InvalidOperationException>(() => InteriorRigAzimuthProbe.MeasureRegistration(
                    host, House, sizeOnly, Interior, opts, InteriorKit.Facings));
                StringAssert.Contains("REFUSED", ex.Message,
                                      $"{room.Key}: a house dialled by size alone puts its door on an eave, " +
                                      "which neither gable reading can place — a loud refusal, not a guess");
            }
        }

        // =============================================================================
        //  the classic pin: the returned house draws today's houses
        // =============================================================================

        /// <summary>
        /// SHA-256 of <c>HouseIso.render(dir, opts)</c> — the RGBA bytes the bake reads — for today's
        /// five village houses at dirs 0–7, rendered by the rig on main at d0488e18 (drop 13's house,
        /// 09-16). The same bytes are in today's sheets: the intake re-baked all five from main's rig and
        /// matched every cell, pixel for pixel.
        /// </summary>
        static readonly Dictionary<string, string[]> TodaysHouseCells = new Dictionary<string, string[]>
        {
            ["school"] = new[]
            {
                "0667e349fe0ce214829af5440ade8fa1e590ff852355ebf1cc0685d0f3c9b745", "ae609bc4aa924178345cc2e227311218090f2230b14a8fbfd647812ecfebd505",
                "58964fe656a2b31bb6e178cc6a49d6e6344d187c19619143c3b3198eff19733a", "9ca2ded072b702be3e029c02ea77dcf3926694754a6635808b63bd641da03619",
                "d223f3e8fe4f820ed7e1249c852f3b8be96f9cd748d84da01cb79e2e94b9267e", "405901f5df5e86ca8459a0b523db27cbe1db63be5f4fd44b91d4ce94d15cb06e",
                "56ccbfc292c5fa0d1943f0015baeed7e4c317abaab2013e5424b35831453dd35", "28e2b47af079892bf31358800bc16a6b4587f6316b5db2177250f650da115d1c",
            },
            ["generalStore"] = new[]
            {
                "3c6bc784ea117d117d21f46ef74296a7a177e02b937c8cf84f3d2ccc608e6b1c", "e15ab8e5e3a2ad8d5d953702b75fc088b285bd35949060d881335d18b845cb1c",
                "194fd3665bd6b527f855ee0ca73366a8fad8278014883e6fe1ac6620b5221674", "8bed66cb24fa2101620b3fa82b2cd533414fe55d1305f531d521c00187f5572d",
                "23f3463eb6faccf7c4fb09db34ee8676bc0f59777e40289390106a76a5161bc3", "661c97f2d5f4bf65315c714ba3d37944a1d93490439d86a8d4e08328bc19d0a7",
                "50ff2876a21259087ab62442f731dd1a1469f8a4655860ddaf291baf63dd0805", "9e16b6f11ebc1f6249f719f45298e1c361f64033e3cd3d1ad2ec73696d71968c",
            },
            ["whiteFarmhouse"] = new[]
            {
                "8be5a46a9a9579fa2bec286a406155156a0de2d29a473da726fe8980fb0ccdf5", "089caaeed7eb4404e8403ca71380a02ee5a8c9d6db4cc6ad27376961d71b16f3",
                "9bb8c4f4eb4c598c50c57650b6e008f5accc2c334172c00d7e49376a3f1bd9e6", "a3297ddc531d08c39c779725cde5e08a8cc0c45984db1511ba9cab55570d035b",
                "b615632bb1930a90d4c33705b988683e3cbe7747f85dffb7521abd53730322e7", "11fbaf710912e52523b89b5fce3ae7dbf26fd7ea89b49a10a48c4ee18e95b4d1",
                "f78f6717fdb67f2bb5b82a71ad07fb4e4ff3d3ef759a20139dd8c72c0d2fdb84", "38c024ea606ae731959115e5a5e83b74da11485a0cb0a2fec3869e2ca68aeaa0",
            },
            ["redSaltbox"] = new[]
            {
                "344d4456288146bc07806720230e69279f3713717c8ef264fd2eecadf56c0c28", "7419be359b0f969d01ce478c745d6487220a141b59c0896335b33a0cd4154bf2",
                "8f84750995069717643c7ebc380f7e48f5a50fa1d8922bcd2d1c99594fe03a2b", "22a358631605feec90443332fb4382f76f86876f0a3729e1dbad72e1998ccde1",
                "79cb3330f665bc64679a843e1150c97f7105055d7f0227daf05420c62765289a", "bbc6b8cfdae52c4cc6b7c87e2e7b290584bcdc2407350855601ac45ddd02bb09",
                "d6adab1f2c3107e518d9899610de70caec5ccdfe8918143a7d9102db2178ed68", "57f9c82d3d6f845950a01f7b45648d65a544cfbd52d3ab29149a6fcb38a81ef7",
            },
            ["sageCottage"] = new[]
            {
                "dbe25b6e94782872b3eeebcf1cec35e7c365c7f59e219e53bf09706d0374ad80", "df261038f416ab927a079e49c4cfd427caef84657b039074e9e3d5f9d0db4372",
                "20b8367c851ba62407defeed8b34e445ecbd55c5151e670f44d654a77c4525ae", "ebd1d6fc6b67e7398f9b2fb5ea6b0f13b9f791f869f801124f6103a3c547a2bf",
                "1b9238ca74333643eb13452445eda3cdf384738a568b053818db4abaa2a601e3", "b70a91f2f54c8b9f8a62a38e6b8bf1a209ae2e5a88cc507cceb8f59552ce08e2",
                "891ab89b0c89748c9c042684d5cdd0a0e9aa69b763058bb5b9b9f00c77a52e61", "a45d90ce685bc1e8de12583c1ba710ba73d1e555083e3c3087a2ec8c0021ea08",
            },
        };

        [Test]
        public void TheReturnedHouseInClassicDrawsTodaysHousesPixelForPixel()
        {
            VillageBuildingKit.Build[] houses = VillageBuildingKit.M1Set.Where(b => b.RigKey == "house").ToArray();
            CollectionAssert.AreEquivalent(TodaysHouseCells.Keys, houses.Select(b => b.Key),
                                           "the pin covers every village house the kit bakes today");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, RigCatalog.Get("house"));
            var failures = new List<string>();
            using (SHA256 sha = SHA256.Create())
            {
                foreach (VillageBuildingKit.Build b in houses)
                {
                    // classic:true keeps 09-16 reproducible, but the companion dresses a house whenever
                    // it is loaded — so the classic look also switches it off, per build.
                    string opts = $"Object.assign({{}},{VillageBuildingBakeMenu.BaseOptionsLiteralFor(b, House)}," +
                                  "{classic:true,coastalPass:false})";
                    for (int d = 0; d < VillageBuildingKit.Facings; d++)
                    {
                        string got = Hex(sha.ComputeHash(host.EvaluateBytes($"{House}.render({d},{opts})")));
                        if (got != TodaysHouseCells[b.Key][d])
                            failures.Add($"{b.Key} dir {d}: {got}");
                    }
                }
            }
            AssertNone(failures,
                "the returned house with {classic:true, coastalPass:false} no longer draws today's pixels");
        }

        // =============================================================================
        //  the building probe: the general store's door is on an eave now
        // =============================================================================

        [Test]
        public void TheGeneralStoresEaveDoorIsReadFromItsLoop_AsTheCatalogDeclares()
        {
            VillageBuildingKit.Build? store = VillageBuildingKit.FindBuild("generalStore");
            Assert.IsNotNull(store, "the general store left the kit");

            RigEntry house = RigCatalog.Get("house");
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigGeometry geo = RigCatalog.Install(host, house);
            string opts = VillageBuildingBakeMenu.BaseOptionsLiteralFor(store.Value, House);

            Assert.AreEqual("+X", BuildingRigAzimuthProbe.DoorWall(host, House, opts),
                            "the returned rig routes the general store's door (front porch + bay) onto the " +
                            "+X eave — accepted by the owner's ruling of 09-27; the house is not placed");

            BuildingRigAzimuthProbe.Result probe = BuildingRigAzimuthProbe.Measure(
                host, House, opts, geo.Width, geo.Height, geo.PivotX);
            Assert.Less(Math.Abs(probe.DoorOffsetPx), BuildingRigAzimuthProbe.MinDoorOffsetPx,
                        "at a quarter turn the eave door faces the camera or away, on the pivot — the side " +
                        "reading has nothing to give, which is why the loop answers\n" + probe.Report);
            Assert.AreEqual(house.DeclaredConvention, probe.Convention, probe.Report);
            StringAssert.Contains("eave", probe.Report, "the report names the reading that answered");
        }

        [Test]
        public void EveryDoorTheProbeCanReadCirclesThePivotTheWayItsSideSays()
        {
            // The loop reading's calibration, re-measured: on every building whose door the SIDE reading
            // can read, a counter-clockwise side is a negative loop. Main's house, the returned house and
            // the wharf buildings; the general store's eave door is the one the side cannot read.
            var rigs = new (string Label, RigEntry Entry, string RigKey)[]
            {
                ("main's house", TodaysHouse, "house"),
                ("the returned house", RigCatalog.Get("house"), "house"),
                ("the wharf", RigCatalog.Get("wharfBuilding2"), "wharfBuilding2"),
            };

            var failures = new List<string>();
            int read = 0, eave = 0;
            foreach ((string label, RigEntry entry, string rigKey) in rigs)
            {
                using IRigScriptHost host = RigScriptHostFactory.Create();
                RigGeometry geo = RigCatalog.Install(host, entry);
                string g = entry.GlobalName;
                // Every build in the kit, M1 and lifecycle: a lifecycle build is probed on its bare
                // building (BuildingBakeRequest.UnderlyingOptsJs), which is what BaseOptionsLiteralFor is.
                foreach (VillageBuildingKit.Build b in VillageBuildingKit.AllBuilds.Where(b => b.RigKey == rigKey))
                {
                    string opts = VillageBuildingBakeMenu.BaseOptionsLiteralFor(b, g);
                    double side = host.EvaluateNumber($"{g}.anchors(2,{opts}).door.x") - geo.PivotX;
                    double loop = BuildingRigAzimuthProbe.DoorLoopAreaPx2(host, g, opts);

                    if (Math.Abs(loop) < BuildingRigAzimuthProbe.MinDoorLoopAreaPx2)
                        failures.Add($"{label} {b.Key}: the door sweeps only {loop:F0} px² around the pivot");
                    if (Math.Abs(side) < BuildingRigAzimuthProbe.MinDoorOffsetPx)
                    {
                        eave++;
                        continue;
                    }
                    read++;
                    if ((side < 0) != (loop < 0))
                        failures.Add($"{label} {b.Key}: the side reads {side:+0.0;-0.0} px but the loop " +
                                     $"{loop:+0;-0} px²");
                }
            }

            AssertNone(failures, "the door's loop no longer agrees with its side — the loop reading's sign " +
                                 "is calibrated on these builds, so BuildingRigAzimuthProbe's eave path is unproven");
            Assert.AreEqual(13, read, "the calibration was measured on 13 builds whose side the probe reads " +
                                      "(5 on main's house, 4 on the returned, 4 wharf); a new build re-opens it");
            Assert.AreEqual(1, eave, "and exactly one door the side cannot read: the general store's");
        }

        // =============================================================================
        //  the landing: the drop's bytes, where the intake put them
        // =============================================================================

        /// <summary>Stamped paths that stay in the intake's Evidence~: the drop's boards, its village
        /// plan, its own checker and the phase-1 rigs it was diffed against.</summary>
        static readonly string[] EvidenceOnly =
        {
            "boards/", "boards-outbuildings/", "boards-phase2/", "boards-shops/", "boards-stpeters/",
            "boards-yards/", "checks/", "phase1-rigs/", "plan/",
        };

        /// <summary>The two rigs the houses kit ships byte-identical to the one copy <c>docs/art/rigs/</c>
        /// already holds, and where that copy is. The wharf rig is drop 13's family, and the prop rig has
        /// one home (MultiunitKitIntakeTests.OneInteriorPropRig).</summary>
        static readonly Dictionary<string, string> LandedElsewhere = new Dictionary<string, string>
        {
            ["houses-kit/Art/interiorPropRig.js"] = "docs/art/rigs/interiorPropRig.js",
            ["houses-kit/Art/wharfBuildingRig.js"] = "docs/art/rigs/wharfBuildingRig.js",
        };

        /// <summary>Files in the folder that no stamp covers: the stamps themselves, the intake's notes,
        /// and the five Claude Design pages the drop did not list (checked against the zip at intake).</summary>
        static readonly string[] Unstamped =
        {
            "SHA256SUMS.json", "INTAKE.md",
            "houses-kit/Cottage Interior Iso.dc.html", "houses-kit/House Iso.dc.html",
            "houses-kit/Interior Props Iso.dc.html", "houses-kit/Manor Interiors.dc.html",
            "houses-kit/Manor Iso.dc.html",
        };

        [Test]
        public void EveryStampedFileIsWhereTheIntakePutItByteForByte()
        {
            var stamps = MiniJson.Parse(File.ReadAllText(Abs(Folder + "/SHA256SUMS.json"))) as Dictionary<string, object>;
            Assert.IsNotNull(stamps, $"{Folder}/SHA256SUMS.json does not parse to an object");

            var failures = new List<string>();
            foreach (KeyValuePair<string, object> stamp in stamps)
            {
                string here = Folder + "/" + stamp.Key;
                if (EvidenceOnly.Any(p => stamp.Key.StartsWith(p, StringComparison.Ordinal)))
                {
                    if (File.Exists(Abs(here)))
                        failures.Add($"{stamp.Key}: landed, but the intake keeps it in Evidence~");
                    continue;
                }
                string at = here;
                if (LandedElsewhere.TryGetValue(stamp.Key, out string other))
                {
                    if (File.Exists(Abs(here)))
                        failures.Add($"{stamp.Key}: a second copy of {other}");
                    at = other;
                }
                failures.AddRange(Check(stamp.Key, at, stamp.Value as string));
            }

            string root = Path.GetFullPath(Abs(Folder));
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (!stamps.ContainsKey(rel) && !Unstamped.Contains(rel))
                    failures.Add($"{rel}: in {Folder}/, but neither stamped nor named as unstamped");
            }
            AssertNone(failures, $"{Folder}/ against the drop's SHA256SUMS.json");
        }

        [Test]
        public void TheHousesKitManifestCoversItsRigs()
        {
            object manifest = MiniJson.Parse(File.ReadAllText(Abs(Folder + "/houses-kit/manifest.json")));
            Dictionary<string, object> rigs = MiniJson.Dict(manifest, "rigHashes");
            Assert.IsNotNull(rigs, "houses-kit/manifest.json has no rigHashes");

            var failures = new List<string>();
            foreach (KeyValuePair<string, object> rig in rigs)
            {
                string key = "houses-kit/" + rig.Key;
                string at = LandedElsewhere.TryGetValue(key, out string other) ? other : Folder + "/" + key;
                failures.AddRange(Check(key, at, rig.Value as string));
            }
            AssertNone(failures, "houses-kit/manifest.json's rigHashes");
        }

        // =============================================================================
        //  helpers
        // =============================================================================

        static string Abs(string rel) => Path.Combine(RigCatalog.RepoRoot, rel);

        static IEnumerable<string> Check(string key, string at, string want)
        {
            string path = Abs(at);
            if (!File.Exists(path))
            {
                yield return $"{key}: missing at {at}";
                yield break;
            }
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 200 && Encoding.ASCII.GetString(bytes).StartsWith("version https://git-lfs", StringComparison.Ordinal))
            {
                yield return $"{key}: {at} is an LFS pointer — the object was never checked out";
                yield break;
            }
            string got = Sha256(bytes, normaliseLineEndings: !at.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            if (got != want)
                yield return $"{key}: {at} hashes to {got}, the stamp says {want ?? "(not a string)"}";
        }

        /// <summary>SHA-256, hex. Text is hashed with CRLF read as LF — the stamps are the drop's LF bytes,
        /// and a Windows checkout writes CRLF — while an image is hashed as it lies.</summary>
        static string Sha256(byte[] bytes, bool normaliseLineEndings)
        {
            int n = bytes.Length;
            if (normaliseLineEndings)
            {
                n = 0;
                for (int i = 0; i < bytes.Length; i++)
                    if (bytes[i] != '\r' || i + 1 == bytes.Length || bytes[i + 1] != '\n')
                        bytes[n++] = bytes[i];
            }
            using SHA256 sha = SHA256.Create();
            return Hex(sha.ComputeHash(bytes, 0, n));
        }

        static string Hex(byte[] hash) => BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        static void AssertNone(List<string> failures, string what)
        {
            if (failures.Count == 0) return;
            string more = failures.Count > Shown ? $"\n... and {failures.Count - Shown} more" : "";
            Assert.Fail($"{what}: {failures.Count} failure(s)\n{string.Join("\n", failures.Take(Shown))}{more}");
        }
    }
}
