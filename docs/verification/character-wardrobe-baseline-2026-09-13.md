# Character creator and wardrobe: production baseline

Source audit on 2026-09-13 against `70969b684e6bf4d0581c8e7ea5c7e35e5de31a8c`.
This records code and committed data, not a live Unity playtest. No editor slot was used.

| Area | Observed seam and consequence |
| --- | --- |
| Ashore | `App/Editor/PersistentCoreBuilder.cs:403` installs `Core/Iso/IsoCharacterSprite` using `FisherIso.asset`; the older `FisherSheet` remains the missing-art fallback. `Art/PlayerOutfitVisual.cs:78` listens for committed/preview outfit events and writes a palette property block to the root `SpriteRenderer`. No ashore mesh presenter is installed here. |
| Aboard | `Player/DeckRiderVisual.cs:558` calls `DeckRiderMeshPresenter.PoseForRider` once with its authoritative aboard state. The presenter requires `GameConfig.MeshCharacter`, usable skin, a facet hull, and an allowed mesh state. It explicitly refuses ashore (`DeckRiderMeshPresenter.cs:202`). The committed `GameConfig.asset:307` sets `MeshCharacter: 1`; default-off comments are stale. `FisherIso.asset:1625` references the committed fisher skin, whose mesh states are idle/walk/run/balance. |
| Mesh appearance | `Art/IsoCharacterFigureRenderer.cs` builds its ramp textures directly from `CharacterSkinDef.Materials`; it does not subscribe to outfit events. A saved or previewed sprite palette change therefore does not dress this mesh. It shares the hull's facet/depth pass and hull ID; a standalone creator or ashore figure needs an explicit rendering route. |
| Mounted | `Player/PlayerDrivePresenter.cs:128` requires a live visible-driver seat, mount contract and complete drive sprite clip, plays `CharacterClip.Drive`, and places its pivot through `DriveSeatMath`. It has no mesh assembly path. The aboard presenter does not claim `ControlMode.Driving`. |
| Pose/contact | `Core/Iso/CharacterSkinStateMap.cs` resolves only idle/walk/run/balance; helm/oars fall through to gait. `CharacterSkinPose.cs:52` fences distant bone failures at 8 m, not physical hand/foot contact. Production `docs/art/rigs/characterIsoRig6.js:1699` still divides boot placement by `Max(0.001, kneeZ-ankleZ)` without the viewer's segment clamp. Mount and child-lift regressions require the rig correction and fresh extraction, not a claim that the runtime fence proves valid poses. |
| Face/material | The committed fisher skin has twelve material rows and 32 pixels/metre. Production `CharacterSkinDef` stores no facial-state track, and the figure draws the bind mesh without a face stamp. The extractor/baker preserve geometry, not the viewer's procedural facial material. The skin/baker/renderer cap materials at 16 ramps. The renderer uploads global gain/bias but not each material's gain/bias. Eight expressions, gaze, speech and blink from the browser are not production coverage. |
| Wardrobe | `World/InteriorWardrobe.cs:79` publishes `WardrobeRequested`. `UI/WardrobePicker.cs:219` lists every authored outfit, without an owned-item filter, and previews via events. Confirm calls `OutfitLocker.Wear`; cancel restores the ID captured at open. Confirm closes even when Wear returns false, so future save failure handling must be explicit. |
| Persistence | `SaveMigration.CurrentVersion` is **14**. `SaveData.WornOutfitId` and `OutfitLocker` contain one selection; no clothing ownership or person recipe exists at this baseline. Unknown nonempty outfit IDs survive migration and visual resolution failure. Preserve that recoverability when adding a recipe. |
| New game | `UI/Shell/TitleScreen.cs:251` calls `ShellFlow.StartNewGame`; `Core/Shell/ShellFlow.cs:91` immediately calls `BeginNewGame` and enters the world. `SaveService.BeginNewGame` writes the replacement save immediately. Stage creator choices before that destructive boundary and commit once; `SaveService.Save` is intentionally suppressed while at title. |
| Commerce | `Economy/GearShop.TryBuy(offer,wallet,save)` demonstrates wallet, seller and event conventions but permits null save before `TrySpend`: money can be deducted and success published without recording ownership. It also mutates ownership before a disk write that can throw. Reuse `IWallet`; define and test readiness/rollback before adapting the purchase flow. |

