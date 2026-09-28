# Current-frame hull reflections (R2)

Owner-approved 2026-09-28, with coordinator v40's Phase B amendments. Serves P1/P2. GPU acceptance remains a separate R2 editor-slot task.

## The dependency and its split

The hull overlay's `HHReflect` pass samples `_HHHullScreenTex`. Previously reflections recorded before that camera's hull resolve and declared no texture read: the persistent global could still contain another frame or camera's image. Moving reflections after the old resolve alone creates a cycle: reflections feed water colour, water depth clips hull facets, and those facets produce the resolved reflection source.

When hulls, displaced water and reflectors are all active, the feature now records:

```text
guard -> water depth -> hull facets/deck -> resolve -> reflections -> water colour
```

Guard and current foam also feed water colour.

More precisely: `guard → water depth → facets/deck → resolve → reflections → water colour`; guard and current foam also feed water colour. The reflection pass declares `UseTexture(resolved, Read)` on the handle produced in the same `RecordRenderGraph` invocation. Water colour declares its reflection, guard and current-foam reads. Depth attachment read/write usage connects water depth to hull facets.

The existing `_HHHullScreenTex` remains the only resolved hull image. The in-scene overlay, deck-occluded sprite and lamp-shadow shaders retain their original resource and now the reflection reads that same current, wave-clipped image. Resolution, colour formats, id/fore-id filtering, keyline gate, lighting and pivot rules are unchanged. Globals are still published with `SetGlobalTextureAfterPass` after their producer.

Water colour uses a separate initially clear depth target, `_HHWaterColorZ`. Reusing the hull depth here would clip the water underlay against hulls/fittings and change the interior/waterline contract. The new buffer retains main's water-only self-occlusion. No scene depth buffer is touched.

No displaced water: hull resolve precedes reflections without a depth replay. No effective hulls: ordinary reflectors precede water. No reflectors: keep the original combined water colour/depth-before-hulls path. Effective hulls requires a resolve material, as before. Culling an empty renderer list cannot bypass required clears/current source production.

At zero `ReflectionRegistry.Count`, `AddRenderPasses` calls `ReflectionRegistry.BindIdle()` before its all-idle return, following the foam house pattern. This rebinds the existing transparent 1×1 fallback every zero-member frame. No camera in that frame records a reflection producer. There is no fallback graph pass and no changed registration, cap or pivot policy.

## Shader parity boundary

The water fragment body stays in place, with a literal `depthOnly` parameter. Its depth-only return occurs after both original coverage clips and before any reflection/colour work. Thin wrappers select true for `HHWaterDepth` and false for both colour entry points. `vertDisplaced` and the guard declaration/test move verbatim into shared HLSL scope. Only displaced wrappers call the guard; sharing its declaration does not make flat water use it.

Both displaced passes use the same vertex implementation, guard, material keywords and complete clip computation, including painted surface noise, seabed/still-water depth, swash, bore run-up, fetch, shore fade and wake lift. The depth pass has no colour attachment, uses `ColorMask 0`, and never reaches `ObjectReflection`. No water maths, material property default, preset or simulation state changes. Shader compilation cannot prove GPU depth parity; the later controls must.

## Resource and performance budget

The split adds one replay of this camera's visible displaced-water geometry/coverage and one graph-owned, full-camera-resolution, single-sample Depth32 texture. No extra hull draw, deck draw, resolve, persistent full-size target, or new tuning value. Existing persistent targets retain their existing per-camera dictionaries and disposal. New callbacks are noncapturing and graph pass data is pooled; allocation is limited to initial camera/size/pool setup.

| Render size | Conservative extra depth bytes | MiB |
|---|---:|---:|
| 1920×1080 desktop | 8,294,400 | 7.91 |
| 1280×720 phone render example | 3,686,400 | 3.52 |
| 2400×1080 native phone example | 10,368,000 | 9.89 |

These are examples, not a new resolution policy. Driver padding and graph pool retention are additional considerations. Non-overlapping graph depth lifetimes may alias, but the budget does not assume that optimization. Two simultaneously live camera graphs have twice the conservative budget. Profile GPU median/p95 prepass and whole-frame time, visible water draws/triangles, CPU recording/GC, peak and retained RT memory, and first-frame/resize spikes. Shore coverage can be expensive; the replay is not advertised as free. The desktop objective remains 60 fps; phone viability needs later device measurement.

## Recording tests and Null Device audit

`HullReflectionGraphTests` has exactly 12 EditMode cases:

