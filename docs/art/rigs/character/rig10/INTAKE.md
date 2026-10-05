# Rig 10: intake record

This file records each intake of Claude Design's character rig 10 into this folder, newest first:

- kit 10.3 (2026-10-05, next);
- kit 10.2 (2026-10-02, further down, as #918 wrote it).

## 10.3 (2026-10-05): kit 10.3 over 10.2

Landed by the art-pipeline lane. The charter is `HANDOFF-2026-10-03-character-rig-10-3.md`, a delta on the 10.2 intake's.

This folder is Claude Design's kit 10.3 **as regenerated on real Node**:
- The kit's own checker, `tools/check.cjs --write`, rewrote 248 of its files (below). Nothing else differs from the drop.
- Nothing in the rig was edited.
- 10.3 ships every file 10.2 did, so only one 10.2 file left the folder: the Art desk's 10.2 harness. The desk's 10.3 harness and probes replace it.
- Git history keeps 10.2, and the kit carries 10.2's rig frozen as `_2`.
- Rig 9's folder (`../rig9/`) is unchanged.

In this phase (A), the game's bake reads 10.3 but nothing is re-baked. The ten committed skins stay 10.2 bakes until Phase B.

| | |
|---|---|
| Drop | `C:/hh-drops/character-rig-kit-v10.3.zip`, 10,584,809 bytes, sha256 `85c3f19df1c4c0ac8ebcefab6b8bc3edc4549e96bdb3b6a3ae694eff62cb356d` |
| Kit folder in the zip | `export/HH-character-kit-10-3-2026-10-03/`, 320 files |
| On arrival | `SHA256SUMS.txt` lists 319 files: 319 ok, 0 differ, 0 missing, nothing unlisted but itself |
| Rig | `Art/characterIsoRig10.js`, rev 10.3, pass 10, global `CharacterIso10`, sha256 (LF) `ad563075475740cea1258de68d05c184f2fc136491357bc22988a10d810413f3` |
| Pose library | `Art/characterIsoRig10.poses.js`, sha256 (LF) `a28e5d92638d3cf1b26bbc49f887b23221acd3a9590bc36fd8b842ac6127c92d` |
| Checks | `Art/characterIsoRig10.checks.js`, sha256 (LF) `9649a0f01b96f400b7c7265d3d90e4cc4a260aded69cbbcc2591f964959bf5fe` |
| Frozen 10.2 | `Art/characterIsoRig10_2.js` (sha256 `fdc4bd9e20147bd59b4ccf649fcb32b755dd4964c7a451b204e7e5fc5c900d52`) and `Art/characterIsoRig10_2.poses.js` (`456f8afa09b31e7369586d86576caaa1d00b0bb3382ee91258fb09d73e560ec9`) define `CharacterIso10_2`, for the review pages only. Each is 10.2's file as #918 landed it, with one comment line added and its global renamed. The game loads neither |
| Frozen 10.1 | `Art/characterIsoRig10_1.js` and `_1.poses.js`, unchanged |
| Added by the intake | `INTAKE.md` (this file), `INTAKE.SHA256SUMS.txt`, and from `C:/hh-drops/character-rig-kit-v10.3-desk/`, as there: the Art desk's harness `node_check_char10_3.cjs` (sha256 `1512b513130312ea4f8d2157de8a7bb11440bced99335e60a40cad6c7bf58d8e`) and its probes `node_probe_char10_3.cjs` (sha256 `32799340ff2a90ea1454cb4a18e173dd8c1b3ca899cae227d45ad4da6980d4be`). They replace 10.2's `node_check_char10_2.cjs` |
| Regenerated | 248 files, by the kit's own checker on real Node v24.19.0 (below) |

### What 10.3 changes (the kit's README, checked by the Art desk)

- **New exports.** The rig now exports numbers that the game's bake used to carry as copies:
  - `TOL`, its tolerances, each with its use in `TOL.use`: the gate 1e-6 m, the inside test 1e-6, the least area 1e-9, the cull 1e-4, the marks' pixel edge 1e-4, the depth and shade quanta 1e7 and 1e9, and the depth tie 1e-9;
  - `DITHER`, the 4 × 4 Bayer matrix (rig 10 does not dither);
  - `INK` (`#12181b`, `#243036`), `INK_ROLES` and `EYE_WHITE` (`#e6e9e2`);
  - `AIM`, the look-at test: a bar of 4°, at 7 bearings and 3 heights, 2 m away, on idle's first frame;
  - `R7`, the 7-decimal JSON replacer every export prints with.

  `lookContract()` gains `aimBar`, so every build's `look` and every sidecar's `overlays.look` carry it.
- **The rod's bend sockets.** The four bend sockets (`tool_R_1`, `tool_R_2`, `tool_L_1`, `tool_L_2`) turn differently in every bent frame of the eight fishing clips, so none turns half round between two frames. In the five clips where 10.2's did, the largest step goes from 180.0° to 14.3°.
- **The sleeper** lies centred on the pivot, so the sleep clip's pins move.
- **The saddle mounts** turn the swing knee about the leg. The README gives the mounts' largest joint step as 111.8° to 179.3° before, and 96.8° to 141.6° now: the saddle mounts 120.7° at most, and the children's bench mount 141.6°, as before.
- **The face marks.** The mouth's marks moved at SE and SW on 5 presets: the girl, the postmistress, the painter, the tourist and the paper boy. Their bind meshes grow by 16 to 20 triangles. The girl now passes the face gate at all 8 facings; 10.2 failed it at SW.
- **The golden report** (schema `character-golden@3`) counts `bindBytes` at 7 decimals. The kit's gated checks pass 570 / 570 (10.2: 569 / 570, the girl's face at SW).
- **New files:**
  - blink and look strips (`<key>.blink.png`, `<key>.look.png`) for all thirty builds, in `renders/1x` and `renders/4x` (120 PNGs);
  - `renders/manifest.json`, now schema `character-renders@3`, which gives each image's `rgbaSha256` and `pngSha256`;
  - `renders/cast.png`, redrawn on a transparent ground;
  - `tools/kit.js` (every writer) and `tools/check.cjs` (the kit's own checker);
  - the review page `Character v10.3.dc.html`;
  - the frozen 10.2 rig and poses.
- **Unchanged:** the frozen 10.1, the 10.2 page `Character v10.2.dc.html` and `support.js`. The 10.2 page now draws rig 10.3 under its 10.2 labels.
- **Not in the README:** the brows' depth bias rose on every build (measured below; for the Art desk).

### The 248 files Node regenerated

As for 9.2 (#889) and 10.2, the committed copy of a generated file is Node's, not the drop's. Claude Design wrote the kit's data files under an emulated Node (its README, item 5).

On real Node (v24.19.0), `node tools/check.cjs --write` in the unzipped kit rewrites 248 files. `node tools/check.cjs` then passes: 570 / 570 gates, 63 / 63 data files, 241 / 241 renders, 319 / 319 sums. After the write, the kit's `SHA256SUMS.txt` equals the Art desk's `node-SHA256SUMS.txt`.

The write leaves the other 72 files byte for byte (the rig, the poses, the checks and the README among them), and every render's pixels.

| File | Why the drop's copy differs | How big the difference is | sha256, drop → committed |
|---|---|---|---|
| `builds/boy.v10.json` | All 524 are positions of the four hand sockets (`tool_R` 141, `carry_R` 141, `tool_L` 121, `carry_L` 121). They sit on a rounding tie at the 7th decimal (−0.05899205 m to within 1e-16), so the engine's last bit decides how `toFixed(7)` writes them: −0.0589921 on the emulation, −0.058992 on Node | 524 of 140,825 numbers, each by 1e-7. 1,411,626 → 1,411,434 bytes | `a466a3635adf73387049c2f754c521d061bedff275e53d808ffc3520e6ba4d42` → `d873791f0282fb2a8504f160d2637f894b511ee1a2f2ef8efed5335ce4e901fe` |
| `builds/paperboy.v10.json` | The same sockets, the same way | 524 of 140,897 numbers, each by 1e-7. 1,412,309 → 1,412,109 bytes | `5ea96861490530ffa4d5effd73a9826d79ffe4ed60be8a5cdc01ab6bf61c181d` → `57b11e0f78b0b8da28f691021fa0f1773afbf1c8259aa2a057a0b3662456981e` |
| `gameplay/characterIsoRig10.fisher.gameplay.json` | One rounding at the 4th decimal: in `castRelease`'s pin 6, the right hand's and the rod grip's x, 0.0977 → 0.0976 | 2 of 15,132 numbers. 210,388 bytes, both | `d1e73e461587ae6949adeb83a82cef51170e7537c50458f6e6985cf83c5d26ba` → `7d352586cb953b975eec2bb07b407aa8a293f49f6847718c79259e6efa9b70be` |
| `data/shading.v10.json` | The last digit of 4 floats in the light directions: `key[2]` and `fleetKey[0..2]` | 4 of 178 numbers, at most 1.11e-16. 8,792 → 8,790 bytes | `554b55023d6206a14101d34ae79ce5919089cdcd4630c4c149a819ba611a1db1` → `9945199448fca191c4f563b92a3d28f9beb6045eac9b48f3cb570b3a3ff5397f` |
| `golden-report.json` | Where two frames tie for a check's worst residual (0.0000000 m), the row names the other frame (`skeleton` on all 30 builds, `loops` 21, `roundtrip` 17, `contacts` 15). Only the `detail` text differs: no value, gate or pass | 83 of its 600 rows. 318,706 → 318,734 bytes | `ba7e9e0a28b3c804a63a6bf381755c83aafa118349d8e394773a27fa1cea1d0d` → `be45c8627fe3b31d659f60269b4c8b95bb2d53d2af77d695011a61ce15a229c8` |
| `renders/manifest.json` | Each image's `pngSha256`, for the re-encoded PNGs. Every `rgbaSha256`, the pixels' hash, is unchanged | 241 of its 6,523 values. 229,986 bytes, both | `b5c9d928a4136e17678bb5b5d42fb40dee454f25ced27069742c27c1d15e7ad1` → `ab7623f6e0116b12bba3c6f0264e4e293d3c0957732adb58f5df27a7f1046487` |
| `SHA256SUMS.txt` | The hashes of the 247 other files regenerated | 247 of 319 lines. 30,492 bytes, both | `9649560d09bb3878649eabdfb5f9e340d60d78b008b101e48051ed049c4d09f1` → `918131e55ac821289214b9640566a449acf1d5dbd54d2271b31b6dee29e6075d` |
| the 241 PNGs in `renders/` | Node's PNG encoding (zlib) writes other, smaller bytes | 4,572,665 → 2,128,879 bytes in all, each 5,955 to 19,948 bytes smaller. Every image's pixels are unchanged: its RGBA hash is the manifest's, before and after | the list below |

<details>
<summary>The 241 PNGs: path, sha256 in the drop, sha256 committed</summary>

```text
renders/1x/baker.blink.png  e9b6ae5c89ea81f40dfbf85cc0ccbac52acfaa3a2be8cd55ef83f1ea0ca7f8ee  974a7c46f87d1eace48ae827cb519a3839a2f9ac26623c90627c9a767574b89d
renders/1x/baker.idle8.png  6fd3817ca97c71c7206a87e544becce13b6b629b041f08d1fa6eecd0dab17013  cd194c88829164a6fc3fd853dc659d374e40facedc4a217030337742ac8ac782
renders/1x/baker.look.png  72820aca642565e47bc4b061b06bf2608916b88b831c682ddf283bd14b47a4a6  ae9b3f570dfea1cb84368a48e16a622ff3d721bc6ede3be3b7ce734246e71a28
renders/1x/baker.walkS.png  a1f3083c2cabd77e8bedd24db35736e1ece6b5924b2cbf9a99fa24724b3dc893  020cdcf051b4d53e1f50164ab21de438ee9bca2f09d97126af86559d6e02299c
renders/1x/boatwright.blink.png  fcbf11cdc1c9b4e0295b603a96c666971633027b0d39fd01cdd912c34ea2c35d  d894b841e8eeb2b55104408e60b218894e46dbfb53836fc4d42381ec967b8ada
renders/1x/boatwright.idle8.png  5759e53e96c6d396d410c9b2c348076330e00789a807df0b1876adccd9f37151  6f51795425f10729a5847da39d414f60ca21cacb903c7ad533a16c3d89b43487
renders/1x/boatwright.look.png  488e8d54d8ca6e93dcbf36901cfb1f1937434523bea3236dd55bc1d215c10b20  8d8323c570f58024caf4158a756c432369258b960a595a6d1e759b102fbaa298
renders/1x/boatwright.walkS.png  667f1d60b52d883950826925cb158a7c8735229f202a4f0e113b47324e085274  f56a834996cff9efdb321deb33a40eb9169d829ccbd6d16214681425847ad9a3
renders/1x/boy.blink.png  85b8136aa710350b4c3696f5702fece2e47517ad64e2f608029e2d14e2ece027  a966601dee2aa29bb3f2929d041fe28220c75cef7b2cb4fa960a8355f6d37966
renders/1x/boy.idle8.png  2c3fc991f6f58020da8600074ec287713097730b2dafaf7b1e15e233b5f33879  7f525fc1cb3655b0cdad62e103fbdcaa3c6a0ba67647f80473fafbeff1078f64
renders/1x/boy.look.png  df6e97353c5edc0928d6feba789fd23326b80fffd4da42b0a589186551310894  fff27575a800ab7343d54385669f3eb8549c2e895a542cc014d24063f82afea1
renders/1x/boy.walkS.png  fca5194fff05c0aeee76efe40b110b036969620a2e1033d21e1a1ae98d320e5a  ac5114a0af37e8ce1d4a34e99ce9f54d98791b5420c2c85e472ea17d5e9834f2
renders/1x/cafe.blink.png  6865139df557d40bef4b8b6cd0447692947873ba7656aab3bd79562d0196d88c  d89a1662db0914132396e62a03353fd507d80d88cf4c44a6a532c0d0f0aaf548
renders/1x/cafe.idle8.png  cfa8744f146e4327ef311b59e7a258a2b2baa218ed248c160aefa9176dcff018  ce3245c078f172febba7463ce399900cda038c31c248c3621f6cc4c79b9948c5
renders/1x/cafe.look.png  67d467f609989213928d644ce9c29f7b19b16db4554a9385c4f568d286278a4f  0cb422f99a6997017ed12a760fb0901d838c772b3a238d04549ccb415bb5db7d
renders/1x/cafe.walkS.png  84d94395fddcfae4365742af7a81a3ada02dde4e332eddb3de3bd4f501f2ae01  184c2866483cc2f07e9d846056814b299a39246e40dd20b5594927d3c9e0a468
renders/1x/coastguard.blink.png  911e187c2741afc0352fe345a8ad431152933573c3cd4e60b4f31b39df92da8e  a45e636df90f535ba866e0885bc910d65f44bf18e0a07f48fbc15718255421c8
renders/1x/coastguard.idle8.png  dab095000ab3276870786076a8ea154a6183f4f9621c555882c73adc013b972d  725e19ff722485a05ebaeff874f004b7e37a3db6092627b3e328b02e676967e8
renders/1x/coastguard.look.png  4aa6d810e4d88751cd42cacd64a76fe570fb69a749dc84663c478bcc886ca811  0506279a0360404af012dff209a978e66dfdae29d4ce7b344c9d50bede894d4a
renders/1x/coastguard.walkS.png  2c62585b95da9a8effc43148664445383799c2ed288422c17add55e60dfb1268  f3ab33bfa466fd4bca4d14a80b3e4146637075c0b99bc47519193cc72f89a6b2
renders/1x/cutter.blink.png  a5920243e255b5551883a1f5514bb58be89aabf55c0a30e66f334d64e397f500  26513661467f3be8e70211e798f9164960fdd18fcbf2b615faab3a0ce6e80372
renders/1x/cutter.idle8.png  871939e61a8097a809d07b97de622d663b05d84f3eae9874ab938f1c62f8e6b8  ffa89f8d56216a795773b5e9441092c66c0ff04edc525e85f63c7d08bd7a64cf
renders/1x/cutter.look.png  56219434d01000611abd4fb5b3f1b65f902e503d205658f1c8a7452fa0c97fa4  598e9b82e6d8cc6ff33ee5f55ed43b1afa747e90f7a04e740a84c33145097e89
renders/1x/cutter.walkS.png  1e542553159a42a20cc0d61a8d1858d2ed001df9801ce814bb0f74627af62258  e769105628971b7b1faee1434811ab96f77c0fd1cbfc8c7d5b582f1990aa526a
renders/1x/deckboss.blink.png  0cd483c0ad3bf4e5c29ae2212d94c4c92487d51cfc672aca5708865ac6876dc4  d4ddbbede2eec63f3c17ddbe38a3d11d7755ba978e6119ab9d2044143260f2a0
renders/1x/deckboss.idle8.png  3a6795b36112597eb84f1333bc732866a14c200488795be2b172a9c3d90ccc37  b99767770aac9fbd9944fc55fc520c606b616742402556148f02a493ba0c21d5
renders/1x/deckboss.look.png  958539fe32cd9d2e1d044635b502f94a05463558c770fec7f29bec77b396ec9f  fbe91f571c035500f32ff0c64e60b5fb28e0dfa623d63b14360ae3b863c8f8ab
renders/1x/deckboss.walkS.png  a46f20d58af4f8b4c0c3ffaf1ad96766f37eca79fc45c9bb699f473dadc4760a  05cd6118c6c555467a2b861628c84581043506e6d96743ca426e1f93208f4c50
renders/1x/diver.blink.png  6fd05f6844e9eb569411a805fdf78b112be9859396fe797b1237a64b95e057de  52efeed07e70b14dfb14c5c061100eec8839fdf8d483d077fd04c5f4e04ed10f
renders/1x/diver.idle8.png  8fa5ef5a0de3c6af727efc44667d7749e39a39e54aaac387aed7b75ad9417aca  e4f9286a6c01e91b40567ec2e6f65b40f461cfc150a74611af1a481da9572908
renders/1x/diver.look.png  637971fbb99607c830888c62422c25bd9deb7143675cbb396a3d1a41b160316e  e73ff9531d2c8bf39916c2159ffbaf4f453ac5291232262db2960537f2f94405
renders/1x/diver.walkS.png  00b8fdc84774d92aadc6d7f441919daa801182f3e828853daf183cb3ad6eb66d  e61e6c29fb5eddd456f1c46ad08fb4e79a594772c7d5a22d5d26c8a21c680b43
renders/1x/ferry.blink.png  eb29914757fe4c7787746ccd4ceaba8748d3e22a55652198ab2c50c587d00db8  5c563cfd93f0b9a3e87729ada18414516ce7f51ffc51b7908a59499edb569aaa
renders/1x/ferry.idle8.png  b33917111bf77e6cb80fdb22c2708d8eeac5e5e405af7b38f0931e8b22dc241a  a931cd019201349c4e6c184e5956c9b35aa2d17b85feb5900e2ccb42a1a58f7a
renders/1x/ferry.look.png  23f6ef8085aa9b0ce995609fc1b221189eed217c1cd1f5215f843cf6f542de0c  a68b326f89876fc6bbe0f4d6b16a4973a43645dcb807f7484955c1f62d995e41
renders/1x/ferry.walkS.png  f1ab7fc34b770238cc755d9edad2dc00da8a74b82d98834628545fa166d79567  120deeed33bf6a8339db14f8a5d4a0b575f84d202c28d2005c2f69334b5672ea
renders/1x/fisher.blink.png  9e69a5bd709d1da49c04621a8f51dca6d3e21d1d65d2b3a9ad98105cf33e15b5  f4d2f02d1a5e8acf68a57bc31da856e2d8a9f3ee7e87d2edcf7c1e4b5e21c6b9
renders/1x/fisher.idle8.png  9bb0933e374303a2cf1cb9836d944408c1c087e4e8c9e78ca2a22bc0bb3936c7  ca931edfe23e2a235d9f0d5f485c60e00431481b08c93490080d49e3aadaaeb8
renders/1x/fisher.look.png  e7ab1611b564161aa77dcb3df05a19c3e83d6950d8f9f2275677bed410460d63  3c67fe1abfdfbe08d6d29b46127bae82b4395acc3a5623833f6fcdfe0413dfa5
renders/1x/fisher.walkS.png  ee0fb4e79a8c57a7d433e7bb50e34b57b4f718536a703943cde23c7f6e4836e9  d09ee472e0457209289ef44bbedd205fb3ee5cadfceddd9b170ef2a822928085
renders/1x/ginny.blink.png  a6f526623447dd9887392a13ea2aad1fc0759a5eb2ffd64f8437b2eb9b759a2e  f9fd371680564413f758a73f49f3b68ae668c0f9354b483d6f8cd844f9d7ac09
renders/1x/ginny.idle8.png  468904483342541a303dfd16e505a755c21a5969bcb584970125f95c8e8bd843  dc528c02699d688fd675c89c3d219bd8b1c73895fe5d1709ee758280743e18b2
renders/1x/ginny.look.png  3abb74fbdf43da1d8075bc3b9ecc502335555e815543bf280d1a0f9883eadd38  1d3823ab286def12006e3f0f4830e56b507a3c39393207ad6a307bd470aee173
renders/1x/ginny.walkS.png  76a800feb9fc6c4db0996a20c3564157b57fd5ce8a5df4718006ed86c5aac978  eca07b573f312ca593aedab829823c5407c9654d20ab18a95305438a63c6ee98
renders/1x/girl.blink.png  7fb711f1525a4a4e44b75c1dbc305fc51606e4df21073adc95c4f365a70d8dbe  e57bcbfae413adcf30104053f0e47b85ba3ed296bc2d860fec3da96543eedd5b
renders/1x/girl.idle8.png  6741425a3c5cf759dbccb71ba770ffe416b60b7f9c504ff44d08c224199a29c5  7d28d2bf57496c408ae4a7384c09654da229ada071bb02504075bf8beb177f8c
renders/1x/girl.look.png  82ddb9b3f4bbd2e99787b1342471a4830fc3b7b86d829854106abf984a943ca3  8daa03abf6dfa3fcdd992109ebcf96fad112201f042a9a68ba06128157620693
renders/1x/girl.walkS.png  55fb9211f24c59dfd655d915842911b8ec245ae9503a414bbf4d0741cfb3bc98  17e53aff2c1f13e6637f28512d9126b7bf3e39a34e97255ec3ed7123420b12b0
renders/1x/grocer.blink.png  bd707405a9107f9687067eb168b610eb04f30f3673df2419708e7ee89515cc61  044cccaba5b8eb8038b6740675d0d4cb32307db9dc6440224dc209b3c182a92d
renders/1x/grocer.idle8.png  b88884371b66a4d442e901e3aa7c8483202dae5fe0a555bd7a7723cd9d90a980  1649a079c44113737726b0e682558b34406beede83811319c03d54c60e2ed59c
renders/1x/grocer.look.png  1d07046a4d6608ab1bb707b67357591e6490ebc75c7bbb9b2d73243629506907  42bec7d637b8680933652cba1aa015d759be4a9bb228a11dc09a2c602d609b5f
renders/1x/grocer.walkS.png  45cdb1e9416485b3d7b9530e17b3c91c427c96fe7c1bcdd70ecc5f7da52ccb2e  b1cd1a33b99ba7ddadbb8b38e42c9efe3f9e8e92772921bb523d5b780c08aa94
renders/1x/hand.blink.png  7705cadcb60985cb75d07bae3c2fa3ca2aba4878feda93e80a8e64f3b72b569a  705ec71666b5e5770f2844258c155fb8ccd9c79ea06dc9e2e0385fad82fd2045
renders/1x/hand.idle8.png  0aad7a74db19aa682ac560cc9bda60d298b4590fa90447b4650fa7f959fb94a0  afde539512139e2219d051ccf8f645c8013763fa76616684dce00d044c87c747
renders/1x/hand.look.png  b6406b9f04f5fe17ba0ee2875ffb140de895b049aed9102b929aff806469b29b  4f347544a90859130fee7c5a44c98ffc400d9e25872d7213e2860f8e252d0367
renders/1x/hand.walkS.png  c30b8a54baecbe7678b61d521d260c99a8edcdc66cf90de7070d33c468e42e12  63bcc8c6f34e36512cc213bade208831d8e35a58baa7002125e52b1713c820aa
renders/1x/harbourmaster.blink.png  1c371c2b3f627d6064c37899248e36c29573807ae055d281484117b4e556b054  e96844768cef5b6ff230ebf4f543bb9f0831e0a663341c64eefac4489f3d0909
renders/1x/harbourmaster.idle8.png  c2ffc434a91bfd751d4506d8dea21870424518ae104f97b8a7fbdae1392fd83e  7ecd7a1403d66a238477eaabf05880fcf84456bca37885d23f0e8621e0614f56
renders/1x/harbourmaster.look.png  bc96a8150c4603b84942dfa66fc6832c651dad6b3a34a96a20756d3f98e12213  65a153c9c7247aa16072950c7429bed25fe95df3cac0009b1c381e3094457a3a
renders/1x/harbourmaster.walkS.png  5e7b100e8b1c332a8287f68f3d7d4ed5b573220fdd2c5324f641b7f2bcf22af8  956d5b6c0466994e086ee155891df8a9be37f00f07f14f2819b74849fa6b0e23
renders/1x/lifeguard.blink.png  ac880c7106bfdc86f48a644935cf344249c032fab4c5cf48dc588fe2d7ac0544  a0a94268f291e8e68c49a3fc088267d2f5e358ba4f8289ced1214f0d19eabef4
renders/1x/lifeguard.idle8.png  e4eb953fb44beeea76aa63ac23578d1f8c272cba1ef5b8837c36b9e35febc2d2  9038040164bab65f41daee3ce555c43cc413820853b4d1ad38835e03bbbd0e3a
renders/1x/lifeguard.look.png  9500e6abe9852adadd737668d7184b783e7c39a08bbe24a919c2d49cb82e6780  0602452c46346c0cdec32e7cd759b675d5d783b03a2c0d55dc8e2be74ab4dfa1
renders/1x/lifeguard.walkS.png  98c774000ccbdf1d36ac51d1c92a3a3d3395bdccb59817a22eea0d4da04e904b  b4bbe3e7eac5009b63cad331ac3279bc8ead45b5e3143bc8bd1a88ba02c0216a
renders/1x/lightkeeper.blink.png  ed01145b818ae68b4bc32535d4257adf122d7baf85a053f610767170c55c6eac  04708ad4474022bdf3c9990efee61916674031c5afcdc3c2b5ef0cfd7c165262
renders/1x/lightkeeper.idle8.png  c6182a95e67a11154854372a279e37fc47deb071e93fe8ce7a8289b6e9ae784d  178dcb5950b874905927c291be5c0889aded72c8d93232986b8cafffd24bba5b
renders/1x/lightkeeper.look.png  4056ed7ee702c97dd6bc21a63e35ce30c0e88c62a1745b59500c0abc68a9f47d  bbafa294f2d391c89b297e940df27293e672c41e2f9a599e6382b3f0172699e9
renders/1x/lightkeeper.walkS.png  c5b14b13445fc1be67effa3fc0b37acdfb8db16975ebb5948313a562a285d6ed  268c1a7dffbc41aafbd5c5cb9ce93caa246e331b2d21fe3c2cfcaa62074b6194
renders/1x/lobsterman.blink.png  8ead7dd9982accee7a3cbd4b4ea690ee68431d499b3146aa98fbd1b3ca291c5c  0255b9e43928846a64f804a13649e6c1ad2003566928124185117d0e4c3f5711
renders/1x/lobsterman.idle8.png  1b08368a6eaf2a892592507599b11b72e8a242a9745676bb3733984e4010ddf4  66c98ef8488e84b3c21c1de1ad002fd32c39b664520e52ea8d49cd067d6e2ca8
renders/1x/lobsterman.look.png  89b78abec8a0360db34908228f6fe31250852d7f9add84642e7e07a89699beab  949a6ec124cce17524ac65c7821afb973391679a2e9d5c7f7844148ae3cd8f63
renders/1x/lobsterman.walkS.png  b38a018701799a16d89bbcfda6093eb97736333a12cdc7acf2f26952b1fcdf3d  4be157fb3e12fce36f00befa31372495c5b8255a6edee1c26612be42bb16b903
renders/1x/mechanic.blink.png  3aece68c91dc04774787741384868e2f3004fae9edcaf28b575a0c232b0e43bd  306fb2e1f1afd1982ac32e07318f1ab31daf62efa561ad19dd499f995fcbf4e1
renders/1x/mechanic.idle8.png  112108a880581e9f5e3fbd7692d7a6eb9eb7c78e9ec24cff26ae90bc5af70b8f  7cb541d8d5a126411015738f2abc0b01aeca9f681889c32f6380d4f3fb414885
renders/1x/mechanic.look.png  a6d8afb4312e39b530e5dc40398da6ee36aa8e239bb6a2f342c24f4aebd90836  7777032173e288675e7aad9f79ae5c061e54589d716f42dbabda1153c6121574
renders/1x/mechanic.walkS.png  ec140245d4cc7f4747692634f177addfdd3faac6cab81e57c25a4620a9f87dd6  5441d148720b7a3fa414f1bd1a082558010081f4d76c348e6f22a502b3b15480
renders/1x/mender.blink.png  0854ac9aa7465aee0b2002ec7dac1caa6a623f11a1406fc939e0cb3610c2e2b7  b457ffe4fd002f83f682500c9e5cc18cb3ba69d2b161c8a793c57d22ff748f19
renders/1x/mender.idle8.png  35398e4ea9fb32faac6e85883b2b4d4e743a82e41172cb669c612c94cb372bb6  e910d826d18a14fc7a4a521f774c17bb7b861f3c22566f17a1cc5c5c18699458
renders/1x/mender.look.png  1e5f9be3d0a44475452d23aa37f788981a8a040474771d1cbcd56f1d7084fb24  85f8d25b04d9ff09d606ffbed52b68470469036153dd7c3b5c6d9b789b678cf6
renders/1x/mender.walkS.png  8f5c894c3fbbefbbde9ef1143f94e5522164efa6ba210390953d3e25a76c2647  83e6a4ac04889002697ab0b768f0e6d9ef78f2fff46bc0cb1e010280cfb7fbcf
renders/1x/monger.blink.png  d0e2f1b60ff77195dd74599dcbe342525a193e4ef286ab685754ff798aaba843  b95cbd69394d5f5430d793cee119c52ebebb41574f63ea2eb1aa16d80e8f14f8
renders/1x/monger.idle8.png  4bd8cb675193946d89af521728018494126e12d3cfd13c54839b27b1532128c7  b8d80dd42067ede60d342fa54fec977a214d877a31be85c533c44e4df76f97d6
renders/1x/monger.look.png  d74cd41f9c244beea86a9f65530f74348f6a934b4eb4843faf04a6ebbc2fc3a7  7c566c5453453d13f1bd3598073866f0cdd1ddc2b4f00836f17962f4d4759df1
renders/1x/monger.walkS.png  9a82907aa19f9a8d99d800441dec48267f1a85c563bce100837c24cd05ad726d  84b01243f0dcc2c8b65d2a98ae8a5e1dd95760a48bdaf4f24516bbf6bc1b7fff
renders/1x/nan.blink.png  97c95c1b1ba09b2ec2d62d6b64005409b7b78a569913cb5204909014d9e46b91  50ed43e3aa92d77280891a5f35fd287a7dce9d4c2838e9677564a32f5b44d38b
renders/1x/nan.idle8.png  2fc2f9236fde01989754135eaf774283cd749e4db414ee57f9db3cbe2380be85  17b3d274b0e9b860d0000da2a048111cceb7b96744c6dd89176013f2a4c6cbbc
renders/1x/nan.look.png  757d46242941b0056600e349aca2d67513d73a37cf2db7a23b9d755e1ca4d33c  3ad9ad19a16d59dca654b9ff07e7bcb71339a0377f8bea8601ed38d09036f56f
renders/1x/nan.walkS.png  a86ee74c34f656dcd37c69b65d6fbb5ff091bf950da2fc939f9082ae6783856c  92e6cdc4c6d97b4584e257c43ba2a8d46db86a6ed39da39ac98de62076ffa229
renders/1x/oldsalt.blink.png  5ec9e243522765963a9b7beab30bca6732bedf43742a5f9edf87aaf5e097aa85  c9961ee415383e26b8ec7b3ff3b36ab74dcd010a8a7f6cbe0d1e5acb97c05894
renders/1x/oldsalt.idle8.png  ebf86fb422a236a6af4a64bac13ce8583f031baf5e5fcb319d549d127936a1e2  b4ae454dab62ba85b19ffe1eb61eaebfe2b57728259784c223c1b6a1fd382fff
renders/1x/oldsalt.look.png  a49de6e98e083666d0672d63e390b4409b42e278d1ea7e5fc70c19e7f84a145d  31cb1ad43d7973a1a31901c979a2734aba6b309979ed73b9992c92d38aeaa112
renders/1x/oldsalt.walkS.png  55524be960dac5d0431b0c2c36395c6924300534d3bde4b28898a084b4d22cc8  363f17b81da4074ee0e6f43cfa3a0b15a73877afef84d980197093e8cb886a37
renders/1x/packer.blink.png  f9a8e929d4bbda15fc840d7875537618d025fe6eb549af71869bea4ffa486a79  5cbe7eecbd54fc5b1d15ff0061952094037e1b28e97caf89ab5c2d80ffd496f1
renders/1x/packer.idle8.png  9e314d454170d7d066e0253def8a1bb03c12276432aaf860559b5cd0c5b31ef7  9a0482c8872af2543c7c8bbd987bbda37de5172b77656c2e75f610ea1d71c8ba
renders/1x/packer.look.png  35b040eb7719f0d66fc8d3a0bfacdbef9190cdf78a0247447c4f58976a8508c5  77e92b34ebe92efd241614fee2314272f3c402f1228bf915d4015341985be4f8
renders/1x/packer.walkS.png  9a6842a84bbbb8475a4dad9fb7b9c251ca4d3ddf8b5010bbd1675a93cdc1375d  05eca865c52180816bd15d03e4509d4279301327a1de0ba79b4f9b6037bb385e
renders/1x/painter.blink.png  c4a83c78478eab2f35cf636fc3f23a0ddf90b0b6cdbfc12f5cabf33f95e29fa1  40fd7155b2e5991a5a31e52dd2980584b7cf0108c240c9611773db93a86d51fb
renders/1x/painter.idle8.png  11e935ff9a858db8033a2a94243e93ee3d0ced4bec2341f2a809dfd3cb8d3f2e  52a5ffe191891fced1afb2edcc1eaf6915f38e9cfd72b3472bceca629cf28179
renders/1x/painter.look.png  3045f9e571e2fd66d2f9114e8b3593ed0cc808793cfd44f6905da7d2672eb59a  f27b6025bbc868b8eda0d6ed5b25c5c036c63aa378165624193f8037de1365a4
renders/1x/painter.walkS.png  22fa7fd16b59b704182e671ec97feb7a561abfa6229d4c6d8cc33382a427d5c4  18f0b0ada66ff71c77434743adb6cd48a1e0d567224d00c9b4304b95078c50a5
renders/1x/paperboy.blink.png  2675a3e361c8ae071f59be7f999d9562c0901f34e6f38807da816a6dcf99473c  3b1b7ceea741e640f13d24ee79afc4c81a59697eddb324fe8bbecc7da02f136c
renders/1x/paperboy.idle8.png  29d030d0b850debdad4109002ef6d74e7bce393fa1dfc1ff722e12cd4972e478  24c45cc004f46495a364700eda9073225f97050e9291b9913ee945dcf9a11f43
renders/1x/paperboy.look.png  0d7468b7d2fb2f8b8fd8f02716106459fa3a4206324dd20b55b5a16ca0cefb20  ea4b57478c5216a37b968a003b7a5244e938a5a473857efad87f1da29f4b9c14
renders/1x/paperboy.walkS.png  c8e77e958eb77d14e1293f582c547e973c9e3cb2773b01507a35e4129837ac61  ff5bb477a8cc16389f5c7b2bf4e9ddcaac2f04db55207327f7d3d344b24a1c4e
renders/1x/postie.blink.png  3819360ba17341be951f79ed22a0c280603271d685d102ac3f554c820fba6045  2008f6d6cffbf67c3d23bb8bbae208c27e763c8140de3301b88e4e2d644d3793
renders/1x/postie.idle8.png  7e9c9888e8c695b0138b34a12eb53610a5840ecc0e35a2a63882c45223a2743f  33cb51440402b022496912123ea97311f90c0cacd99009250fee6263fbc7df71
renders/1x/postie.look.png  601c090271fc3da63be5e4bffb475a18574426d08947b0955c6fb536bb82726c  691f2cdd46c32e3d3f11ef374e119956176a362ee596b94ade3f5f4cc0113fb6
renders/1x/postie.walkS.png  54b45f8fb730c9932bc20ff3b9040f6794bf00e30bf84e3b78ee4716bb413e2a  4cbcba5742bc6fb339671c15a2557f2f4d58394c72ecae7eec848d46b2c5f7ed
renders/1x/skipper.blink.png  fa4d7c9a3c6a060f0bce754e54df83008247c152482c68663c6655cf21e4a8b0  da18cf48a83e2b245aed1d6ae7247ddb8e99215b003937a3fc57e140f7162f6b
renders/1x/skipper.idle8.png  d517bbce6aefe9e4cc7ad1f75034045a68063f24acc2351aa7327952bb769b70  db841e95cf38c4735ef1b56113b563ab36f8640bb671cf9f56963fe5da9cf7d9
renders/1x/skipper.look.png  18372a786cf4eec0a4f89bcaf9f9ae0e56c680234a2d11e294e5e30d004f0e6c  64f94852a908de0237b1c737d7eff0e7912d5eef05cc6b4dc3c26438c327532a
renders/1x/skipper.walkS.png  20aff9a20a696bb29da32ae92c9e446e6d6fcfbdb07ad74c161832e5b6525e89  9875301696194523b144d6547ff5719cefab99d06024ba7e902278b068bb430e
renders/1x/swimmer.blink.png  f21d6010ef09d274815d420a1a2370a7fb2654e6a004415cb42c0ae82c948068  865107a1e7deedc7f922c19e63a9c5ab97f26daeda4fe493f28086494dbbef77
renders/1x/swimmer.idle8.png  08ab70e89f6e5c449bb781ecd24930c7072746974ed8f9c4269fa61f9fb2ee7a  7f4abdcfafd0e19c683ce3a1789864ba0f1193f5a4bc1d576bde8491153bf5e6
renders/1x/swimmer.look.png  23e1690c384da4b99e25298aa3a82348ff64755fa76a7cbbcaf2ac2ff9394e45  d666c3c29cf6843286cb205b27666018f030d6b77612190a9e82894bf76aabd5
renders/1x/swimmer.walkS.png  ae688eaf96006dc226816a463b27aba27a524ba551be5bf24e38d9b0bf981065  eed38174301a274d1edd9c8922d590a00cc8e489a7b841464058d2aa1d7fe0bb
renders/1x/tourist.blink.png  6080f68a6a56d1978d6c8d22a0c3b6b8b4fa6ae9d33c23c7f7603b0832c30bb3  9b73ff2e0dc3ad034d3078e39b5e93d84efd30e334acb264e2f4e9fc21f19e01
renders/1x/tourist.idle8.png  5378bd7bffb740fac2e8b674029d6727a57807c718d4bca45ee008e07d163a09  5149e1dd168ddae62985f034863782ab3fc0359821a81b4866f1fd52be181c3a
renders/1x/tourist.look.png  b7ba2d3352be00b245e26a28b02218173ff46978a0223fd3df3ab2115af75a0c  d8f44acfe717cdc126ce4f23739c4e04db990c203162dc5a0415bf8f1a4eb3cd
renders/1x/tourist.walkS.png  2c704697deabf3fabb325b698e4488ba96c17c6f8fd35f6b4a35879557b2f82c  054f84103a1362ec23c726cf4f434451dd32538798bb15ce0f73f0ae85399606
renders/4x/baker.blink.png  aef6ca364a7d5808fa03c01eafe6aef217ed06f8695bd74e8486b0ab59f4ab14  301bf3ca6738bc89caa479ffc4b8aa561894e1191b58363539ad2025d9fb3d28
renders/4x/baker.idle8.png  405297117003673ca4b24d9826cfd403ce9f389c9b3fc3113d38cf4d8fc7a2b0  c9a798b06ddd1a907a7790c64a172aa1af11a1368678e6307260d0bf5241695e
renders/4x/baker.look.png  c24f36000c2329bfd797e15d28091bd3c9501ba094fed79ba761a16423969679  5f9e433cbc849e2012daabe02739f4d39037ecc735e3f6eb2f7a0990c2a0b0ec
renders/4x/baker.walkS.png  761fd63670b1b053c32da57a42e5b92b7525cb6bcfeb6fcb5528d1d665001207  57f17bd93bcfed1aec4e3929257d6ae295daef33b161c5198a5e4a19989e902b
renders/4x/boatwright.blink.png  438614ae04d579b77b09f577fee9b4515dd4cc6cb5a90626a6060c8f75dae379  214d2332cd0adb02927c94253a0c457df4672114a12db5a2ad5daa8c22148ca0
renders/4x/boatwright.idle8.png  83669ccf39561f39601d0fed645829c3c31465187dc2e19c5999b89c3a80bd41  ca3c46a3134dff6ce61eeadc65be30a0453fe9dadf30b0f6de267a8b7910f255
renders/4x/boatwright.look.png  e4aac3bfa09824a22f2204455a46e21a0e1b4b2fbf37d9586d650ec3a0597d2e  fc42bbfcdc55b535946e1999489767c2493c8db36278ce2afc27cfcbe0ad113e
renders/4x/boatwright.walkS.png  88c49a00550038a3ce012256510ca18fa2de73d921519df72d817c79d309a758  88346d6e9b7be3cf464277db7ca9d9d730cf7d638d1631662a082300ca353439
renders/4x/boy.blink.png  c5cd51a12b62dcf74218b2d01d9f27be00c52d44094c544bc4283e95b2df92f1  c7fce118e355e6ddfacb34824ccb688c08d817357148db462951b95554afe767
renders/4x/boy.idle8.png  ac66b59f152a2b122dd17210750ab8f7663d669d648755795cd997a24e461019  c2e23bbcc50c5ba232e7449aaea051b3413ba80886b994182c19887d72b09a4d
renders/4x/boy.look.png  96e802da64f73def4658530954b9f80e573b6d08447a62e437378c70dc31d30d  42a2fc4065b89edbeda71a8372ca33acd005ba0af46f96e406d5eb9b41fdbd04
renders/4x/boy.walkS.png  c201c23475146baf7a35e9bb46630bdb021d1e07d9fa033eb04e3ad9ce4a5be8  2519c643b844de388ed456144548550f32146505d7a436237699e80f73e4e799
renders/4x/cafe.blink.png  849949b3fba96009e8f71a101098322a0366f040ccae5f5609d84bfd12733f43  4f300a31caefc085073a85b978a4be0f2da377306dde3416e45df8cf9ea99d4d
renders/4x/cafe.idle8.png  bdd35f4a2adfeb93b65f3fa3f7ad8d257355ae4e9443de1066e6f3f4dda2f9a5  f6dcc4878c804b62416cc4f7a2343a62f74bbb1fa03a1afda129a39eb3942b2b
renders/4x/cafe.look.png  4009121ab836cff4d027516ed61b72f9f61624bb15b28d0a4e962003bc871219  fdfb91779a2039503ad0d3f13ebb72dd18bc185e33f5175232d160e135877757
renders/4x/cafe.walkS.png  e5db3d605729c977e1eeb19e81aee47c1efb0892c52e0463ac68c4390fe7037e  6f73f4ff98fcda7b9b82e1fb8a9ff61387fc8982ac44801e8517415642355652
renders/4x/coastguard.blink.png  9dba60da109ee307fe43f13da22715097ec7e28193ef71b8dbae8c3fb48be9ee  603019a227475d95edeaf3de97efeebc643904938bd00b7890f9984c570d09e9
renders/4x/coastguard.idle8.png  2ae09bb9829612812c0c2d384cecb19aa53477e947cb72a08b9c47bf57751e5e  846c1bc49f92c485961775b547c95eae2958c749d28e66936bd0ee04f90158f3
renders/4x/coastguard.look.png  a8199aafa8602904d1e7abf6616c18d9f0fb38a55307fe0ae3e2ec3b739765d0  2259161ab322947ae2325480429fdb4dcd76045190120f2812b76746b87f734a
renders/4x/coastguard.walkS.png  69c3a8c701544ade11ee38f419b42797f0feec509c6e50ff4c15ff0c951a8c14  125a36f22d7a8ae21670cce2099d04c5ab2a1a6a823d9dc3cc8742405fca4dd8
renders/4x/cutter.blink.png  d52c9ab4d5a0cf7c61d16eabeadf3fb47d2b5f79de1f34143642fb4c058672a0  534e9e088b4b626c4deb595831788bade6c287f2431964362e41530aeda903c6
renders/4x/cutter.idle8.png  153b6ed64602be3c894687c27eea7e0d0a79ebb06c32d139f28588c2f53e45fe  ebe8a559561619ebaae083b118a4e062b705551c93900ac2b0b960d4c7efcad5
renders/4x/cutter.look.png  f4b75bb5306b079dc37d987babe562da26e919bc89b8adbfc31cbd4142f323f8  c963d20002797a03af6633fbda9be5b00c1d7efcb364e1fd7dee1e6b3d203494
renders/4x/cutter.walkS.png  2efa1bb45f0150ab5df029fa016ad6291bf181db50a24ca496b77040d9ce3bd3  45e2d364a0ddf2b384d2f7cad0d7efee2b9bde912b6c8736e2af013e644ef54a
renders/4x/deckboss.blink.png  cbd3b85665d039902e1e474f4281124e4113262f94488938706125e5ef2ca0b4  25a40213310e60a6a1eabd9e240100979b9b3746e75717077bfdca33d727e1f7
renders/4x/deckboss.idle8.png  a074090a9f0dad097f41edafbfdd3e54a165484f7a1b5b2e0e4880bf9fbd8330  46b8a30f3e27dae617726fcab42ebd89f4a5c782da28ce5fb7508c8e1cf4841c
renders/4x/deckboss.look.png  f5f78c8003d0ebd3819f1d5517576be54e013b2f5a1d10ee5111c47f361b3d96  5d6dcbcb4abbdf99ce672aa821dee31a76fddf8abf02adf0ebd0e41f7222486d
renders/4x/deckboss.walkS.png  7b3093baa185ff6d598d0e83e31877d9acee4c52a162014943d47efb1a2680ba  6bf27e3a82f3116fcfb4402166a6d5347cf2690b107c127153a8b154ca272396
renders/4x/diver.blink.png  4c147f37bf7cf6b473a7c026fba0c50110da2c58ed8d457ef82844278a4142c8  476ae3036e62bf5a859da2ec8bcdc3d0ec2dedabaaed5f8e9fe00fbfdcb30b81
renders/4x/diver.idle8.png  94022762b81bbc55f5d56dce11e6137a86fc2bb380bd693f2064a3ad19586a34  3e077583af53c6ac50ad9478bd56c44d484de7ed4ade02149d54b05cde84dabe
renders/4x/diver.look.png  e4393c6f35b5c49b1a63911c2cd396787bc30ed5212e8f4c83296a1d23290698  41cbc4c83ff54797785b04c4f07bde203c117a391ea11055939707002e3168c4
renders/4x/diver.walkS.png  28c558a10156c1fec797161be21c8747b408cd2c5a16609cb450bd9a787aed9e  81c14af16650916d26837e299ce93c490b531dfdd67d284207718f7f38eb0d60
renders/4x/ferry.blink.png  e553e752d958ebbd769a0ba6cc2a161fbea6df874028e0acdd627067525ea2dd  1809d0b4fd15d1b0af5ca7bdb2a918f4f34690e3c67595192a0e9d6bdfcda1dc
renders/4x/ferry.idle8.png  dee1c6748a50da327f7be4e231a7788dcf6490e0d261759151a2001ec3148188  ba9d4ee80434efe21b10d7182fb23d152501e9f63cea253d6d30e3ece09e3ad7
renders/4x/ferry.look.png  f583ea91d36a57abeb7a9c937f23ecadc8045eeeadfe938155c59aecf7fd4d48  01d54a21596f8fdc2c93e4399fd21a649e3242542508a92d1c956b8117a73bf1
renders/4x/ferry.walkS.png  30b4968dd44a38385867e8cc0172eb56ce19903cd2489bc109f88e7657003468  80d1384d599140d4cd2e907a9f6ae37afae7c0773ef7611c7c084ae46539170b
renders/4x/fisher.blink.png  880b368c7d60d5f4645bbfc4ebb68e2c9a95b4d8a22d7859a13c0388488670c2  0a9ca6ceccbdd1a13847599a144e115937df8377b6cc78ffdf884f62f7b87e06
renders/4x/fisher.idle8.png  56732461456c688b6fa3f28bd9247b95a6749848c0e15f02de07dbc323f92852  602c6aa55379969916eace88dd5336cd4be1b800568d0ac326cf4281890f3ba7
renders/4x/fisher.look.png  8dc983a401e41db7ab2bf88b6b3e7c7045b029fb4d880837f91a89da702a07cd  4ec34ef1b04dc4ddac4e83a5460f7e1062052e312f6d32077624a243b4278a9a
renders/4x/fisher.walkS.png  905ab3e8ef6b1b38553308ac99c578c00c6ed1d9a5863816fac48713097e20da  69aa0a71b132cfc1f787bfa6085a96310da9d94abf2af1fd1113c86431aedcc1
renders/4x/ginny.blink.png  8e0f62ef9bf1f1df22599cb9cae1232a5f784ae635c36bfcb319b8f8f7e3f642  910ff1009ea5f5365f202e6b23a3934f2ee80f51ce3e321f404f7e3166a40d21
renders/4x/ginny.idle8.png  0463a1ce20b026774a81129d587913cb6a5560441f71678c1f28c8774b4eb5e4  f729758e8b8a9d5681ddcd2a6e992952e73a2f9779b0af2e2da0639d896cff52
renders/4x/ginny.look.png  05eefb5aed7f42e802b1dc45a1f4d38b9e3ff6765f681a0dd3d0297beb6ec146  a22401e332778c8beddc7c7283cc4c5e196562311aaf7f13ed5b4a1dc58b83b5
renders/4x/ginny.walkS.png  a89aee3b2967b1b03187e174ea6fa27566d2c460c1627cde1243d0df0473b96d  a5d49c0d7c52a760f5210b65d1ca28a0e1b2c7c792d20c78eca22c421622a3e8
renders/4x/girl.blink.png  613180d7354cdc6eeae2739ca0b4ca9937796ff44645bfc09b7184267c604cf8  fd556ec91d73c55328f33045e1106d28cb4d8f615561b0ea30c956a17aa6ad07
renders/4x/girl.idle8.png  5ecde8daefe360cf0ca2c1f3bf327429aa54b18a4ff2ca2defc2ffb8e3eaa87a  3d78e9c07d6441b9869f264642793db42f77b100976cb112b9e51214f7258638
renders/4x/girl.look.png  e4b028dc8ea279615daf737e9eba05b0941e8aef84bd3897549d6f85867c89ac  163ed6ccce6fc61d2a38e35c0f0fa9543c311d42f2e2e481589c5d820bd0d135
renders/4x/girl.walkS.png  53f616e6da4c567f15f83e6de8300d86a8cb0dd3b87a549a0b872ca3110d0673  063539cfa54b85a4c8166f76cddbb6885ab1c66e32f71e7b9c6dcd0335fa161b
renders/4x/grocer.blink.png  854cd99bd3a23ad90a9352d9ff969d79f5ffca990bfe4f84a2dbe57491d7a17f  0e9fd00681ead730a484bb0f8e77fa39897c22eddc46ad01267b986438ac84cd
renders/4x/grocer.idle8.png  026cf00397302bd1aa3f7f80bf7350087db3f048166eb5c448031610d23ee205  7aa20837362329f1b4bca5deb90001d570f61cbe119a6e15ce44b82aeb59662b
renders/4x/grocer.look.png  73d8e78ea6ce4baf6969684c6bcfa0f60c83d7698b7b74f4129f27b5ebab9d51  2ee8ffc0ff1baaf2481f4551190ec6499e032753f33204864ff146affcefedc8
renders/4x/grocer.walkS.png  e43b7ec7b8992647e537a67b57968cf94fb2cb5d0859e55d2ef97d20cb1ddbf0  267b7bf46a5d77e0a32c8496c00d4f975a93d27b56836b3737a692ec7488877e
renders/4x/hand.blink.png  1d87b7735d409c8fb8e1f33c963e2e2b0904c9caad1af82f5c637b0acccbd5c2  50a89f19fed64e7ee72e0143a2de3b38ab036bd00b4da639d04b6b1f8f263ae5
renders/4x/hand.idle8.png  70bc17a2cbba404697ff8de4e426e38a61b8b594eb51fc463b8168fbf3b09c67  8ab71ce785deec8abdff87359927bb2f09692c6a737ae0c59045874f28ab7bd7
renders/4x/hand.look.png  4e64be476c89a7acab64bce9a6b6b39bb1fcfe6dfaea8a4dc377d915b50484c4  65c923102e6e10179d80cfd5969d998f7132b801465b992217a22adddd9f5e65
renders/4x/hand.walkS.png  6bb45b6271e6b3c3745ef551acac83dca0e23c8ed54273724113496ab8203977  94bb6bb082c297384c18878899363b96417122d0dc9f651e2fdbad75c9dccae5
renders/4x/harbourmaster.blink.png  08ccaa6adfdc06c05ee65e1b1a055df0671209ce74880d1824696d31a9cabed1  80985f55c1bf7dd36d1d67126bb90a9ed2e4d9079499747d6cc24a0e53822a64
renders/4x/harbourmaster.idle8.png  c710b55418c09b48255acb648c342fcfc40c6f061a688b32e8710a392b8b09fe  f97ebc048ba9ecc05e07638b71f3a6f6e7db2c93c5581042ea5a80418b426ec9
renders/4x/harbourmaster.look.png  9ae9bb8920020c10d3dd02bba30f04dd9c87cf5789b70f3b4fb7a33e0c7e198a  ebf88dad459c0cdb1c071339319626081f7cb50ba0117d003b068e56adb3e402
renders/4x/harbourmaster.walkS.png  2630ecb0c08b11b82e4a2f44d53950f3654abebc516a600481e4599c4c3a65a8  47360921fd536ce1554eea79902f339c32aa33316a4e39bcc0973dbdc54ae1af
renders/4x/lifeguard.blink.png  3d358d991ac1c0e483715e960154ee77009e3dfa75f84fed903b7d9dc5c80602  30b138bfc95de80a0fb3d83b3d34395e8c38aa29775e66a6ce8b149c5279e968
renders/4x/lifeguard.idle8.png  56dd142ea9107b65ce5f67941e9277132ac86f4ce15d5769d6a21c8ef59bdbb6  8beb037ff07a71a4eebcf8b7d4ad355d5a018530be6bcd2380739e8e3762d834
renders/4x/lifeguard.look.png  b51726b43caeffd28c030ee15995409231a8b83e57be34df94691877ae33eaca  1c8dd6d58c510c20303fa4c742a8a4797e01c7c55c9f9d4ed10b4296bee92ab9
renders/4x/lifeguard.walkS.png  e4878c761297c1cd5644fdd0bb37c75e48c922457150b97f4f89c3375221da5c  c19d19a0ef2a94d338ebe374bb56a385c5cac1e9d4c69e745d22d355b5bb2ad3
renders/4x/lightkeeper.blink.png  672c1be8630fb369173778e3d7aec82466ee3f0543e4048c4c2379efd4b827cc  60bc3629c03d76ded75439c7e2e74578c4b888bf3dbef246794115941b9c4398
renders/4x/lightkeeper.idle8.png  69782a6ed716a8ce4424c06623beb6fb8433076c5af238f87b486d4e2f3b6697  3c47c6658d3a8e8ab8088e0a258c2445882a5f07d264bc411323d7cb898f2be4
renders/4x/lightkeeper.look.png  29cdc87f32260360def45d21eba514f9685286dffacdfc354708a5e73ccffc2e  24c9281f176bf39ed2f780d655aaa990b5866d352656a8bceac7932b392ea10d
renders/4x/lightkeeper.walkS.png  f001890f4f1e7ba3c06335934780e6ff8acc324a7544eb1b7bb1943d69bdadab  03d9667a97fdd84520238b8f9b76f805b1bf49d3358d8483a48985088a6e63fa
renders/4x/lobsterman.blink.png  8f6d259d705876324d618e6b74112a7d60d2e507a064f1f42267d0dbd64e7044  4137bed10397b57d84e65e30dbcd9afa6f71002545601851e7f3c3192980abdc
renders/4x/lobsterman.idle8.png  95326a2a9efc8a988ae609936ef440a2044a6aff31c6af895063e8a40e290a22  f0a7dacc0279372c747beb27d59edced34a63ca069162be33dc823dd33525cbb
renders/4x/lobsterman.look.png  2abd767ad4d5287b44ff36d83f180ceb32d4e22f8bc69a920f319f3b44df98c6  d86511a4624eb5391c5e183528779d9cd24276b7f593ccee57db5a3eb6a3b3f9
renders/4x/lobsterman.walkS.png  6e51d288637d4a067f155ac4504a8df6cf3c66db074ea9e094a56bded7ce6c38  e2b82662473b2ac1ff482fb355327a88e629d669c822f369a464cc7a7b3fbb28
renders/4x/mechanic.blink.png  c49509922ab51b7db4961dedb45b3a0ac68655c0354eba2defd6bde6056815f6  6896f77d5748bfce307a1c0573f462a950f71244375992fe7626bb0dad293254
renders/4x/mechanic.idle8.png  a87b45ec1679b45f6c496c387a16f6135d53c9cbc8ac7d2a363dc71f4a2d93f6  41da7bef4862a928c3747dbf4107d17e8f10f8e12db7a268dd042bf1b0bfc842
renders/4x/mechanic.look.png  5fad2c0818bdfb2a60408b98e0e8815630110fe8d82eff8489d21fb4158c33e8  a116c45feff24d9c1c11f31a6a2dc7101ad6a40e5a9fc0654eaa861537fed90b
renders/4x/mechanic.walkS.png  70a1ab698ecc49af5e0193a9dfe61e9a1c46b3294dc225ca8c3579e08eb29173  a857b4421d80730740691311f9897a448c9d1ab726d4c00a28f91f1595a194b1
renders/4x/mender.blink.png  1ec76cb400dbb77dab5740af8ae65f3c0502703b89570350e3dadddcb22c67e6  62a31d7a49ccc149261d1a9895b93ab53b289b0cc64ffdceeb711b4d13fff3ca
renders/4x/mender.idle8.png  0290e76f4e7b72ca42979ac29b84aceaa7ab66ff40fb60fd2005e8a4e84d2d18  8f356c43588250007e486a588a9ca8f76036ff145d26ca63d20b7bc006b1a49f
renders/4x/mender.look.png  6b8112bf512ea6d081bc04efdff32a37e563d9f277ba4990b7f6d4798cb8d90e  3444c57ee4cc05df29c1ae27cfa2e695764aa463aa756bf91da5462c2d427260
renders/4x/mender.walkS.png  6283860e4e48e4e11aa7ea72d86acd221b7e42c9f60d8131d5c45d88e1501831  09d6705cda74e5f1c0f3f960a5044999dad736cef11ab889fbad977d54cea026
renders/4x/monger.blink.png  3bd8845ae685c601a32cd3b28d67fb4438bdb87670a826d5f0988ab0e40faca6  cc7abfb7e7dea822549f095126382a0df23b4571304ab8b7ce186b84b88c211c
renders/4x/monger.idle8.png  3cd03f1a12d754e9c37c894b9490d2453ee1b3c91525d3063b945a6e8c92f1d5  9d4ca777528c78c85356baef5d876b9789233302328bff418fe84e13cace87df
renders/4x/monger.look.png  54a681dddc21e1105264eaba8614802cbc4ccf653298d1876d28bfb1d0177271  6891b1fa2e7a745f4ba8e49b9133d8319480b2c5c52ea6d5ea87f98dc5840d8e
renders/4x/monger.walkS.png  522d6bfefa886bd5cfbf9fee289ba67b121d4f11d4a62ca027295cc58f253f6b  f146e4a9d7e82acd92614c0148abdd22613af504b82d96c73c5eb5f02893371d
renders/4x/nan.blink.png  cc1efdead6853852f821e1ba36809cfa71c81ddc44ef16e883d6d22524db4d5e  54755548b7d8f5823e8a9bcaa5a7b71ae4ce66b59e25e94fca1acfc1a1a17a63
renders/4x/nan.idle8.png  06d8490074f4c48f90d453409c6e42290157b8a0bbecc7b5e434b03220857c0c  35264a607a33da6736e46064fdc91b997cf9a209c0f9cb8a5dc012f308c0ca2e
renders/4x/nan.look.png  f7a44c9a00cfbb919378bf581c6dd6ba779f06d8565a5d1291dfebd300a2fb6e  fe072061acbf2b571dfc0c4fc35f117b2e0b530242e102ef1d9fb39c7b9ab936
renders/4x/nan.walkS.png  64249072cbd9b153f0ce33bfa68b975f9c35babb3654781d3e00de3c3b19405c  f1a6d5dfff04dfb682d4328316a54c9ca9b9dbd066c8435c260cb0c602c12dd2
renders/4x/oldsalt.blink.png  3e472907c46da96809ab317410fde4f93b6bee3fc617edc07628f022c2928ed5  0eefcb7a08da01b49f6b0cf61b1cb1e1378caa70f27476b1a401563a788b3acc
renders/4x/oldsalt.idle8.png  db6cd20097a6fbffa1be33d0c303262b88a70461fdb757e28789085d08cff705  b216465d35a22a29c490eb3409621aa69e8804a04ebbe72f99aca7b0e2264ff9
renders/4x/oldsalt.look.png  b3f7fba6d4dd2ecd451b1a03e40d7db086ae70da7ed972f41b75d9d12fadc3c5  54638c1d6692ecda25d2751a0436e16a4d939c9ac6dcc5ada03f3d7e8c914457
renders/4x/oldsalt.walkS.png  b3ea97d0211fa0e8f200dada8cfc895dc0b8e82de7c26c4c2862f82ec2612552  5ac6b1e86df421c93266ec005ce98ce5fe8a66a192cbc450d4cf1ff4fc2daab0
renders/4x/packer.blink.png  5de425b2dbb7c50756463df972a3fa133aee172eb463346ac779ff6dc426ec6d  0bb23a0b94080199e3c46188f0061af51b848ba06514f50ae0099448b05357f1
renders/4x/packer.idle8.png  d7b7b6f9f814f237cee2e65d7e037a31a9ef4fe24cce6f273da099c7ed86aa78  a31b5c8fbb63f90256d1fc9154cf4b8ecb0f792aee0d8e1d0bd74ad57d3b7376
renders/4x/packer.look.png  9170e06f9dd36575a7cd9d9520b53c48876543a2889851b792d78ce47becaf51  e823f1eb1c2665c286a9e0fe2b960dc9cc30ed1047ff64f6deb0458acb7f0703
renders/4x/packer.walkS.png  f715cc8f44a1bc1c0da35ed98aa45ee05345ce8939aad96027b607033937ec82  33cda71ca469300431d9ae2ea96115d10bdc523dd9dd3f4cb7f879e115972f94
renders/4x/painter.blink.png  ad17376d52edb89d8e1d09522fd55d66fddb4d4306912931c467214dfc85f53b  f0919940cde0e5fbeffcb0c777c7bce9ab0164fd011ea36cd8e73a4bf33b96f0
renders/4x/painter.idle8.png  8aa52e0fd590325379a2e3778b8ac6f9c2dcf936fcf984d2063332503201a097  ac0cd30a9dae0631d6304fb5f92e31ea97ed2d7f9e02414a2b4719898d53394c
renders/4x/painter.look.png  3ebfe0c12bff2b463167e69b83621067faf955a1e81579cf388f958cce6a55b4  fdd42bde5082120dbf11483148b4a9ced7df98dbf6b7c06aa6d8fc2ea937eee2
renders/4x/painter.walkS.png  28e42d8570528086bb83dc1761c664b6a820fd417048f0a199f5e14db304c0ee  5a887db9114b0db36de7e457f61357d54d4af040d30392619a9ad3f5840229e6
renders/4x/paperboy.blink.png  8ae6a4ea9893d2b7386436a01a1eb975cf56394a0000906fd164e22b119864a7  33832b9c883c44642d320650bbb4da2c577bdb028c9db1075ce5b1bb736d1ef4
renders/4x/paperboy.idle8.png  1792b1c79e2060fe9680b00f1ba3242d47b87e319e4d391ec2b2153394b0884f  6619d8f84b302edb1c52b7fec8069b1aa143e43168fee05b77ca3bbf8e64b08e
renders/4x/paperboy.look.png  5820a73ffd38f18826adac28e9ab5066e1248d2afff4a1d3624e3d7af5edcd4d  a1044b40dd15927ad2fe188ed3d28e9b06b580bbc7965d610d0247b0f8937caa
renders/4x/paperboy.walkS.png  14c3385faf11df27cc00a41875604ef32ae2f04e20e7aa8261731159060766c0  49e8228af0a6f6a94196fb4699460472c175fb47fff6358a2e6afc6d03e6567f
renders/4x/postie.blink.png  9e2620e419f919ea86204b9b9559672e7bc45bb18070a18c853b5687cc30694d  45313a6051211705a215801cd13ef994cea4ff5782b60ebe82557541b6520094
renders/4x/postie.idle8.png  d3d40b7a11b62ea084a0a7e4ec9d3d7b8905d61f3c331d38fb3005f64f33fcd2  3d4d43c6ddc5b4b77bd4e13a7d2a0f570936c2633344b857ffdad46d3ead91a3
renders/4x/postie.look.png  4a035cb196f1f65112adebdbc73ace2a487786823d3f835b328c8bdd276c109e  33441074d4e9fe6f0eaaaf7124aff8723731059a8032f786af4a5f261c08145a
renders/4x/postie.walkS.png  f0d8b8e608443c709c881bf56ba7f6bbc368d29ae3350a47de51e0a7f3604b6b  084e4d9b61f5f0fc3aa2914d42be59e31a4399cebf9e9472a0f94af5f2e42a73
renders/4x/skipper.blink.png  60aff952768e03f2ae30a3b940031e8baae6c3800c2931f39ade0328dae957ef  57c2a297f01678f3d5733a42111204dae371c73e5fe8c0c3927acf983ec921d3
renders/4x/skipper.idle8.png  88ed6648f436b02e5ffc5a0e005be7f48b37a7bcb0b4dce84226a76396f2f3ee  a8d9d63f84c66e5cc0844e47fbd52a87126420ffca8bf9158a9ca26feca0fe01
renders/4x/skipper.look.png  840c297dfc70327d32329c1f554e40aef7802dc9ae78ed07b4aa2ba9d78d9190  34f54e7ca13355fbdc909208b83f4dc42f5d60c12f9d9dab25f858d4fba2a1e8
renders/4x/skipper.walkS.png  7df022d0100dc04e3563ef28b9713a5e0ef723e19678c8527a3697988d74eaee  a9bbbfa738c475a8b70e5cb6d7b7837a63b4c4e8f94a728de15b214cf7e2394c
renders/4x/swimmer.blink.png  681a8807bcb9be06470354d2aa6e4c1af4865f6e95f2a70fc5be401024336882  3a57f16d13e096ae80e31cba4149bf5448b55d5dc1322ca2f236276248e72757
renders/4x/swimmer.idle8.png  b42ef40cc57c57b4a37e4c1160e198bf0f47be5a4c756b51427b8aa8ca4f4b9f  b72adfb5194075358ccb282c1e89d67acb0ca041ee3cd059ab7740cd55081ccd
renders/4x/swimmer.look.png  e9bf782c7c02487f54c6b6fad0bd43fbbca20877217fcd795c0ac8a5f3f1be2d  899fedeb15e8c95ac38b519824f0bcb6dcf02cf49aab725fffb3c1d864f35321
renders/4x/swimmer.walkS.png  4c9dcdbe17eb15ba9ccdfea0252acc7d4259d28655ead4d265ce92eb8ea6f8f4  c68af487fd84f24d42b36b1c5ea552f6c9a659712837acbad7f56f27b8f0d087
renders/4x/tourist.blink.png  23a59dc01cf873e04ce12730068fdc0893bd91498d4fe4ee756e87f76c56b489  934e4d3db45a5e254671917a571afc29415a56a27ced212b1db5fb30a71f295c
renders/4x/tourist.idle8.png  f2dfe72b84c7277199f335d124c2269e70c83ff3f9e13d3ba87d3677c2c47b66  6ac4d329f8cd33350eca12877af8b7713b4e182e9091b7b8ecc6830b8dfc71ba
renders/4x/tourist.look.png  268c11160cb17c2e026793224e8009e670b61f34efc84c4eac463d34fd39c37b  0ad7a8b8a4dac17f30029b24e220d034fa90f93b6fed941ba2297e24e77db0e9
renders/4x/tourist.walkS.png  b798407be9fbc89db79d4ffec34cd57bb9063fea83db499c3baef81128fe5653  62ed1149f069e4ff9a4f4f9fa19e464dc58b601ba8f5cfaafeed2d686686f783
renders/cast.png  d47020ad52d2240b19861bcaf53ddc2929fe2cc505242fe562012abca4689d19  d271c3aa10bf43d935a651e19ab82885bed37231dac9ed360510583251dafdb8
```

</details>

`sha256sum -c SHA256SUMS.txt` in this folder gives 319 OK, because the kit's list is Node's and pins the committed files. `sha256sum -c INTAKE.SHA256SUMS.txt` gives 250 OK: the 248 regenerated files as committed (`SHA256SUMS.txt` among them) and the desk's two scripts. Between them, the two lists pin every file in this folder except `INTAKE.md` and `INTAKE.SHA256SUMS.txt`.

### What was checked (no Unity)

| Check | Engine | Result |
|---|---|---|
| The drop's hash and its `SHA256SUMS.txt` | sha256sum | as the charter; 319 / 319 on arrival |
| The desk's files: the harness, the probes, `node-SHA256SUMS.txt` and its records | sha256sum | each as the charter |
| The kit's checker on the unzipped drop as shipped, with `--random 60` as the desk ran it | Node v24.19.0 | `RESULT: FAIL — 5 problems (570/570 gates, 59/63 data files, 241/241 renders, 319/319 sums)`: the emulation's noise in the four data files and the golden report above. Every line is as the desk's `check-cjs-as-shipped.txt`, timings aside |
| The kit's checker on the unzipped drop, `--write` then the check | Node v24.19.0 | `--write` rewrote the 248 files. The check: `RESULT: PASS — 570/570 gates, 63/63 data files, 241/241 renders, 319/319 sums`. Both runs are line for line as the desk's `check-cjs-write.txt` and `check-cjs-after-write.txt`, timings aside, and `SHA256SUMS.txt` then equals the desk's `node-SHA256SUMS.txt` |
| The desk's harness on the drop as shipped | Node v24.19.0 | `RESULT: PASS (stamps, golden, exports, gameplay, data, renders, random, claims, steps, frozen, vs102, cellcut, aim)`, every line as the desk's `node-check.txt` but the timings |
| The desk's harness on the regenerated kit | Node v24.19.0 | `RESULT: PASS`, every section. Sections A to E (stamps, golden, exports, gameplay, data), which the desk's `node-check-regenerated.txt` records, are as that record line for line, but for one blank line |
| The desk's probes on the regenerated kit | Node v24.19.0 | the desk's `probe.txt` line for line, but the 10.2 kit's path in its first line |
| The kit's checker on this folder as committed | Node v24.19.0 | In place: `RESULT: FAIL — 4 problems (570/570 gates, 63/63 data files, 241/241 renders, 319/323 sums)`. The 4 are exactly the intake's four files, which `SHA256SUMS.txt` does not list. On a copy without those four: `RESULT: PASS — 570/570 gates, 63/63 data files, 241/241 renders, 319/319 sums` |
| The desk's harness on this folder as committed | Node v24.19.0 | `RESULT: PASS`, every section, in 227 s. It is the desk's `node-check.txt` except where the regeneration shows: section B 600 / 600 rows equal (the drop: 517); C builds 30 / 30 byte for byte (28); D sidecars 30 / 30 (29); E shading equal (4 floats differ). Sections A to E are as `node-check-regenerated.txt` |
| The desk's probes on this folder as committed | Node v24.19.0 | the desk's `probe.txt` line for line, but the 10.2 kit's path in its first line; 25 s |
| The two lists in this folder | sha256sum | `SHA256SUMS.txt` 319 OK; `INTAKE.SHA256SUMS.txt` 250 OK |
| The game's script engine against Node: ClearScript V8 from `Assets/_Project/Plugins/Editor/JsEngine`, run outside Unity on .NET 8 over this folder's rig, poses and checks | V8 / Node v24.19.0 | `gameplay(key)` on all 30 builds: identical. `shadingContract('fisher')`: identical. `exportBuild` on the ten and the paper boy: every number within 5.2e-15. After `toFixed(7)` they are identical but for 8 numbers each on the boy and the paper boy: a `tool_L` z on the tie above, which V8 writes −0.058992 where Node writes −0.0589921, and which the export guard holds within the rig's 1e-6. `runChecks` on the ten: every row's id, pass, gate and value identical; only the timings and one tie note differ |

### Re-running both checkers

From the repository root, with Node 18 or later. The PNGs must be checked out from LFS, not left as pointers.

**The kit's checker.** It hashes every file in its folder against `SHA256SUMS.txt`, so in place it fails on the intake's four files, which the kit does not list: `RESULT: FAIL — 4 problems (570/570 gates, 63/63 data files, 241/241 renders, 319/323 sums)`. Run it on a copy without those four files:

```bash
rm -rf "$TMP/rig10-kit" && cp -r docs/art/rigs/character/rig10 "$TMP/rig10-kit"
rm "$TMP/rig10-kit/INTAKE.md" "$TMP/rig10-kit/INTAKE.SHA256SUMS.txt" "$TMP/rig10-kit/node_check_char10_3.cjs" "$TMP/rig10-kit/node_probe_char10_3.cjs"
(cd "$TMP/rig10-kit" && node tools/check.cjs)
```

It should end `RESULT: PASS — 570/570 gates, 63/63 data files, 241/241 renders, 319/319 sums`; it took about 95 s here. Never run it with `--write` in this folder, because `--write` rewrites the kit's files.

**The Art desk's harness and probes.** Both are read-only on the kit, and their sections against 10.2 need 10.2's kit:
- Unzip `C:/hh-drops/character-rig-kit-v10.2.zip` (sha256 `79dcf1ab92db84ad8433c046bb82597944167e1be3b67395739ed06cdd0aaae5`) outside the repository.
- Pass its `export/character-v10.2-kit/` folder as `K102`.
- Pass both folders on the command line, because the defaults are the desk's own paths.
- Create the probes' output folder first: the probes do not create it.

```bash
K102="<unzipped 10.2>/export/character-v10.2-kit" node docs/art/rigs/character/rig10/node_check_char10_3.cjs docs/art/rigs/character/rig10 "$TMP/rig10-check"
mkdir -p "$TMP/rig10-probe" && K102="<unzipped 10.2>/export/character-v10.2-kit" node docs/art/rigs/character/rig10/node_probe_char10_3.cjs docs/art/rigs/character/rig10 "$TMP/rig10-probe"
```

The harness should end `RESULT: PASS (stamps, golden, exports, gameplay, data, renders, random, claims, steps, frozen, vs102, cellcut, aim)`. It took about 4 minutes here and writes `node-check.txt` and `node-check.json`. Unlike the kit's checker, it runs in place, because the intake's files do not trouble it. The probes write `probe.txt` in 25 s. Both outputs should equal this intake's runs (above) line for line, timings and the 10.2 kit's path aside.

### Line endings and LFS

`.gitattributes` pins this folder's text files to LF by extension (`js cjs json txt md html`). `git check-attr` gives `text eol=lf` on every new text file, including `Character v10.3.dc.html` and the two files in `tools/`. The kit's 79 text files carry no CR.

The PNGs go through Git LFS: 241 now (10.2: 121; the 120 strips are new).

The kit's `SHA256SUMS.txt` is taken over LF bytes, and the baker hashes the rig the same way (`CharacterSkinExtractor.LfSha256`), so a Windows checkout does not move a pin.

### The game's port (Phase A, no Unity)

The bake now reads from the rig, in V8, every number 10.3 exports that #918's port carried as a copy:

| Number | 10.3 exports it as | #918's port had | Now |
|---|---|---|---|
| The paint's five tolerances: the least area, the inside test, the depth and shade quanta, the depth tie | `TOL.area`, `inside`, `depthScale`, `shadeScale`, `tieDepth` | literals in `RigPaint9.cs`: 1e-9; 1e-6 three times; 1e7 four times; 1e9 twice; 1e-9 twice | `RigPaint9.Tolerance`, read by `CharacterSkinExtractor.PaintTolerance9`. It also holds that 10.3's paint reads each one where the port uses it |
| The face cull's floor, the def's `FaceCullFloor` | `TOL.cull` | read off 10.2's source text, a literal 1e-4 there | `CullFloor9(host)`. Both of 10.3's culls (the faces' and the marks') read it, once each |
| The marks' pixel edge, the def's `FaceMarkEdge` | `TOL.markEdge` | read off the source, 1e-4 | `ReadMarkCull9(host)`. The marks' band floor (`hh<1e-6`) has no `TOL` key, and is still read off the source as on 10.2 |
| The gate of the rig's own checks | `TOL.gate_m` | `V9Tolerance`, 1e-6 | `GateTolerance9(host)`, for the golden replay and the export test. `V9Tolerance` stays as rig 9's, since 9.2 exports none |
| The dither, the def's `Bayer16` | `DITHER.bayer4` | the canonical 4 × 4 matrix in the port | `Bayer9(host)`. Rig 10 never dithers, and a rig that says it does is refused |
| The ink and the eye white | `INK`, `INK_ROLES`, `EYE_WHITE` | no copy in the bake, which took each colour from the rig's materials | the same, and a new guard holds every def's ink and eye white to `INK` and `EYE_WHITE` |
| The look-at test | `AIM`: bar 4°, 7 bearings, 3 heights, 2 m, idle's first frame | the bearings and heights copied as rows into the look guard | `AIM` on rig 10, and rig 9's own checks file on 9.2. The bar is the rig's `AIM.bar_deg` (the owner's ruling: 4.0° for every build) |

`CharacterRigKit.ExportsNumbers` says which kits export these: 10.3 does, 9.2 does not. For a kit that does, a missing value, or one that is not a finite number above 0, refuses the bake; it never falls back to a copy.

**Copies removed.** On main, the new no-copy guard finds 18 copies on 12 lines:
- `RigPaint9.cs`: the 12 literals above, on 6 lines;
- `CharacterSkinDef.cs`: 4 tooltip lines quoting 1e-4 and 1e-6 (they now name the rig's keys);
- `CharacterSkinBakeGuardTests.V9Life.cs`: the look guard's bearings and heights rows, 2 lines.

On this branch it finds none.

**New guards.** In `CharacterSkinBakeGuardTests.V10Exports.cs`:
- `V10_TheBakeReadsTheRigsExportsOffTheRig`:
  - `TOL`'s keys are exactly the ones the port reads, and each of the port's readers returns its key's value;
  - `DITHER.bayer4` is the port's dither, `INK` holds two colours, and each `INK_ROLES` entry names a material;
  - a fresh bake of each of the ten carries the rig's `TOL.cull`, `TOL.markEdge`, `DITHER.bayer4` and `SHADING.keyline`;
  - every material the rig fixes at an `INK` colour or at `EYE_WHITE` holds that one colour in the def, and some def of the ten carries each;
  - no def carries `iris`.
- `V10_NoCopyOfTheRigsExportsRemainsInThePort`:
  - no literal in the port or in rig 10's tests equals one of `TOL`'s numbers, in code or in a JS string. The port here is every C# file of the character bake and of the skin it writes, and comments are skipped;
  - none of those files, nor the character guard bodies, holds `INK`'s or `EYE_WHITE`'s colour (as hex or as three bytes), a Bayer row, or `AIM`'s bearings or heights;
  - one line may stand: `V9Tolerance`, rig 9's gate, in `CharacterSkinExtractor.V9.cs`.
- `V10_EachOfTheTenAimsInsideTheRigsAimBar`: for each of the ten, the rig's own look-at, the port's and the committed golden report's worst aims all land inside `AIM.bar_deg`.
- `V10_NoFishingClipStepsAHalfTurn`: on fresh bakes of the ten, no bone turns 179.9° or more between neighbouring frames of any clip in the rig's fishing group (`GROUPS.fishing`: the five where 10.2's sockets turned 180°, and hold, bite and reel).
- `V10_TheSleeperLiesCentredOnThePivot`: on every frame of each fresh bake's sleep clip, the pelvis lies at y = `pelvisZ − heightM / 2` (read off the rig) and x = 0, within `TOL.gate_m`.

In `CharacterSkinnedExportTests.V10.cs`:
- `V10_TheGirlPassesTheFaceGateAtEveryFacing`: the rig's own face check, run on the girl, passes, and every facing's row is ok.

Before the push, the no-copy guard was run outside Unity against three trees:
- this branch: none found;
- main's port: the 18 copies;
- this branch with 15 planted lines: each found or passed exactly as it should.

**Guards moved from 10.2's values to 10.3's** (#918's):
- `V10_TheInkIsTheRigsShadingWithNoKeylineAndTheMarksFloors` read its two floors as literals in the rig's source. 10.3's source holds `TOL.cull` and `TOL.markEdge` there instead, so the guard reads `TOL` and holds each test at its one site.
- The look guard shared with 9.2 (`TheLookPortLandsWhereTheRigsGoldenCheckDoes`) aimed at bearings and heights copied from the checks. Now it reads them off the rig: `AIM` on 10.3, and rig 9's checks file for 9.2.
- `V10_EveryPresetExportsWithinTheRigsToleranceOfItsCommittedBuild` held the export to `V9Tolerance`. Now it holds it to the rig's own `TOL.gate_m`.
- Two guards go red by design until Phase B re-bakes the ten:
  - `EveryCommittedSkinDef_PinsTheRigsAsTheyAreToday`: the committed skins pin 10.2's rig hashes.
  - `TheCommittedBindMeshIsTheFaceTheChainComposesToday`: the committed bind meshes are 10.2's.

  Nothing merges before both are green.

### Measured: what 10.3 changes in the game (no Unity)

**The bind meshes (the rig's `exportBuild`, the ten, 10.2 → 10.3).** On 9 of the ten the face count holds, and 22 brow faces each (`brows.up`, `brows.knit`) change their depth bias and nothing else:
- the adults: 0.0143 → 0.01495;
- the boy: 0.013871 → 0.0145015.

The girl's mesh grows from 544 to 554 faces (1,126 → 1,146 triangles): her mouth's marks at SE and SW. As the charter expected, nothing else moves.

**The rod's bend sockets: the largest step between neighbouring frames, any of the ten.**

| Clip | 10.2 | 10.3 |
|---|---:|---:|
| `cast` | 180° | 14.3° |
| `castBack` | 180° | 0.6° |
| `castRelease` | 180° | 13.6° |
| `strike` | 180° | 5.3° |
| `land` | 180° | 3.8° |

The largest step of any joint in the fishing group's eight clips is now 100.7° (`ginny`'s `tool_L` in `cast`, at frame 5).

**The saddle mounts: the largest step of any body joint between neighbouring frames, 10.2 → 10.3.**

| Preset | `mountUp` | `mountDown` |
|---|---:|---:|
| fisher | 131.0° → 94.6° | 104.0° → 103.6° |
| ginny | 160.9° → 99.1° | 137.2° → 109.2° |
| skipper | 173.1° → 102.3° | 169.2° → 115.6° |
| nan | 179.3° → 104.9° | 178.0° → 120.7° |
| deckboss | 90.9° → 89.6° | 112.5° → 96.8° |
| packer | 165.7° → 107.3° | 151.5° → 116.9° |
| cutter | 179.2° → 99.3° | 178.3° → 116.3° |
| hand | 154.3° → 96.5° | 116.1° → 106.5° |
| boy | 119.4° → 121.7° | 134.3° → 130.5° |
| girl | 119.6° → 119.7° | 134.2° → 130.5° |

`mountCab` is unchanged.

**The sleeper: where the pelvis lies along the bed in `sleep`, from the pivot (10.2: −0.18 m on all ten).**

| Preset | 10.3 | Moves |
|---|---:|---:|
| fisher | +0.0533 m | 233 mm |
| ginny | +0.0484 m | 228 mm |
| skipper | +0.0227 m | 203 mm |
| nan | +0.0371 m | 217 mm |
| deckboss | +0.0636 m | 244 mm |
| packer | +0.0480 m | 228 mm |
| cutter | +0.0472 m | 227 mm |
| hand | +0.0536 m | 234 mm |
| boy | −0.0245 m | 156 mm |
| girl | −0.0271 m | 153 mm |

Each 10.3 value is the build's `pelvisZ − heightM / 2`, so the feet and the crown lie half the figure's height either side of the pivot. The pelvis is there, on the centre line, in every frame of `sleep`: off by 0 to 7 decimals on all ten.

Two more measures of the sleeper move from 10.2 to 10.3:
- the pins' body midpoint: −0.16 to −0.25 m → −0.004 to −0.010 m;
- the centre of the posed mesh along the bed: −0.139 to −0.226 m → +0.0086 to +0.0285 m.

**The face gate.** The girl now passes at 8 of 8 facings (10.2: 7 of 8). All ten pass their 19 gated checks and the face check at all 8 facings.

**The look-at.** Every one of the ten lands inside the 4° bar:

| Preset | Worst aim |
|---|---:|
| fisher | 2.96° |
| ginny | 2.90° |
| skipper | 3.73° |
| nan | 3.65° |
| deckboss | 2.98° |
| packer | 2.89° |
| cutter | 2.88° |
| hand | 2.92° |
| boy | 2.82° |
| girl | 2.82° |

**The ink.**
- `INK[0]` `#12181b` and `INK[1]` `#243036`. `INK[1]` is drawn only by the round and lidded eyes' half-shut and gaze-in states, so the cutter and the deck boss carry none.
- The eye white is `#e6e9e2`.
- No face draws in `iris`.
- The keyline colour is `SHADING.keyline` (`#101a19`). It is not drawn (owner ruling K4).

**On screen.** Nothing changes in Phase A: the committed skins are 10.2 bakes. After Phase B's re-bake, these are what show:
- **The girl's face and the brows.** The girl's face at SE and SW changes (her mouth's marks). Every brow's depth bias rises slightly; Phase B's plates will show whether any pixel moves.
- **Nothing from the rod sockets, the mounts, the sleeper or the pins.** The game never draws those clips on the rig's mesh:
  - The fishing, sleep and mount states are not among the mesh states the presenters draw (`DeckRiderMeshPresenter.cs` l.388, 396 and 407; `CharacterFigurePresenter.cs` l.375 and 385).
  - The bunk plays the player's rig 6.5 sleep sprite (`PlayerSleepPresenter`).
  - Nothing at run time reads the pins or the rod's sockets.
  - No one plays `mountUp` or `mountDown`: the presenters keep the mount states off the mesh, and nothing in the game references the girl's or the boy's build. So the game plays neither on the girl or the boy.
- **The overlay's pad.** The re-bake re-measures each skin's `ReachPx`, which is measured over every clip, the mount and sleep clips included (`CharacterSkinPose.MeasureReach`). The figure's overlay is padded by it (`IsoCharacterFigureRenderer.cs` l.893–929). Its comment at l.889 still gives 10.2's figure ("the deck boss asleep reaches 1.69 px below it").

### For the Art desk (and Claude Design, through the owner)

1. The probes write into their output folder without creating it (`node_probe_char10_3.cjs` has no `mkdirSync`); the harness creates its own.
2. The probes' bind-mesh comparison against 10.2 skips a build whose face count changed. It also missed that the brows' depth bias rose on all ten (above), which the README does not mention either.
3. 10.3's paint still holds one tolerance as a literal: the marks' band floor (`hh<1e-6`), for which `TOL` names no key.
4. The shadow mask keeps the literals 1e-9 and 1e-6 outside `TOL`.
5. The gameplay sidecars label all four mount clips `saddle`, the children's bench mounts (`mountCab`, `mountCabDown`) included.
6. The golden report counts `bindBytes` with `R7`, so its sizes follow the builds' rounding. This is a note, not a defect.

## 10.2 (2026-10-02)

This is 10.2's record as #918 wrote it, with its headings one level down.
- Where it says "this folder", it means the folder as 10.2 left it. 10.3 (above) replaced the kit's files, and with them the 3 files below.
- The desk's 10.2 harness, `node_check_char10_2.cjs`, has left this folder. Git history keeps it at `e99863f1`, and `C:/hh-drops/character-rig-kit-v10.2-desk/` holds it too.

Landed 2026-10-02 by the art-pipeline lane: the character rig 10 intake (charter
`HANDOFF-2026-10-01-character-rig-10-intake.md`). This folder is Claude Design's kit **as delivered**,
except for 3 generated files that real Node writes differently (below). Nothing in the rig was edited.
Rig 9's folder (`../rig9/`) is unchanged and stays as 9.2's record. Rig 10 is registered beside it as
catalog key `characterRig10`. Since the intake's Phase B (below), the editor bakes rig 10
(`CharacterSkinAssetBaker.LiveRig`).

| | |
|---|---|
| Drop | `C:/hh-drops/character-rig-kit-v10.2.zip`, 8,416,055 bytes, sha256 `79dcf1ab92db84ad8433c046bb82597944167e1be3b67395739ed06cdd0aaae5` |
| Kit folder in the zip | `export/character-v10.2-kit/`, 195 files |
| On arrival | `SHA256SUMS.txt` lists 194 files: 194 ok, 0 differ, 0 missing, nothing unlisted but itself |
| Rig | `Art/characterIsoRig10.js`, rev 10.2, pass 10, global `CharacterIso10`, sha256 (LF) `cbd50b54001d504d711a6a2e96ea01b1843a12e8b30665c9002e6ce45a7ed814` |
| Pose library | `Art/characterIsoRig10.poses.js`, sha256 (LF) `89c7c902d3575e9ea905aade6c43a26c91bc29653467f62fdd80fb9e2713f820` |
| Checks | `Art/characterIsoRig10.checks.js`, sha256 (LF) `9b25859906baf75f076eae6fde1b5f2210330e00e1306f3640854043b577ad55`. Optional; loaded after the poses |
| Frozen 10.1 | `Art/characterIsoRig10_1.js` and `_1.poses.js` define `CharacterIso10_1`, for the review page only. The game loads neither |
| Added by the intake | `INTAKE.md` (this file), `INTAKE.SHA256SUMS.txt`, and the Art desk's harness `node_check_char10_2.cjs` (sha256 `1e33d7879c2822f10e5c6a736959c84da84a0eb2ac8e9394d7e00ecb76a68dea`, as in `C:/hh-drops/character-rig-kit-v10.2-desk/`) |

### Phase B: the bake and the switch (2026-10-02)

All ten committed skins (`Assets/_Project/Data/Characters/Skin/<preset>.asset`, the player and the
nine cast) were re-baked from rig 10 in one headless run. Each keeps its GUID, id and switch states. Each
now pins `characterIsoRig10.js` and `characterIsoRig10.poses.js` at the hashes above and records revision
10.2 and tone rule V9. Each carries 28 bones, 53 clips and a bind mesh of 519 to 713 faces (2,119 to 2,852
corners), binds the 13 face groups, and paints 20 to 24 of the 32 colour slots that V9 allows. Two changes
came with the switch:

- **Smooth shading.** Where rig 10 gives a face a smooth normal, the bake stores it in UV2, every pose
  skins it, and the facet shader lights the face by it. Against the kit's own renders
  (`renders/1x/<preset>.idle8.png`: idle's first frame at 8 facings, 80 renders, 31,782 drawn pixels),
  the game differs in 52 pixels: 13 drawn by one side only and 39 in colour. 46 of the 80 renders are
  pixel-exact, and the worst differs in 4. Lit flat, as Phase A's port was, the same renders differ in
  4,778 pixels and none is exact.
- **The overlay covers the figure's reach.** Each skin records `ReachPx`: how far its drawing passes
  the 80 × 104 cell on each side, over every honest frame of every clip at every facing. The ashore
  overlay pads each side by it, and the cell is unchanged. All ten reach 10.1 to 11.1 px below the cell
  (the mount clips: `mountUp`, `mountDown`, `mountCab`, `mountCabDown`), at most 2.3 px past either
  side, and never above. Every clip, frame and facing was shot through an overlay opened 15 px all
  round, and no drawn pixel passed the measured reach. The old 1 px pad would have cut 707 to 1,092
  pixels per figure past the cell, nearly all in the mount clips; the measured pads cut none.

The look target height moved with the switch from 1.31 to 1.63 m (`CharacterLookTargetHeightMetres`):
rig 10's fisher's head at rest, 1.6332 m. The plates were shot in the same slot. They are evidence and
are not committed; the PR carries their numbers. Rig 9 stays in the catalog as `characterRig9`. To go
back, set `LiveRig` to `CharacterRigKit.Rig9.CatalogKey` and re-bake.

### The 3 files Node regenerated

As for 9.2 (#889), the committed copy of a generated file is Node's, not the drop's. The Art desk's
harness named these three; on real Node (v24.19.0) the regeneration changes exactly them:

| File | Why the drop's copy differs | How big the difference is |
|---|---|---|
| `builds/boy.v10.json` | Not written by Node. `exportBuild('boy')` on Node writes 11 numbers differently, all at one frame of the rod socket `tool_R_1` in `strike` (`clips[9]`): the same rotation written the other way (a quaternion's sign, and 180° for −180°), plus rounding at the 7th decimal | 11 of 140,812 numbers. 1,411,142 → 1,411,145 bytes. sha256 `608b2fbed247907266ac9b299268407b45ec5749ec78e9f9f67f1ce4bae931ae` → `322d52033af0f9e6b139cbd749ee49013596d35dcd7a111737fdf1d2ef29fb25` |
| `builds/paperboy.v10.json` | The same, at the same frame | 11 of 140,668 numbers. 1,409,419 → 1,409,422 bytes. sha256 `5e8d4f3198d40c07994309d9c1e85d0c0c43e9b506fc43ec9933e12789c77bcc` → `32d154e8d85851b3fae1012b400f184d7ab84d4d7b378e463ed32453c3e11390` |
| `golden-report.json` | Written under Claude Design's emulation, unlabelled. On Node, every build's `sizes.bindBytes` differs (all 30, by −36 to +208 B), and so do the printed numbers of 35 check rows: noise near 1e-16 m and the budget's KB counts (budget on 9 builds, handoffs 15, rock 9, loops 1; the monger's `roundtrip` worst falls on another frame). No gate, pass or failure changes: 569 / 570 gated, the one failure the girl's `face` at 7 / 8 facings | 72 of 5,330 lines. 318,234 → 318,314 bytes. sha256 `6a7e42df3bdd1d19b648346851271051cad79f59e95753ad14add6456aaef976` → `f1841dccfae7a34a2c94e6f2890d100b749c520eedd9d5c8a4197b7fd325f9b5` |

The regeneration wrote each build as the kit writes the other 28 (`exportBuild(key)` under the kit's
header, every number `toFixed(7)`), and the golden report as the kit lays it out (`runChecks` and
`sizes` per build, no row tables, `JSON.stringify(report, null, 1)`). Node writes the other 28 builds
byte for byte as delivered.

`sha256sum -c SHA256SUMS.txt` in this folder gives 191 OK and 3 FAILED, and the 3 are exactly the
files above. `sha256sum -c INTAKE.SHA256SUMS.txt` gives 3 OK. Between them, the two lists pin every
file of the kit.

### What was checked (no Unity)

| Check | Engine | Result |
|---|---|---|
| Zip hash and `SHA256SUMS.txt` | sha256sum | 194 / 194 on arrival |
| The Art desk's harness on the unzipped drop | Node v24.19.0 | `RESULT: PASS`, every line as the desk's record but the timings |
| The same harness on this folder as committed | Node v24.19.0 | `RESULT: PASS`. Stamps 65 / 65. Golden 569 / 570 gated, as the kit; rows equal to this folder's report 600 / 600, sizes 0 differ. Builds 30 / 30 byte for byte with `toFixed(7)`. Gameplay 30 / 30 byte for byte. Data and presets equal. Renders 60 / 60 by pixel. Sections G to L as the desk's record |

### Line endings

`.gitattributes` pins this folder to LF by extension (`js cjs json txt md html`), and the 121 PNGs stay
in Git LFS. The kit's `SHA256SUMS.txt` is taken over LF bytes (the kit's 75 text files carry no CR), and
the baker hashes the rig the same way (`CharacterSkinExtractor.LfSha256`), so a Windows checkout does
not move a pin.

### Re-running the checker

The harness is read-only on the kit and writes only into its output folder. From the repository root:

```bash
R9=docs/art/rigs/character/rig9/Art RC=60 node docs/art/rigs/character/rig10/node_check_char10_2.cjs docs/art/rigs/character/rig10 "$TMP/rig10-check"
```

Pass both folders and `R9`: the defaults are the Art desk's own paths, and where `R9` holds no
`characterIsoRig9.js` the harness skips section K (against 9.2) without a word. It should end
`RESULT: PASS (stamps, golden, exports, gameplay, data, renders, random, claims, steps, frozen, vs92,
cellcut)`. It took about 3 minutes here; the desk measured about 12. Section B compares
the golden checks with this folder's `golden-report.json`, so on this folder it reports 600 / 600 rows
equal and no size differences (on the drop's copy: 565 / 600 and 30).

### The game's port (Phase A, no Unity)

The editor reads rig 10 with the same reader as 9.2, one kit per script host:
`CharacterRigKit` names each kit's files, global, revision, preset table and face rules, and
`CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig10)` loads this folder. Phase A left the editor
baking rig 9; Phase B (above) moved `CharacterSkinAssetBaker.LiveRig` to rig 10 and re-baked the ten.

| What changes | 9.2 | 10.2 | Where the game takes it |
|---|---|---|---|
| Names | `characterIsoRig9`, `CharacterIso9`, `'9.2'`, `CAST` | `characterIsoRig10`, `CharacterIso10`, `'10.2'`, `CAST10` | `CharacterRigKit.Rig10`; catalog key `characterRig10` |
| The export | `_sidecarExport.js` and `fit.js` | the rig's own `exportBuild(key)` and `gameplay(key)` | `CharacterSkinExtractor` |
| The cell | 64 × 92, pivot (32, 82) | 80 × 104, pivot (40, 90) | read from the rig (`W`, `H`, `pivot`) into the def's `CellW`, `CellH`, `PivotPx` |
| The face marks | culled by role (`ROLE`, `minT`) | `minT` 0; a point mark culls by its own band (`pt`, `az`, `sn`, `oh`) | the band in the mesh's UV1.w, `sn` and flags in UV2; the def's `FaceMarkAzFloor` and `FaceMarkEdge`, read from the rig's source; the facet shader culls a mark by its band |
| The keyline | drawn | not drawn unless asked (`keylineDefault: false`) | the def's `KeylineDefault`; the figure is inked by its edge without the ring (owner ruling K4). Other rigs' keylines are untouched |
| Unknown preset | throws | throws | the bake takes the ten of `CAST10` and refuses any other key, the twenty NPCs included (presets only, owner 10-01) |

**The guards.** 9.2's bake guards became bodies that take the rig they read. Their `V9_` tests run them
on 9.2 as before, and the `V10_` tests in `CharacterSkinBakeGuardTests.V10.cs` run the same bodies on
rig 10: the composition, the material limit, the replay of every clip and frame, the clips, the face
and tool tracks, the blink, the look (four ways) and the ink against the rig's own render. Two 9.2
guards stay 9.2-only because they read 9.2's `ROLE`; rig 10 has its own pair, on the faces' own `az`
and on the rig's shading with no keyline. None retired. The strips check stays 9.2's: this kit's render
manifest lists no RGBA hashes, so the harness held its 60 renders to the rig by pixel at landing.

**Every number from the rig.** The cell, the bands, the two floors of the marks' cull (the rig's
`1e-6` and its pixel-edge `1e-4`) and the shading are read from the rig; the guards read them again
from the rig's source text, never from the code they test.

### Measured for the switch (no Unity)

**The body (the rig's own `gameplay(key)`, 9.2 against 10.2).** The work contracts (helm, oars, rail,
ladder, seat, bed, reach: 37 values) are the same in both; the body standing in them is not.

| Preset | Height 9.2 → 10.2 (m) | Change | Drawn at idle 9.2 → 10.2 (px) | Change | Collider radius 9.2 → 10.2 (m) | Change |
|---|---:|---:|---:|---:|---:|---:|
| fisher | 1.5057 → 1.7662 | +17.3% | 45.2 → 51.0 | +12.7% | 0.200 → 0.186 | −7.0% |
| ginny | 1.4739 → 1.6483 | +11.8% | 44.8 → 47.5 | +6.1% | 0.172 → 0.156 | −9.3% |
| skipper | 1.4881 → 1.6407 | +10.3% | 44.8 → 47.4 | +5.7% | 0.210 → 0.201 | −4.3% |
| nan | 1.3572 → 1.5138 | +11.5% | 41.1 → 43.5 | +5.8% | 0.179 → 0.168 | −6.1% |
| deckboss | 1.5981 → 1.8641 | +16.6% | 48.2 → 53.7 | +11.4% | 0.229 → 0.230 | +0.4% |
| packer | 1.4517 → 1.6233 | +11.8% | 44.0 → 47.0 | +6.6% | 0.178 → 0.161 | −9.6% |
| cutter | 1.3176 → 1.4916 | +13.2% | 39.9 → 43.0 | +7.7% | 0.157 → 0.142 | −9.6% |
| hand | 1.4477 → 1.6768 | +15.8% | 43.4 → 48.1 | +10.8% | 0.174 → 0.156 | −10.3% |
| boy | 1.1393 → 1.2570 | +10.3% | 35.8 → 37.5 | +4.9% | 0.158 → 0.147 | −7.0% |
| girl | 1.1367 → 1.2623 | +11.0% | 35.3 → 37.3 | +5.5% | 0.144 → 0.145 | +0.7% |

Height is `figure.height_m`, the crown in idle. "Drawn" is idle's first frame through the rig's own
`proj`, top to bottom of the picture, the tallest of the 8 facings. Both rigs draw 32 px a metre at
the same 40° camera, and the picture also holds the footprint's depth, which barely changes, so the
picture grows less than the standing height. What grows by about 17% is the Fisher's and the deck
boss's standing height (+0.26 and +0.27 m), most of it in the legs: the Fisher's hip goes from 0.6045
to 0.9338 m (+54%) and the hanging hands from 0.5738 to 0.7993 m (+39%). The footprint narrows a little
(0.40 × 0.36 to 0.372 × 0.36 m).

**What pins 9.2's body today.** What the switch moves, and whether it holds on rig 10:

- **The switch's own branches.** The bake (`CharacterSkinAssetBaker.cs` l.663) and four guard tests
  read `LiveRigIsV9` as "rig 9, else rig 7": `CharacterSkinBakeGuardTests`'
  `EveryCommittedSkinDef_PinsTheRigsAsTheyAreToday` and
  `TheCommittedBindMeshIsTheFaceTheChainComposesToday`, `CharacterSkinCastBakeTests`'
  `ThePlayersComposedTableIsTheCommittedDefsTable` and `EveryCastStateIsAClipTheRigBakes`. With
  `LiveRig` on rig 10 they would take rig 7's branch, so Phase B taught them rig 10 before it moved
  `LiveRig`.
- **The helm and the oars.** The contracts are unchanged (the wheel 0.655 m up and 0.315 m ahead, the
  seat 0.4 m). The intro's S7 (#916, not on main yet) holds Armand's drawn ankles (`foot_L`,
  `foot_R`) within 0.05 m of his helm station across the deck. Armand is the skipper preset. On 9.2
  his ankle midpoint sits 7.2 mm off his origin in `idle` (S7 measured 7 mm). On 10.2 it is 9.8 mm in
  `idle`, 0 in `helm_idle` and 32 mm in `helm_walk` (9.2: 24.3 mm). All inside the bar; the ankles
  stay 0.075 m up.
- **Doors.** `BoatInteriorDef.ClearHeightMeters` is read only by the editor spike
  `BoatInteriorExtruder`; the game's door threshold reads the width alone, so no door stops anyone.
  On 9.2 every door cleared every figure (lowest door 1.6 m, tallest figure 1.598 m). On 10.2 the
  Fisher (1.766 m) stands taller than the doors of `CapeIslanderIso` (1.6), `SportFisherSkybridgeIso`
  (1.65 and 1.75), the Fundy inshore lobster boats, hardtop and open (1.696), the Northumberland ones
  (1.74) and `SportFisherConvertibleIso` (1.75). The deck boss (1.864 m) also outgrows the
  Newfoundland inshore boats (1.784), `SternTrawlerIso` and `SternTrawlerMk2Iso` (1.8) and
  `LobsterBoatIso` (1.84). The rest (1.9 to 2.235 m) clear everyone.
- **Bunks.** No bunk length is recorded anywhere; sleep lies at the rig's bed height (0.3 m in both).
  Lying length in `sleep`'s first frame: the Fisher 1.533 → 1.791 m, the deck boss 1.608 → 1.866 m.
- **Where villagers look.** `GameConfig.DefaultCharacterLookTargetHeightMetres` is 1.31 m, rig 9's
  Fisher head point rounded (its comment says 1.3135 m; 9.2 measures 1.3151 m), and
  `CharacterFigureLife` aims looks at it. 10.2's Fisher head point is 1.6332 m. No test ties the
  number to the rig, so after the switch villagers would look about 0.32 m low until it is retuned
  (`GameConfig.asset`, the owner's to tune). Phase B retuned it to 1.63 m with the switch.
- **Walkers' lights.** `WalkerLights.HeadlampLiftMetres` (1.55 m, "her brow") and `LanternLiftMetres`
  (1.05 m, set for a 1.7 m figure) are fixed heights. The Fisher's crown goes from 1.506 to 1.766 m,
  so the headlamp, which rode just above 9.2's crown, sits about 0.22 m under 10.2's.
- **Carried things.** `CarryHands.Pose` hangs props from `FisherCarryAnchors`, sprite-era points by
  gait and frame. The rig's hands rise from 0.574 to 0.799 m, so props would hang low on rig 10 until
  the anchors follow the hands.
- **Plates.** `AshoreFigurePlatePlayTests` (`HerBox`, ±0.7 m across, −0.4 to +2.2 m up; its overlay
  check reads the def's own cell) and `VillagersAshorePlatePlayTests` (±0.9 m, −0.5 to +2.6 m) hold
  the taller figures as they stand.
- **Not tied to the rig's body:** the walkers' 0.35 m foot collider (sprite-era, not the rig's
  radius), the speech bubble at 2.1 m (above the deck boss's 1.864 m), the swim and wade waterlines
  (sprite-only), the synthetic test fixtures, the pre-rig sprite pins, and the guards that pin 9.2 on
  purpose.

**The lying clips against the 80 × 104 cell.** Every frame of `swim` (8), `tread` (6) and `sleep` (6)
at 8 facings, the posed mesh through the rig's own `proj`, unclipped. Reach past the cell's edge, worst
case (negative is room left):

| Preset | swim | tread | sleep | sleep's pixels past the cell | past the game's overlay (cell + 1 px) |
|---|---:|---:|---:|---:|---:|
| fisher | −4.39 | −6.82 | +0.37 (bottom, f0 N) | 0 | 0 |
| ginny | −5.49 | −7.26 | −1.14 | 0 | 0 |
| skipper | −5.49 | −6.52 | −1.43 | 0 | 0 |
| nan | −6.37 | −6.87 | −2.70 | 0 | 0 |
| deckboss | −3.32 | −6.47 | +1.69 (bottom, f0 N) | 8 | 1 |
| packer | −5.57 | −6.63 | −1.39 | 0 | 0 |
| cutter | −6.53 | −7.16 | −2.70 | 0 | 0 |
| hand | −5.13 | −6.97 | −0.51 | 0 | 0 |
| boy | −8.96 | −6.88 | −6.58 | 0 | 0 |
| girl | −9.04 | −7.17 | −6.62 | 0 | 0 |

On 9.2's 64 × 92 cell, every lying clip of the ten fits (the closest is the deck boss's sleep, 0.99 px
of room). Today only the player (the Fisher) plays the lying clips (`PlayerSleepPresenter`,
`PlayerSwimAnimator`). In Phase A the ashore figure drew inside its cell padded 1 px
(`IsoCharacterFigureRenderer.BuildAshoreOverlay`). Phase B pads each side by the figure's measured reach
over every clip as well (above), and the mount clips, not the lying ones, reach furthest.

### Findings for Claude Design (through the owner)

The rig is not edited here. The send-back waits until the intake lane reports (owner, 10-01); this list
is its first draft. Each item goes back with the evidence named. Items 1 to 7 are the 09-30 send-back's,
checked by the Art desk against 10.2.

1. **Missing README sections** (09-30 §2.1, not done). There is still no section on the options file's
   format, the shading or the gameplay sidecar. README l.145's "the outline ruling (§4.6)" points into
   10.0's README, which the kit does not ship.
2. **Stale file name** (09-30 §2.3, not done). `Art/characterIsoRig10.poses.js` l.4 still says the
   solver is in `characterIsoRig8.js`.
3. **No dither, tolerance or ink export** (09-30 §3.1, not done). The API has none of the three, and
   `INK` is internal (`characterIsoRig10.js` l.278, `#12181b`, `#243036`). The baker carries the
   standard 4×4 Bayer and 1e-6 itself.
4. **No blink or look-at in the renders** (09-30 §3.2, not done). README l.146: the renders draw no
   tools, blink or look-at.
5. **Files written under emulation** (09-30 §3.3, in part). No reports ship, but the golden report's
   bind bytes and its 1e-16 values are emulation numbers, unlabelled, and the boy's and the paperboy's
   builds differ from Node's at one frame of `strike` (above). Ask: write the kit's files on real Node.
6. **The fisher's irises** (09-30 §4, no answer). They are still teal, `#2ba39a` (`EYES.sea`, l.278).
7. **The aim bar and the steps** (09-30 §5.1 and §5.2, no answer). The look-at now lands within 2.82° to
   2.98° on every build but the seven elders, which reach 3.65° to 3.73° (9.2: 1.70°, and 2.40° for the
   skipper and Nan). The rod sockets still turn 180.0° between frames 1 and 2 and 4 and 5 of `cast`,
   `strike` and `land`, and `mountUp` swings the right hip 179.3° between frames 9 and 10 (9.2's worst
   body joint was 171.6°, `mountCabDown`). Ask: name an aim bar, and say whether a 180° socket turn
   may be taken either way. (In the game, no code follows a socket's rotation yet: the tool track is
   data, and a clip plays frame by frame without blending, so such a turn would first show when a prop
   hangs on the socket.)
8. **The cell is tighter than the README says, and the lying clips run past it.** README l.122 says
   every build's cell fits with 4–5 px spare; the kit's own report gives 2 to 5 px. Measured unclipped,
   the sleeping figure facing N runs past the cell's bottom edge, and the render cuts it there: 8 px on
   the deck boss, 5 on the lobsterman, 1 on the painter; the Fisher and the monger touch the edge.
   Of the ten the game bakes, only the deck boss loses pixels (the table above). The `cell` check
   exempts swim, sleep and tread on any build over 1.545 m, which is 9.2's Fisher and covers 24 of the
   30 in 10.2. Ask: a cell that holds the lying clips, or a bar set for the 10.x body.
9. **Stale texts in the checks file and the README.** The `cell` check's title says "the 64 × 92 cell"
   (`characterIsoRig10.checks.js` l.174). The `heights` detail still says "the Fisher is 1.522, rig 7
   1.523" (l.268). The cell check's note "cloth (skirt, apron) reaches the cell edge" fires at sleep N
   on 19 presets, the Fisher in overalls among them: there it is the sleeping body at the edge. The
   README's heights are the rig's plus 1 cm on all 30 rows, 6 to 15 mm over (the Fisher "1.79 m" is
   designed at 1.782 m and stands 1.766 m in idle). README l.67: the twenty new builds use every beard
   but the mutton chops (only the core deck boss wears those). README l.115: elders draw salt, grey or
   white hair 83% of the time, not 3 in 4.
10. **Random builds fail the gates; the README names two cases.** 23 of 60 seeded random builds fail at
    least one gate (face 15, look 9, light 1). README §10 names the long head at SW and one crotch
    pixel. The ten presets the game bakes pass every gate but the girl's face at SW, which the README
    names. The owner's ruling K3 has the game's creator gate each build at bake.
11. **The golden report's printed numbers depend on the engine.** The editor runs the rig in
    ClearScript's V8. On the ten presets it reproduces every gated row's pass and flags (189 / 190, the
    girl's face as the kit records), but prints 3 values differently from Node 24: residuals near
    1e-16 m. The game holds such rows to the rig's own 1e-6 gate. Ask: print residuals rounded, as the
    builds are (`toFixed(7)`), so any engine reprints the report byte for byte.

**Notes, not defects:**

- An unknown preset name throws (`no preset "…"`, l.1019). The desk reads this as new; 9.2 on main
  throws the same (`characterIsoRig9.js` l.748). An unknown field still falls back to the Fisher's.
- The body is about 17% taller: the Fisher's idle crown goes from 1.506 to 1.766 m and the collider
  radius from 0.20 to 0.186 m. That feeds the fixture resize, which Claude Design also carries as open
  ("fixture sizes for the 10.x body", README l.145).
- `SHADING.keylineDefault` is false: 10.x draws no keyline unless asked. The game follows the rig and
  draws no keyline on characters (owner ruling K4, 10-01).
- `brows.flat` binds no faces, on purpose (`FACE_EMPTY`: the fringe's edge is the flat brow).