Seller candidates are grounded in existing builder source: `seller.leblancs` in
`App/Editor/StPetersBuilder.cs:768` is the general-store counter, with Marguerite placed
there by `StPetersInhabitants.cs:146`; `seller.nmc_chandlery` in
`NineMileCreekBuilder.cs:252` is the existing rod-selling counter. Neither has clothing
stock at this baseline. Committed scenes contain gear offers but no serialized seller
IDs, so source wiring alone does not prove the current baked scene exposes catalogue
stock. Verify/rebank the selected counter during production integration.

The current CI workflow is **Buildalon**, not GameCI: Ubuntu Unity EditMode and PlayMode
tests, plus a separate Python scene-export staleness check and unittest job. It does not
invoke a standalone player build. `Assets/Tests/EditMode/HiddenHarbours.Tests.EditMode.asmdef`
is the existing Core test assembly. Pure schema/recipe validation can run from exactly the
same C# sources in a disposable .NET/NUnit harness, without Unity objects or stubs; tests
that create `ScriptableObject`s, resolve assets or exercise rendering remain Unity checks.
Existing wardrobe content, picker model/PlayMode, save migration, character build,
skin export/bake/state-map suites are the relevant extensions. Browser pose sampling does
not replace the in-world create/buy/equip/reload and contact acceptance from the handoff.

## Stage 1 validation added in this branch

`CharacterClothingContractTests` and `CharacterClothingWorkbenchContentTests` passed
**55/55, zero failed, skipped or warnings**, in the disposable NUnitLite 3.14.0/.NET 9
runner on 2026-09-13. It compiles the exact Core schema, recipe and validation source
files. Content checks read the actual workbench fit, five garment records and two
recipes; there is no hand-entered copy of the authoring data and no dependency on the
generated catalogue, browser output or a Node build. They cover duplicate/malformed
IDs and records, unresolved fits/sections/coverage, prices, colourways, combined-slot
and section collisions, incompatible tags in either order, required fitted surfaces,
unchanged identity and JSON roundtrips.
The final review added two regressions proving IDs and source hashes cannot carry a
hidden trailing newline; strict end-of-input validation now rejects both.
Independent final workbench review reproduced and closed two parity defects: duplicate
selected geometry now fails before assembly, and omitted material overrides inherit the
complete source material. Six focused Node checks passed for those paths, invalid
garment versions/slots, and trailing-newline IDs/hashes. The accepted inheritance path
preserved the identity recipe and complete original material metadata.

Reproduce from the repository root on this machine:

```powershell
dotnet run --project artifacts/character-clothing-contracts/CharacterClothingTests.csproj -- --noheader --labels=Off --result=artifacts/character-clothing-contracts/results.xml
dotnet build artifacts/character-clothing-contracts/UnityApiCompile.csproj --nologo --verbosity minimal
```

Both projects/results are ignored disposable files. The second command compiles both
Def wrappers and both test files against the installed Unity 6000.5.0f1 managed
assemblies: **zero warnings, zero errors**. It does not start Unity. The runnable local
content tests use `System.Text.Json` with `IncludeFields`; the Unity branch uses
`JsonUtility` and was compiled, not executed locally. Unity deserialization, asset
import, rendering, source extraction, scene wiring and the gameplay loop remain CI or
granted-editor checks.

The production boot correction must travel with a real skin rebake:
`CharacterSkinBakeGuardTests.TheDefPinsBothRigs_ByHashesTakenTheSameWay` compares the
committed fisher's source and base hashes to the current rig7/rig6 files, independently
normalizing to LF. A source-only production rig edit fails this guard. Changing the
stored hash alone would conceal stale geometry/keys; Stage 1 keeps the proposal in the
workbench until the actual rebake and production checks can run.