- `HullReflectionGraph_DeclaresCurrentSourceBeforeConsumption`: 9 gate combinations (all present with guard on/off; hull+reflection; hull+water; water+reflection; water only; reflection only; hull only; idle).
- `HullReflectionSource_CamerasAndResizeAreIndependent`: `OneCamera`, `TwoCameras`, `Resize`.

They invoke the production `HullPass.RecordRenderGraph` with actual Core builders, inspect actual resource versions, attachments, publications, renderer-list tags/layers and camera target identities, and verify clear flags, ordering and producer edges. Headless culling data is deliberately empty; the tests prove recording/ownership, not drawn geometry. A narrow allocator injection wraps uncreated RenderTexture objects while production keeps `ReAllocateHandleIfNeeded`. The runtime camera dictionaries/import logic and all pass decisions run unchanged.

**Do not instantiate RenderGraph normally in this fixture.** Core 17.5's field initializer constructs `RenderGraphDefaultResources`, whose constructor allocates a default shadow RT and submits its clear. Instead the test-local adapter creates an uninitialized real graph and initializes only its managed recorder fields, real pool, builders and resource registry. It does not call `BeginRecording` (which also initializes default GPU resources), a compiler, executor, camera render/cull, or command submission. It does call the CPU-only `RenderGraphResourceRegistry.BeginRenderGraph` with a fresh execution index for every recording in a case. At each case boundary it clears the shared managed object pools; teardown discards cached test handles before restoring the prior global resource epoch. Each graph also restores the validity-check setting. A missing private API fails the test; there is no graphics-device skip.

The source audit below is pinned to the installed **Core 17.5.0** (`com.unity.render-pipelines.core@f43b729f1f6e`). Paths are relative to that package. It covers production calls reached by the fixture as well as its own graph setup. Metadata reads are not claimed to be GPU execution proof.

| Call reached by fixture | Source / why it does not render |
|---|---|
| Managed recording setup via reflection | `Runtime/RenderGraph/RenderGraph.cs:357–378,1608`: pool, builder, lists, registry/state and globals; `RenderGraphResourceRegistry.cs:295–312`: registry constructor constructs arrays/pools and registers callbacks without invoking them. Default resource/context/compiler fields are deliberately unused. |
| Real `RenderGraphObjectPool` / builder creation | `RenderGraphObjectPool.cs:25–33` stores managed object factories; `RenderGraphBuilders.cs:21–27` initializes fields. |
| Per-recording `RenderGraphResourceRegistry.BeginRenderGraph(executionIndex)` | `RenderGraphResourceRegistry.cs:314–318` only stores the execution count and calls `ResourceHandle.NewFrame`; `RenderGraphResources.cs:101–119` hashes the count into the static validity token. No command/context/texture allocation is involved. The adapter calls this part of `BeginRecording:1233`, never its GPU default initialization at :1235. |
| Per-case `RenderGraphObjectPool.Cleanup`, epoch save/restore | `RenderGraphObjectPool.cs:130–138` clears managed temporary collections and shared free-object pools; :25–33 installs those pools without release/destroy callbacks, :38–41 calls their `Clear`. This is the same pool cleanup used by `RenderGraph.CleanupResourcesAndGraph:477`. Only cached objects are discarded, not checked-out pass objects. Epoch reflection reads/restores the static integer field at `RenderGraphResources.cs:39`, after pool cleanup; it submits no work. |
| `ContextContainer.Create/Get/Dispose` | `Runtime/Common/ContextContainer.cs:23,44–66,143–153`: context storage and reset. Camera/rendering context items are data; no render invocation. |
| `RTHandles.Alloc(existing RenderTexture)` | `Runtime/Textures/RTHandles.cs:722–725` forwards to `RTHandleSystem.cs:1452–1467`; this overload wraps the supplied object. `RTHandle.cs:183–189` assigns it and its identifier. Ownership is false. No size-allocating overload is used. |
| `RenderGraph.ImportTexture(handle, params)` | `RenderGraph.cs:603–607` forwards to `RenderGraphResourceRegistry.cs:470–523`, which records external backing, descriptor, clear/discard metadata and validates dimensions/formats. |
| `RenderGraph.CreateTexture(TextureDesc)` | `RenderGraph.cs:675–679` → `RenderGraphResourceRegistry.cs:809–820`, which adds a descriptor/resource handle. It is **not** `RenderTexture.Create`. |
| `AddRasterRenderPass` | `RenderGraph.cs:1053–1074`: obtains pooled data, appends a pass, sets up its builder; `Debug/RenderGraph.DebugData.cs:319–328` adds only debug metadata. |
| `CreateRendererList(RendererListParams)` | `RenderGraph.cs:780–784` → `RenderGraphResourceRegistry.cs:873–876`: stores the description. Actual context renderer-list creation is deferred to execution at registry :1219, which this fixture never calls. |
| `UseRendererList` | `RenderGraphBuilders.cs:594–597`: records the list on the pass. |
| `UseTexture`, `SetRenderAttachment`, `SetRenderAttachmentDepth` | `RenderGraphBuilders.cs:345–360,515–552`, through `UseResource:249–299`: records access/resource versions and attachment metadata. Validation inspects descriptors, not pixels. |
| `AllowPassCulling`, `SetGlobalTextureAfterPass`, `SetRenderFunc` | `RenderGraphBuilders.cs:84–90,393–400,584–587`: flags, queued publication, stored delegate. Render delegates are never invoked. |
| Builder disposal | `RenderGraphBuilders.cs:149–194`: registers graph globals/dependencies and resets recording state. The command-buffer flag set/reset by Setup/Dispose is a validation flag; no command buffer is created or submitted. |
| Test inspection | `RenderGraph.cs:361`; `RenderGraphPass.cs:28–65`; registry `GetTextureResource(int):851–854`; `RenderGraphResourceRendererList.cs:50–59`: reads recorded objects, actual resource lists/versions, descriptors and renderer-list descriptions. |
| Recording cleanup | `RenderGraph.cs:1402–1413` clears passes/registry/globals; registry `Clear:1241–1248`, `Cleanup:1256–1262` clears unallocated pools. It never uses public graph Cleanup, default resources or compiled graphs. |
| Pass resize/disposal `RTHandle.Release` | `RTHandle.cs:237–247`: removes wrapper, skips destruction for non-owned backing, resets fields. Fixture destroys its uncreated descriptor objects separately. |

