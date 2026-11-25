namespace HiddenHarbours.Tests.EditMode
{
    public partial class ModuleBoundaryTests
    {
        // Original base f1b4ab2f59f417eda06f2239f7dee25d4c52ad23, 2026-11-22.
        // Reconciled with d46bde303e6aa256d23d47a05ede5d7d6f38e51e, 2026-11-25:
        // calm-mirror phase fallback added; two old speech clock lines replaced by one injected read.
        // Unchanged entries retain their original source line references.
        // Companion audit: docs/design/randomness-and-clock-source-audit.md (separate documentation change).
        // Each fingerprint hashes the complete whitespace-normalized source line in UTF-8, not just
        // the matched API. Counts forbid duplicate copies; line numbers below are review references.
        // This is a frozen inventory, not approval to expand any known fault or presentation exception.
        private static readonly SourceAllowance[] ReviewedSource =
        {
            // PRESENTATION, base line(s) 709: Camera framing time only.
            new SourceAllowance("App/CameraFollow.cs", "ce7d79846921bda0f372f516bd77ad3a318e1308ae092e9220f24b0f8cc8720a", 1),
            // PRESENTATION, base line(s) 160: Camera shake phase only.
            new SourceAllowance("App/FightStrainCamera.cs", "c8f91b4c3b6033e0a93570eb390a144a564eb0bbdfd8adea3e1f26f8aa0cba1f", 1),
            // PRESENTATION, base line(s) 14: Crane picture refresh only.
            new SourceAllowance("App/NmcWharfCraneVisual.cs", "16d87341f8e7fd843986d8ebdacb187306b966a38198242756034f7a9c88c583", 1),
            // PRESENTATION, base line(s) 17: Crane picture refresh only.
            new SourceAllowance("App/NmcWharfCraneVisual.cs", "deb34860aedf7a0c9a4982d425bc24d29d3fd929daf08d5d1cf19c143d0c96d8", 1),
            // PRESENTATION, base line(s) 187: Throttles water/deck picture refresh; daily work is sampled from the game clock.
            new SourceAllowance("App/NpcPotBoats.cs", "85bf6c26c1cbc010e3c27471db238e6eabbd12abb65d2fcecc6ac47f958b6530", 1),
            // PRESENTATION, base line(s) 190: Throttles water/deck picture refresh; daily work is sampled from the game clock.
            new SourceAllowance("App/NpcPotBoats.cs", "f29dca7d58835cf8f4e2e6447d6d2103d221994e3e9e1f8c7d1be6e8d1ca5399", 1),
            // PRESENTATION, base line(s) 139: Throttles loaded-scene visibility refresh; the trip pose is sampled from the game clock.
            new SourceAllowance("App/NpcTrawlBoats.cs", "85bf6c26c1cbc010e3c27471db238e6eabbd12abb65d2fcecc6ac47f958b6530", 1),
            // PRESENTATION, base line(s) 142: Throttles loaded-scene visibility refresh; the trip pose is sampled from the game clock.
            new SourceAllowance("App/NpcTrawlBoats.cs", "f29dca7d58835cf8f4e2e6447d6d2103d221994e3e9e1f8c7d1be6e8d1ca5399", 1),
            // PRESENTATION, base line(s) 13: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/AmbientParticleMath.cs", "d4ba42d7d76b15f7334e813ed96e7a753b48b0a82a51bb10b674e4af7becd7a9", 1),
            // PRESENTATION, base line(s) 26: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/AmbientParticleMath.cs", "086b8abf50c6fe4a9b7d50eb61ce603c81351a2f42c4724649af67090a5f3c9b", 1),
            // PRESENTATION, base line(s) 623: Spotlight flicker only; process-dependent hash is also presentation.
            new SourceAllowance("Art/BoatSpotlight.cs", "258b3530a082bd9c1915ef4c41ca146238c715320afa5d47430e333e89321810", 1),
            // PRESENTATION, current line 247: Mirror ripple phase fallback when the game clock is absent; no simulation mutation.
            new SourceAllowance("Art/CalmMirror.cs", "7880186a505fbb2a207accdda77a10b193a37d069d8ab85ffff933f2e0d5160c", 1),
            // PRESENTATION, base line(s) 27: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/CharacterFigureLife.cs", "e6f094a62eb97739a0b84dd6b08a132fc13d783bd8c527e0d30474164c7c25b5", 1),
            // PRESENTATION, base line(s) 16: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/ChimneySmoke.cs", "e36f54b1d3c13448d1d309a913438b367ceab0c6314391a3fa93d2c0debec204", 1),
            // PRESENTATION, base line(s) 38: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/DriftWeedKit.cs", "a6f587a7e772733e9c8d53f0fbcac100e18d4b56b1fe103d23058873539f882b", 1),
            // PRESENTATION, base line(s) 14: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/DustMotes.cs", "703e9fa5ec0d04ae3eea59836d7c4a286bcf501912614116d60b40ccea8f697c", 1),
            // PRESENTATION, base line(s) 109: Dust particle phase only.
            new SourceAllowance("Art/DustMotes.cs", "f2ca52893a6f05f13f93681f767866216a3b9066d57a9f46e8988e38e487ac41", 1),
            // PRESENTATION, base line(s) 36: Preview grass wind phase only.
            new SourceAllowance("Art/GrassDevWind.cs", "d345fbe628e75eab779791673b1fdcff3716a718ebb6f1ad159831b69ed58755", 1),
            // PRESENTATION, base line(s) 46: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/GrassFootstep.cs", "a2d0adbecdb11b41af48be8b65a682320dd8f56f9233d3e9b12d21d9623b02ac", 1),
            // PRESENTATION, base line(s) 300: Cosmetic grass displacement fade only.
            new SourceAllowance("Art/GrassFootstep.cs", "9704d93f7e72c7443cb7e53a68511c7fb482de8f3e3506041e4a42b914126c5f", 1),
            // PRESENTATION, base line(s) 161: Ground-print picture refresh only.
            new SourceAllowance("Art/GroundPrintRenderer.cs", "5c3c1d4873e71e17676a802a4177c77268a8537fc7bce484c1d885c54421a1bd", 1),
            // PRESENTATION, base line(s) 163: Ground-print picture refresh only.
            new SourceAllowance("Art/GroundPrintRenderer.cs", "42823a03acad40da5d79bbb924c21c606652a9868cf51648a8aa5a4d8a0af4fd", 1),
            // PRESENTATION, base line(s) 60: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/GullFlock.cs", "6e2f5aa616f2d9d63be8b89ac970bbe7fe50aa74f5ed04454f0c487ae7ce7665", 1),
            // PRESENTATION, base line(s) 163: Reads spoil to tint contents; does not settle or mutate the hold.
            new SourceAllowance("Art/HoldCatchFillSource.cs", "a51a95a551fd831d93df4a579e45c713a45bbab34145c495917b3296a508b8cd", 1),
            // PRESENTATION, base line(s) 164: Reads spoil to tint contents; does not settle or mutate the hold.
            new SourceAllowance("Art/HoldCatchFillSource.cs", "fef3567cb44f7a60b903aee85457bc8b4d63ddfae5de08a0088e11370cb26ec5", 1),
            // PRESENTATION, base line(s) 102: Hull picture wave phase; engine-time fallback without a clock.
            new SourceAllowance("Art/HullWaveBridge.cs", "b6d3c53a28eb296894c9616ec35978a33e08381e4949772d27dc7b3522bca0c5", 1),
            // PRESENTATION, base line(s) 218: Offer highlight pulse timing; does not decide interaction eligibility.
            new SourceAllowance("Art/InteractAffordancePresenter.cs", "d87910589e0e1d6ed3c287f368152aca07bd7fe42e3d5f859325c0eeaeae393c", 1),
            // PRESENTATION, base line(s) 375: Offer highlight pulse timing; does not decide interaction eligibility.
            new SourceAllowance("Art/InteractAffordancePresenter.cs", "1feadb42e91b1e361337b95d612b4747e214ef35c6dd147ff4dec1e8458d34b9", 1),
            // PRESENTATION, base line(s) 9: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/LightMath.cs", "8b7c7f91e135810d4fd2817ebb33ceec560d101dbabc4c9f6d96d3ec9ae2b328", 1),
            // PRESENTATION, base line(s) 401: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/LightMath.cs", "9ea1aaa8bfe95cd8861e79cda1de4427490cfe6396c14f5df69a5483d3087737", 1),
            // PRESENTATION, base line(s) 643: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/LightMath.cs", "4d6e271f7f81ecab1615f27597a2d05f1e3e01e9c7a7de060b916a280658ff1a", 1),
            // PRESENTATION, base line(s) 168: Navigation lamp phase; engine-time fallback without a clock.
            new SourceAllowance("Art/NavLight.cs", "c32e3ecbc93383f4787a7a014e57f95f59f7907dc3d7fd0cd7e7d8eafd456415", 1),
            // PRESENTATION, base line(s) 211, 216: Refreshes plant appearance and shadow meshes only.
            new SourceAllowance("Art/PlantField.cs", "2a17440999173d9430b7d9e696dfe4ef03a400ada63c16969715ffe8c5edde6f", 2),
            // PRESENTATION, base line(s) 215: Refreshes plant appearance and shadow meshes only.
            new SourceAllowance("Art/PlantField.cs", "875aec0622e1f50725c10fbc6e5437702fd2491ba6e0a526f99ca16ed689f5c9", 1),
            // PRESENTATION, base line(s) 23: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/PreconfiguredLight.cs", "a42f00d293edc28f0bad7c9f6e342068a8cb28fb6ce3d9d5bb8972f10ec03fec", 1),
            // PRESENTATION, base line(s) 32: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/RainEmitter.cs", "d4c422bb22f0f13b5256c4837de20da7806cd8a7ab50f30fa4d9e73f2f503f69", 1),
            // PRESENTATION, base line(s) 30: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/SceneLight.cs", "50beb4e65522f3277a72a9815e6f08ae5347dcf8f1831e733719a9cd1fc8a257", 1),
            // PRESENTATION, base line(s) 138: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/SceneLight.cs", "32478a69cb7c55f61104566673dbd8170d5254c6a6ea90452b3b2712b7c97a3e", 1),
            // PRESENTATION, base line(s) 521: Lamp flicker phase; engine-time fallback without a clock.
            new SourceAllowance("Art/SceneLight.cs", "425515a902828b903371a3a205eca49a1c4fc5c6c10b0afa270825e093e86694", 1),
            // PRESENTATION, base line(s) 39: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/SeagullFlockMath.cs", "114226cdf2fbbc452a740347197da759d87125cc5983f4c16a3616460c9dc989", 1),
            // PRESENTATION, base line(s) 14: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/SeagullSplashMath.cs", "054b852aa36e33a08eb392ff4e9416a1bf6c1fb8e896302d715a9e14f8e4bfb3", 1),
            // PRESENTATION, base line(s) 24: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/SeaMistEmitter.cs", "8f09c7538a0f29143444f4c53e8f66150356f398c7fc4404e0f718569fc5211e", 1),
            // PRESENTATION, base line(s) 15: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/SeaweedMath.cs", "8af53a6786e34a994349f21057e93fb37bc45e5ceb4661ae9680e744b4a9a636", 1),
            // PRESENTATION, base line(s) 19: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/SeaweedPresenter.cs", "feaca3636ecf9efa8e83d0126f2af1ab27318bad32de147a403f80da7077dab9", 1),
            // PRESENTATION, base line(s) 209: Decor-only activation and snag/strand cadence; no catch, load or saved mutation.
            new SourceAllowance("Art/SeaweedPresenter.cs", "0f553df8fc6adc2cfc722cc432995ba3c0e7692abd1b35aee8a829d943312b13", 1),
            // PRESENTATION, base line(s) 211: Decor-only activation and snag/strand cadence; no catch, load or saved mutation.
            new SourceAllowance("Art/SeaweedPresenter.cs", "81546787c97206489b5600e1190951a976118487b256d2e26748f042d691c8f4", 1),
            // PRESENTATION, base line(s) 226: Decor-only activation and snag/strand cadence; no catch, load or saved mutation.
            new SourceAllowance("Art/SeaweedPresenter.cs", "85bf6c26c1cbc010e3c27471db238e6eabbd12abb65d2fcecc6ac47f958b6530", 1),
            // PRESENTATION, base line(s) 227: Decor-only activation and snag/strand cadence; no catch, load or saved mutation.
            new SourceAllowance("Art/SeaweedPresenter.cs", "cfbc9c457eab0b4301c2248dae24d7939893425467fdb22640c5178429b4138f", 1),
            // PRESENTATION, base line(s) 105: Plant tide appearance refresh only.
            new SourceAllowance("Art/ShorePlantTideDriver.cs", "05e32959de381a40addd9774a312671131bb33334a7c0777f80b50122a85e267", 1),
            // PRESENTATION, base line(s) 106: Plant tide appearance refresh only.
            new SourceAllowance("Art/ShorePlantTideDriver.cs", "d1ab0b882439544a42c992054e413a36cea77477f4f2375891197822c2af7484", 1),
            // PRESENTATION, base line(s) 34: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/SprayEmitter.cs", "c93a1ecfcd9641fa3d7aa59814cdee87e805488c6261077eaa943a9183a62cfa", 1),
            // PRESENTATION, base line(s) 21: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/SurfSprayEmitter.cs", "88f0172c32feede38649572ed0da07d59d140d665fe35c708299536f8d9440d4", 1),
            // PRESENTATION, base line(s) 172: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Art/WadeSplashEmitter.cs", "2cf022c15a1e43b07979958357e1ab10c9d7d41deb70843a183e9ca6cd9fc278", 1),
            // PRESENTATION, base line(s) 190: Press/hold gesture controls lamp appearance; not autonomous world simulation.
            new SourceAllowance("Art/WalkerLights.cs", "75f45aa5a8a783a2a322221c15f7ac57ceb3b1513ebeb817f4fa61bde7f2a512", 1),
            // PRESENTATION, base line(s) 194: Press/hold gesture controls lamp appearance; not autonomous world simulation.
            new SourceAllowance("Art/WalkerLights.cs", "8606f9903726151400b0b8c5bf32661bdd005ba1661ea2fd836c2b365bfb8efe", 1),
            // PRESENTATION, base line(s) 386: Oar sound scheduling and listener discovery only.
            new SourceAllowance("Audio/AudioDirector.cs", "04c89f71b922567e4f16f18a069e03643f180dff26e90e7bc10075301957b607", 1),
            // PRESENTATION, base line(s) 395: Oar sound scheduling and listener discovery only.
            new SourceAllowance("Audio/AudioDirector.cs", "a9ea392624dac2b363639399cdde53b475c8c4bc4f069cb7092ea4126c62ef90", 1),
            // PRESENTATION, base line(s) 397: Oar sound scheduling and listener discovery only.
            new SourceAllowance("Audio/AudioDirector.cs", "2939803ff16b63385ab07893e2298bb607b1956209bba6c7efb3e7dae5b3294a", 1),
            // PRESENTATION, base line(s) 157: Fishing sound cooldown only.
            new SourceAllowance("Audio/FishingAudio.cs", "2fb92a6e8810f46e2150c5f470583a5033f668e0a49b55a4754e55db07090868", 1),
            // SIM, base line(s) 26: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Boats/AmbientFleetPlan.cs", "c7bf4f30f41d083fdcac9a55168f50aca7b287d438f482e5a2249e863b119941", 1),
            // PRESENTATION, base line(s) 202: Decor-only activation and steering probes; no placed gear, catch or economy mutation.
            new SourceAllowance("Boats/AmbientFleetPresenter.cs", "0f553df8fc6adc2cfc722cc432995ba3c0e7692abd1b35aee8a829d943312b13", 1),
            // PRESENTATION, base line(s) 204: Decor-only activation and steering probes; no placed gear, catch or economy mutation.
            new SourceAllowance("Boats/AmbientFleetPresenter.cs", "81546787c97206489b5600e1190951a976118487b256d2e26748f042d691c8f4", 1),
            // PRESENTATION, base line(s) 217: Decor-only activation and steering probes; no placed gear, catch or economy mutation.
            new SourceAllowance("Boats/AmbientFleetPresenter.cs", "85bf6c26c1cbc010e3c27471db238e6eabbd12abb65d2fcecc6ac47f958b6530", 1),
            // PRESENTATION, base line(s) 220: Decor-only activation and steering probes; no placed gear, catch or economy mutation.
            new SourceAllowance("Boats/AmbientFleetPresenter.cs", "f29dca7d58835cf8f4e2e6447d6d2103d221994e3e9e1f8c7d1be6e8d1ca5399", 1),
            // PRESENTATION, base line(s) 61: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Boats/BoatWakeEmitter.cs", "7295180870cf77339f3378206c0ae6aebc9b8372b7a917869f41cc03742a4c61", 1),
            // PRESENTATION, base line(s) 395: Wake picture phase; engine-time fallback without a clock.
            new SourceAllowance("Boats/BoatWakeEmitter.cs", "0bf51656ebf367ce0e49f463be2ded5673bee90163662dce6a7cac02d2edfc79", 1),
            // PRESENTATION, base line(s) 443: Visual wave riding; engine-time fallback without a clock.
            new SourceAllowance("Boats/BoatWaveMotion.cs", "326274db13ac7c6616a0c76834551f90d0595082561f50be8b76f7cd81cda69d", 1),
            // PRESENTATION, base line(s) 38: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Boats/BowImpactMath.cs", "e7911f3aad41813f26ff23b711720e6aa3f8694532e9178ed0b560c2cd395cdf", 1),
            // PRESENTATION, base line(s) 262: Buoy picture wave phase; engine-time fallback without a clock.
            new SourceAllowance("Boats/BuoyWaveVisual.cs", "bd8de11fd401298e313b0a2f11b0c6945a4431d24a1bcbcef55c5574870b6839", 1),
            // PRESENTATION, base line(s) 177: Reads spoil to tint the container; does not settle the hold.
            new SourceAllowance("Boats/DeckContainerPresenter.cs", "c8f2b193c99300644837eacd47d319e06efac3c80df8b6e12e7718ef389e8c6d", 1),
            // PRESENTATION, base line(s) 179: Reads spoil to tint the container; does not settle the hold.
            new SourceAllowance("Boats/DeckContainerPresenter.cs", "fef3567cb44f7a60b903aee85457bc8b4d63ddfae5de08a0088e11370cb26ec5", 1),
            // PRESENTATION, base line(s) 381: Visual interpolation between rowing physics steps.
            new SourceAllowance("Boats/DoryOarMeshLayer.cs", "22d18b3d1b7c700a314be65d0590726b581c453e374de8817311b934f5151c4b", 1),
            // PRESENTATION, base line(s) 417: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Boats/DoryOarSplashSystem.cs", "0f34e08640a4cdd6e399ab151c95b83e43ca910427d928536ca502ead7909470", 1),
            // PRESENTATION, base line(s) 347, 382: Caches depth/wind instrument readouts; physical grounding and forces do not read this cache.
            new SourceAllowance("Boats/HelmControlRelay.cs", "9704d93f7e72c7443cb7e53a68511c7fb482de8f3e3506041e4a42b914126c5f", 2),
            // PRESENTATION, base line(s) 307: Smooths displayed hull trim; engine-time fallback without a clock.
            new SourceAllowance("Boats/MeshHullDriver.cs", "0510ddc4452b30015875ef419f6e5a59d47d69e6ac2a31698878f155e10ad192", 1),
            // PRESENTATION, base line(s) 38: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Boats/WakeBubbleSystem.cs", "3712fa7e64760fea9f6fdca0bf4d4f1bd4251420407c32f2a1051fcbe6b29e0d", 1),
            // PRESENTATION, base line(s) 34: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Boats/WakeParticleSystem.cs", "22cfed84e04994c0d23d13be0bc05353da088cf885974a1a35e679a971b64830", 1),
            // PRESENTATION, base line(s) 365: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Boats/WakeParticleSystem.cs", "c2da5d37285ce9856bcd4e7336958396b5c182a93b2b353640c305b8ac547dbe", 1),
            // PRESENTATION, base line(s) 389: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Boats/WakeParticleSystem.cs", "9e4b583c8c8c0d158675eff2581c4f76785a42fa84f7b9f1197b1da2d3933c54", 1),
            // PRESENTATION, base line(s) 36: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Core/Environment/WakeFoamAgeing.cs", "0eb614c56ea404c97d2e69f5b2615aaa9ee3a5e1c63a4518747272e4427bf9c7", 1),
            // PRESENTATION, base line(s) 108: Presentation wave-clock fallback; explicit-input wave methods do not read engine time.
            new SourceAllowance("Core/Environment/WaveFieldAnimator.cs", "eec39ea15b4213a328e62bfc73cba4ee8b63985d0a6ceff6941492b917f9eb0e", 1),
            // PRESENTATION, base line(s) 112: Presentation wave-clock fallback; explicit-input wave methods do not read engine time.
            new SourceAllowance("Core/Environment/WaveFieldAnimator.cs", "5d4a8780567082124f94711ccac9043877a68eab54bcf4ffe73d56322c42b8f1", 1),
            // PRESENTATION, base line(s) 83: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Core/Fishing/FishJumpArc.cs", "a6a84e0339e7d4660238ecb9418332c992c240b9c609134ad66988f4f56cb442", 1),
            // PRESENTATION, base line(s) 15: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Core/Iso/CharacterFigureBlink.cs", "dddbf9c53f58e3c4e5dfddec6c7ebd0e259ab327d96f5abc31465af4893d0c1b", 1),
            // PRESENTATION, base line(s) 61: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Core/Iso/CharacterSkinPose.cs", "3575dfb3a27edb8246ef5842f3f4fa4edd491332aa4a8fd195c410bbca79154d", 1),
            // PRESENTATION, base line(s) 26: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Core/Nav/LoranMath.cs", "f9e1134a8a98706fb8dd630dcea0c1574a82cc3f52e3dcb2f53b2ffd27c9132e", 1),
            // PRESENTATION, base line(s) 45: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Core/Nav/NavLightCharacter.cs", "56a1ac4fe2dc38eba1a3719cf741bfbcc7873c54143bfb7c841128e2d610226a", 1),
            // PRESENTATION, base line(s) 214: Refusal speech cooldown only; the effort check precedes this read.
            new SourceAllowance("Core/Needs/EffortGate.cs", "e1a4c286e3f0619742a3b926de524592ab38f93f114f8f3355903d8963265990", 1),
            // TOOLING, base line(s) 143: Editor load-read timeout; production call sites are editor-conditional, not a world-model clock.
            new SourceAllowance("Core/World/SceneLoadHold.cs", "51fc725df8a5085d0f6c35dabc1befb647afc8fed8e96bc7098147c8f7383ac5", 1),
            // SIM, base line(s) 17: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Environment/CurrentModel.cs", "e871600d316bd654168a428bec0ac0330258fcb1022e1ea25b05af3c5e994399", 1),
            // PRESENTATION, base line(s) 139: Refreshes visible dug-hole pictures from explicit game time.
            new SourceAllowance("Environment/DugHolePresenter.cs", "952b857f0c79f7d540045d4d09bd0fc1d34404431139054d26215bcd5ffb94df", 1),
            // PRESENTATION, base line(s) 141: Refreshes visible dug-hole pictures from explicit game time.
            new SourceAllowance("Environment/DugHolePresenter.cs", "720080a1c46c44db49319fd9531b09620ba14ee8bd60d91ac01acada8b5c4cc5", 1),
            // SIM, base line(s) 8: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Environment/WeatherModel.cs", "6be7ac7be6046a28eb50c3d0adc2f394fd727146a066d98d102842ce896b4b5c", 1),
            // SIM, base line(s) 198: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Environment/WeatherModel.cs", "68816d8b34ac186aab518c96fcd0b0a1452bade5307c0771dc570a8133a27582", 1),
            // SIM, base line(s) 60: Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy.
            new SourceAllowance("Fishing/BiteSequenceSim.cs", "2daf23921f4cc652841cb49175304f106341b9590de46302b4fcc20b1ca34a7a", 1),
            // SIM, base line(s) 92: Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy.
            new SourceAllowance("Fishing/BiteSequenceSim.cs", "490ae0010b2b849d0f3a063f3ab2abe83916f8535954a6b4d1d8d1e547ba3dd3", 1),
            // SIM, base line(s) 156: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Fishing/CatchResolver.cs", "fdf4f9ab22c74e107b77b5808ea5556da2f0595772028b642f6a0019c49fca82", 1),
            // SIM, base line(s) 174: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Fishing/CatchResolver.cs", "1d09e5c40ba8a8a7eda5991bff0e8ef0bd7621117a6e0a410ba11dab21268195", 1),
            // SIM, base line(s) 176: Acceptable explicit RNG parameter; caller supplies the stream. Different stream history can change catch; no RNG is created here.
            new SourceAllowance("Fishing/CatchResolver.cs", "fff39d450bf10b0fcb007a5ba89a049d7633b347c6b3e3b4f014502fa605b910", 1),
            // SIM, base line(s) 187: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Fishing/CatchResolver.cs", "afdbed7d04f718caf007fe17ba93f0df42e36ca68cb324847a161079d49ee54c", 1),
            // SIM, base line(s) 191: Acceptable explicit RNG parameter; caller supplies the stream. Different stream history can change catch; no RNG is created here.
            new SourceAllowance("Fishing/CatchResolver.cs", "57ee111c9e1daf1a3922eab7122f747f816fb41c2719bd37159b3162370729f0", 1),
            // SIM, base line(s) 202: Acceptable explicit RNG parameter; caller supplies the stream. Different stream history can change catch; no RNG is created here.
            new SourceAllowance("Fishing/CatchResolver.cs", "c4e702d5c8dc170210bb24ec136ead9646f80c51e93c3989c2a7e51a6cae7997", 1),
            // SIM, base line(s) 296: Acceptable explicit RNG parameter; caller supplies the stream. Different stream history can change catch; no RNG is created here.
            new SourceAllowance("Fishing/CatchResolver.cs", "1729b834c38f67174db0d4d6b56bd1eaec3442c9e21a96ff90685aa345c1ed86", 1),
            // SIM, base line(s) 310: Acceptable explicit RNG parameter; caller supplies the stream. Different stream history can change catch; no RNG is created here.
            new SourceAllowance("Fishing/CatchResolver.cs", "10e83bc9f930e370bbb9af343450925fdef0ecf8e0290ddf154d8a8182126eb2", 1),
            // SIM, base line(s) 103: Acceptable explicit stream: default seed hashes world seed, hole ID and action game time; nonzero override and missing-service zero remain explicit limitations.
            new SourceAllowance("Fishing/ClamDig.cs", "aea62d8bc4f70b4e37705046c6d8bec0287731da9a146e2e5f9deb344e23f6f9", 1),
            // SIM, base line(s) 176: Acceptable explicit stream: default seed hashes world seed, hole ID and action game time; nonzero override and missing-service zero remain explicit limitations.
            new SourceAllowance("Fishing/ClamDig.cs", "a8024cda86cd7ea8999b9c5c726ff1632178611dd991f71c2ac9cf6deb2cdb7e", 1),
            // SIM, base line(s) 185: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Fishing/ClamDig.cs", "faeb379e92b5c49f7422c524734db087dedbe252d5e300a4fd5e033d0118e737", 1),
            // SIM, base line(s) 15: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Fishing/ClamSpurtMath.cs", "14eae98b2fd2f234fe0517b98c33d2e056eaf68e304c502baf60aad8a6a40b36", 1),
            // SIM, base line(s) 104: Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy.
            new SourceAllowance("Fishing/FishFight.cs", "2daf23921f4cc652841cb49175304f106341b9590de46302b4fcc20b1ca34a7a", 1),
            // SIM, base line(s) 116: Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy.
            new SourceAllowance("Fishing/FishFight.cs", "a4e20224a75f2c478ab19bd6171677d03270a8c3506568e6ea998a7b1b822280", 1),
            // SIM, base line(s) 107: Acceptable explicit stream: default seed hashes world seed, region and cast game time; nonzero override and missing-service zero remain explicit limitations.
            new SourceAllowance("Fishing/FishingController.cs", "aea62d8bc4f70b4e37705046c6d8bec0287731da9a146e2e5f9deb344e23f6f9", 1),
            // SIM, base line(s) 390: Acceptable explicit stream: default seed hashes world seed, region and cast game time; nonzero override and missing-service zero remain explicit limitations.
            new SourceAllowance("Fishing/FishingController.cs", "a8024cda86cd7ea8999b9c5c726ff1632178611dd991f71c2ac9cf6deb2cdb7e", 1),
            // SIM, base line(s) 26: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Fishing/FishSchoolMath.cs", "cb5041db335afb2c52fe1d677234e8b6f19caacf6748ee16b12ba78575ff4111", 1),
            // SIM, base line(s) 76: Acceptable: seed hashes world seed, line ID, set game time and hook index; equal placement inputs reproduce the draw.
            new SourceAllowance("Fishing/LonglineCatch.cs", "d80dc0f2ea613f937a25c3c98cf2e00a608dc0b757be61a5622d3dddc7563712", 1),
            // SIM, base line(s) 17: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Fishing/PlacedTrapCatch.cs", "04f33aaf5f605394b4098ba6c3ceecc4b5310622638274d074a43b57ba3c3a6e", 1),
            // SIM, base line(s) 58: Acceptable: supplied stream or seed from world seed, trap ID, placement game time and animal index; equal inputs reproduce the draw.
            new SourceAllowance("Fishing/PlacedTrapCatch.cs", "c4e702d5c8dc170210bb24ec136ead9646f80c51e93c3989c2a7e51a6cae7997", 1),
            // SIM, base line(s) 111: Acceptable: supplied stream or seed from world seed, trap ID, placement game time and animal index; equal inputs reproduce the draw.
            new SourceAllowance("Fishing/PlacedTrapCatch.cs", "88d9ad37d602cf1e2c5d821b5fe93ca291359a21411772d3a607ce45e488d6ce", 1),
            // PRESENTATION, base line(s) 822: Crawl flipbook frame only.
            new SourceAllowance("Fishing/PotDeckWorkController.cs", "e4466ecdc0e31164e6fcf8134cccba0868367ad1cd46dce92c8f6a5df7d3a2cf", 1),
            // SIM, base line(s) 33: Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy.
            new SourceAllowance("Fishing/RodFightRhythm.cs", "2daf23921f4cc652841cb49175304f106341b9590de46302b4fcc20b1ca34a7a", 1),
            // SIM, base line(s) 49: Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy.
            new SourceAllowance("Fishing/RodFightRhythm.cs", "dacf5311cd12e11fc2376b3886fb89a65cce7ea1257a72e274c8b1c260d2d708", 1),
            // SIM, base line(s) 98: Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy.
            new SourceAllowance("Fishing/RodFightSim.cs", "b65e9e72d71bde5803c387b8d6b4c06c042584b3a21cd8dab42c9da12cc94599", 1),
            // SIM, base line(s) 75: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Fishing/StableHash.cs", "5c4d77961390304d69b84395e778f60570a79b348303fd2223b153613cfd455a", 1),
            // SIM, base line(s) 72: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Fishing/TrapFill.cs", "2d762c7bb036bb983905c72c046e685f7b633a29bd23fc8b3b95068e926cd822", 1),
            // PRESENTATION, base line(s) 489: Rope lift/shudder picture only; no catch or haul-state input.
            new SourceAllowance("Fishing/TrapHaulController.cs", "bd8de11fd401298e313b0a2f11b0c6945a4431d24a1bcbcef55c5574870b6839", 1),
            // PRESENTATION, base line(s) 650: Rope lift/shudder picture only; no catch or haul-state input.
            new SourceAllowance("Fishing/TrapHaulController.cs", "380ce66013249c7ee3a974c7a0508758ae4d5328a721521a3c372f8268eac930", 1),
            // SIM, base line(s) 104: Acceptable required caller stream, consumed at setup only; null throws. Equal stream, tow and track inputs reproduce outcomes.
            new SourceAllowance("Fishing/TrawlSession.cs", "d5093ec9c79dd6515ae75f0e8c29cb560b8cb35c7a682600c154a97e86e4c979", 1),
            // PRESENTATION, base line(s) 807: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Player/DeckRiderMeshPresenter.cs", "56862ba15b4b6b0e8e875665f162e9642406455f932883214b27937dda54f567", 1),
            // PRESENTATION, base line(s) 816: Visual seat/ladder tween timing only.
            new SourceAllowance("Player/DeckRiderMeshPresenter.cs", "1cec403c2a04172676c5717682e5a2451ac95c77f6d56ecbc6cd17b1831551fe", 1),
            // PRESENTATION, base line(s) 100: Cosmetic froth animation only.
            new SourceAllowance("Player/PlayerFrothPresenter.cs", "030c0681464ab12b0b3d7fde3f73462ad646d7ad4e009941da9502440cc32bc7", 1),
            // PRESENTATION, base line(s) 454: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("Player/RodFightPresenter.cs", "e9f121caf2124191ec89e587c0a1fb5eace28b9968bdd333a3cfbae4bd274d4a", 1),
            // PRESENTATION, base line(s) 994: Drawn churn and shudder phases only.
            new SourceAllowance("Player/RodFightPresenter.cs", "f40d0e39b1dec542ba398cd2dfc9ad0dcebc513c86b5bb9695bcc90ce38c84e0", 1),
            // PRESENTATION, base line(s) 1267: Drawn churn and shudder phases only.
            new SourceAllowance("Player/RodFightPresenter.cs", "f4f2120ae288a20e463c032817ab4b3d8604260b54dbe1c587f3caf9c2f1e483", 1),
            // PRESENTATION, base line(s) 161: Cosmetic dirt/odour animation only.
            new SourceAllowance("Player/StinkLines.cs", "4406073d59c754f9e982f59e05e6f839b5bf126e89cbbf283976a0bd323f6d1e", 1),
            // PRESENTATION, base line(s) 22: User-requested appearance shuffle; rolls body/colours/outfit, not traits or world outcomes.
            new SourceAllowance("UI/CharacterCreatorModel.cs", "b2ebecd28dba12dbc734d4538d4b9c6b43549c4a3667f66337883497fad40dac", 1),
            // PRESENTATION, base line(s) 209: Instrument blink and waterfall display phases only.
            new SourceAllowance("UI/FishFinderOverlayHost.cs", "8a6e8fd3bba4d6cd4e8f3397bede294a99f16c55ba8d1d4940251c9de3930d75", 1),
            // PRESENTATION, base line(s) 212: Instrument blink and waterfall display phases only.
            new SourceAllowance("UI/FishFinderOverlayHost.cs", "d1245a9cc6e105193f40463c39385abb4006b207f1c0fb10df8f08a96b68b348", 1),
            // PRESENTATION, base line(s) 75: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("UI/FishFinderOverlayLayout.cs", "9dc9d4ebdb1a3647ba79f3cfc6e2ae60b1be8c4d3e1e1cee67c6f98d35e81a3b", 1),
            // PRESENTATION, base line(s) 338: Instrument alarm blink only.
            new SourceAllowance("UI/HelmOverlayHost.cs", "8899e97dcbb1439985a77ccf7c5248e8268f84e1cce359f9925e4e7a71fac3dc", 1),
            // PRESENTATION, base line(s) 461: Radar repaint throttle only.
            new SourceAllowance("UI/RadarOverlayHost.cs", "f2d963181d684e535bef52aa93d95ced100510a5e78852621f5c287896fcb1d5", 1),
            // PRESENTATION, base line(s) 462: Radar repaint throttle only.
            new SourceAllowance("UI/RadarOverlayHost.cs", "4cd3cff69159a6014108fbdc950d58b53c9f63c13b98fd4d30fbd2fd96055004", 1),
            // PRESENTATION, base line(s) 170: Sounder alarm blink only.
            new SourceAllowance("UI/SounderOverlayHost.cs", "6ecbf87b5ff8812811a5f2c38d373dc1ec7fc41abbee4db873fb0640d210a0e9", 1),
            // SIM, base line(s) 219: KNOWN FAULT: wall-time roster caching can change rank and the day outing at equal seed/time when discovery occurs in different batches.
            new SourceAllowance("World/Beach/BeachLifeDirector.cs", "2fb92a6e8810f46e2150c5f470583a5033f668e0a49b55a4754e55db07090868", 1),
            // SIM, base line(s) 156: KNOWN FAULT: wall-time talk timeout/body cache can change who is held or eligible for a conversation at equal seed/time.
            new SourceAllowance("World/Beach/BeachLifeDirector.Gatherings.cs", "5a6d098015c3e4fe6b8b1ca19c38fc3b2d6ca9f2f7ad840c7543a77899ecbd07", 1),
            // SIM, base line(s) 259: KNOWN FAULT: wall-time talk timeout/body cache can change who is held or eligible for a conversation at equal seed/time.
            new SourceAllowance("World/Beach/BeachLifeDirector.Gatherings.cs", "23ee4e4ac0dffe7b5da663538066fbc58e06fdcf6eef3c6f32b19e33852d18b7", 1),
            // SIM, base line(s) 333: KNOWN FAULT: wall-time talk timeout/body cache can change who is held or eligible for a conversation at equal seed/time.
            new SourceAllowance("World/Beach/BeachLifeDirector.Gatherings.cs", "2fb92a6e8810f46e2150c5f470583a5033f668e0a49b55a4754e55db07090868", 1),
            // PRESENTATION, current line 222: Injected speech cooldown clock with engine-time fallback; barrier forces and weather precede this read.
            new SourceAllowance("World/Campaign/CampaignBarrierDirector.cs", "f6a57df5bc90a7a79b01b703c67baad8a38d8aba5c8c5f4ce98afb3a64ec03c3", 1),
            // SIM, base line(s) 10: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("World/Conversations/ConversationSchedule.cs", "3136e528db7d4ee604476b335cdaae893033392201658c9c1311b564c31469fa", 1),
            // SIM, base line(s) 61: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("World/InnerVoiceDirector.cs", "3ebf114588960b10605977569e3e73dd519fd6fab7ef3f98d0191e81f08716c3", 1),
            // SIM, base line(s) 242: KNOWN FAULT: repeat-line eligibility and session speech history differ with elapsed session time at equal seed/time; not a catch/price fault.
            new SourceAllowance("World/InnerVoiceDirector.cs", "658843e014c8a99cda53d76c96b784a2640eb08152c6de6267bf9d422e469a28", 1),
            // SIM, base line(s) 247: KNOWN FAULT: repeat-line eligibility and session speech history differ with elapsed session time at equal seed/time; not a catch/price fault.
            new SourceAllowance("World/InnerVoiceDirector.cs", "262b46d866c7e2d3863a6c6ceb425fd05c40c24dfbc379f720b29494905453ba", 1),
            // SIM, base line(s) 90, 164: KNOWN FAULT: equal game-time re-entry can travel or refuse depending on elapsed session time; limited to legacy/arrival trigger paths.
            new SourceAllowance("World/RegionPassage.cs", "38f80cf360d078bde413fc57f7b80449fc537137534240045cb7d5ea956a5cf8", 2),
            // SIM, base line(s) 130: KNOWN FAULT: equal game-time re-entry can travel or refuse depending on elapsed session time; limited to legacy/arrival trigger paths.
            new SourceAllowance("World/RegionPassage.cs", "012ff6632f70c286635dd2983338f601ee82cf90be09ecec9e7cfa0e35d29539", 1),
            // SIM, base line(s) 18: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("World/Routines/RoutineSchedule.cs", "b9af98028b2897a1695eaaa6d9fb033a075d3e925def8c9ff8ea899011d39ef8", 1),
            // SIM, base line(s) 23: Comment or literal only; no executed read, RNG construction or divergence at this hit.
            new SourceAllowance("World/Routines/RoutineSchedule.cs", "6d50903d067bde405346e2dcdaaa8da6870ff3baa7cce72daa001509e5ddcaca", 1),
            // SIM, base line(s) 296, 503: KNOWN FAULT: wall-time shelter/seed refresh can leave talkability or a prior-world plan stale at equal seed/time.
            new SourceAllowance("World/Routines/VillagerRoutine.cs", "9e270409da314f38afb2551d3e61dc64fc0e1ffac2ecfb0f3cc84e66e72e7fb4", 2),
            // SIM, base line(s) 298, 505: KNOWN FAULT: wall-time shelter/seed refresh can leave talkability or a prior-world plan stale at equal seed/time.
            new SourceAllowance("World/Routines/VillagerRoutine.cs", "14441e68a3fac13111e2f19046dab773557eea6de4e3cfc7c6758ba1f887c80c", 2),
            // PRESENTATION, base line(s) 154: Refreshes the drawn ladder span; boarding registry and eligibility do not use this timer.
            new SourceAllowance("World/WharfLadder.cs", "defc09a24cf1d1a9ddb322f4d53afb6d2ced89e5845cc21bf2aa8fe8f12171af", 1),
            // PRESENTATION, base line(s) 160: Refreshes the drawn ladder span; boarding registry and eligibility do not use this timer.
            new SourceAllowance("World/WharfLadder.cs", "546985515bda98235d089e736da9243aff8f9192c8fa9e38f0051aedb6156726", 1),
        };
    }
}