The following are **UnityEngine APIs, not implemented by Core 17.5**, so a Core source-line claim for them would be false: GameObject/Camera creation and scalar properties, transform position, camera entity id, SortingSettings/DrawingSettings/FilteringSettings construction, ShaderTagId/PropertyToID, Shader.Find, Material creation, RenderTextureDescriptor/new RenderTexture/IsCreated, Shader global getters/setters, ScriptableObject.CreateInstance/DestroyImmediate, and the idle fixture's 1×1 Texture2D/SetPixel/GetPixel. They create/read CPU objects or properties, never call Camera.Render, RenderTexture.Create, texture readback or a command buffer. The idle test supplies an unuploaded clear fallback and restores it/the globals, so production `BindIdle` cannot call Texture2D.Apply in the fixture. [Unity's constructor source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Graphics/Texture.cs) distinguishes the RenderTexture object/descriptor constructor from explicit GPU creation; every fixture allocation additionally asserts `IsCreated()==false`. [SortingSettings source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/RenderPipeline/SortingSettings.cs) initializes camera sorting state. These rolling engine references supplement, rather than replace, the pinned Core audit. ProfilingSampler construction creates profiler markers; Begin/End is never called.

Two forbidden paths are worth keeping visible: `RenderGraph.cs:364` → `RenderGraphDefaultResources.cs:43–69` allocates/submits the default shadow clear; `RenderGraph.cs:1235` initializes defaults on BeginRecording. The adapter bypasses both, not the production recording calls under test. CI supplies runtime execution of this fixture; local validation remains standalone C# compilation only.

### First-push failures and fixture repair (2026-09-28)

Run `36406177809` returned eight failures, all in the nine-case graph test. The first full/guarded case recorded successfully but expected one explicit read instead of two. The next seven non-idle cases threw at their first colour attachment; idle and the three camera/resize cases passed. Both causes are in the fixture; production recording and shader code need no change for this repair.

**Seven MRT exceptions:** a fresh builder can receive an old pass object. `RenderGraph.cs:1063–1072` obtains a pooled raster pass, initializes it and sets up the builder. `RenderGraphPass.cs:625–627` calls `Clear`, but :107–115 intentionally retains colour/input/random-access attachment slots and relies on handle validity. The adapter's disposal returned passes to the static shared pool (`RenderGraph.cs:1409,1483–1488`; `RenderGraphPass.cs:717–722`; `RenderGraphObjectPool.cs:25,125–127`) without either advancing the recording epoch or clearing that pool. Consequently an old attachment remained valid. When a different layout supplied a different resource/version to index 0, `SetColorBufferRaw:275–286` rejected it as a second binding. Matching layouts could pass because that same check accepts equal handles, which explains why the camera/resize cases did not establish isolation.

Normal game recording cannot take this missing-epoch path: URP 17.5's `Runtime/UniversalRenderPipeline.cs:259,393,1003` reuses its pipeline render graph, and `UniversalRenderPipelineRenderGraph.cs:26–27` calls `BeginRecording` before the renderer records passes. Core `RenderGraph.cs:1233` advances its execution count through `RenderGraphResourceRegistry.BeginRenderGraph:314–318`; `ResourceHandle.NewFrame:101–119` changes the token checked by `IsValid:83–85`. Old pooled attachments therefore fail validity before reuse. Pipeline disposal also calls graph cleanup (`UniversalRenderPipeline.cs:466`; Core `RenderGraph.cs:469–477`). This is a source-traced explanation of the fixture defect, not a claim that GPU acceptance has run.

The repaired fixture retains real pooled passes and real builders. It invokes the CPU epoch step once per recording. Each test starts with clean shared free-object pools and execution index zero, so a preceding fixture cannot supply an inherited attachment. Each test ends by clearing the pools and restoring its saved epoch, including on assertion failure. Records inside one case share the pool and use increasing execution indices. Each gate case first records/disposes a full graph with the opposite guard setting, then records its requested layout: reuse is exercised even when that case runs alone. Existing camera/resize checks keep earlier graphs alive and inspect their stored handles/metadata without submitting them. No test count or device gate was added.

**First case's second explicit read:** `RecordWater` binds depth with `SetRenderAttachmentDepth(depth, AccessFlags.ReadWrite)`. Core `RenderGraphBuilders.cs:539–547` routes that through `UseResource:249–291`; :254,263–269 records a read and excludes it from the implicit-read list because `AccessFlags.Read` is present. The two explicit inputs are the initially clear depth attachment (version 0) and the current guard. **Both also exist in a game frame.** The repaired assertion checks the exact resource-index multiset (depth plus optional guard), checks depth version zero, and retains the guard's exact producer-version assertion. With guard off it expects the depth read alone. Reflection input remains prohibited; no production dependency was removed to satisfy the assertion.

## Sabotage assertions (execute in Phase C)

| Sabotage | Assertion expected to fail |
|---|---|
| Remove reflection's `UseTexture(resolved, Read)` | `Reflection must explicitly read this recording's current hull resolve: declared resource version` |
| Record reflections before the resolve | The same assertion's `producer order` arm (or unproduced-resource/version assertion) |
| Read a previous/other camera's source | `Camera reflection must read its own current resolve: declared resource version`; distinct camera backing and dimensions checks |
| Publish another hull target | `Published hull must be this camera's target` |
| Use hull-contaminated depth for late water | `Late water must not use hull-contaminated depth` |
| Select `HHWater` for the prepass | Depth pass tag assertion expecting `HHWaterDepth` |
| Remove idle rebind | `Zero-reflector AddRenderPasses must bind clear before all-idle return` |

These are named mutation expectations, **not locally executed sabotage results**. They consume no Phase B push and run on the granted Phase C slot.

## GPU controls and acceptance boundary

Keep the 2D renderer's lowest-layer `BeforeRenderingSprites` injection point and existing Preview/Reflection-camera exclusions. For game, Scene and inset cameras, compare raw reflected hull masks to independently mirrored **same-frame resolved hull** masks using the published current pivot/id block and documented pixel rounding. Report XOR pixels, maximum boundary error and centroid offset, with explicit in-bounds coverage. Zero stale silhouette requires zero mask/offset mismatch in deterministic controls; compare the previous-frame mask as a negative control.

Cover first frame and re-enable, alternating fast turn/translation, camera pan/zoom, both two-camera orders including an empty view, resize on its first frame, no hulls, no reflectors, guard off and no water. Compare main/candidate hull and water coverage with reflection strength disabled on temporary material clones, including rough shallow swash/bore, wake lift, interiors, deck/fittings and overlapping chunks. Keep original materials/presets and R1 tests unchanged. Compiled headless tests do not prove depth parity, rendered global timing, memory aliasing, shader appearance or GPU cost.

The second-push forecast supplied by coordinator v41 against main `45a09dd6` is EditMode **13329/13077/252/0** and PlayMode **1007/923/84/0** (total/passed/skipped/failed), with main's names plus the same 12 cases and identical skips. These are forecasts, not observed second-push results. The owner authorized exactly one repair push after the first run returned results XML and the eight fixture failures. No local Unity runs are authorized in Phase B; the branch does not merge main and PR #904 stays draft. After the repair push, report and stop.
