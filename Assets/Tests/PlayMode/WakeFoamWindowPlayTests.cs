using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The window-alignment pin for the owner's 2026-08-27 defect 3: <i>"the whole foam band shifts by
    /// 1–2 px as ONE unit … it's noticeable it's a separate entity from the water; they shift in large
    /// groups."</i>
    ///
    /// <para><b>What it drives.</b> The exact per-frame sequence <c>IsoFacetHullFeature</c>'s foam block
    /// runs — camera position → <see cref="FoamBuffer.WorldCellOrigin"/>, published drift ×
    /// <c>Time.deltaTime</c> → <see cref="FoamBuffer.AdvectCells"/>, then
    /// <see cref="FoamBuffer.DrawOrigin"/> — over REAL frames with a REAL moving camera and REAL variable
    /// deltas. What it measures is the world position a mark laid on the sea is DRAWN at, which is the
    /// only quantity the owner could have been looking at.</para>
    ///
    /// <para><b>Why PlayMode and not just the EditMode invariant.</b> <c>FoamBufferTests</c> proves the
    /// arithmetic with a fixed synthetic drift; this proves it survives the two things a synthetic loop
    /// cannot supply and the shipped code actually meets — a camera whose pan is not a multiple of
    /// anything, and frame deltas that vary. The defect was a quantization artefact, and quantization
    /// artefacts are exactly what a tidy fixed-step loop hides. It also runs the pipeline in the ORDER
    /// the pass runs it (advect first, publish second), which is where a sign or an off-by-one-frame in
    /// the residual would show and nowhere else.</para>
    ///
    /// <para>No GPU: the buffer's window is C# arithmetic that the shaders are merely handed. That is
    /// deliberate — it means CI adjudicates the pin.</para>
    /// </summary>
    public class WakeFoamWindowPlayTests
    {
        private GameObject _camera;

        [TearDown]
        public void TearDown()
        {
            if (_camera != null) Object.Destroy(_camera);
        }

        private Camera MakeCamera()
        {
            _camera = new GameObject("foam-window-cam");
            var cam = _camera.AddComponent<Camera>();
            cam.orthographic = true;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            return cam;
        }

        [UnityTest]
        public IEnumerator TheDrawnBand_DriftsSmoothly_AcrossRealFramesAndARealCameraPan()
        {
            const float extent = 96f;
            Camera cam = MakeCamera();

            // ⚠️ A DELIBERATELY FAST drift, and the reason matters. In batchmode a frame is about a
            // millisecond, so 120 frames span a tenth of a second — at a realistic 0.9 m/s the buffer
            // would cross the 0.125 m cell boundary ONCE in the whole run and this test would pass
            // without ever exercising the frame that used to teleport. (It did exactly that on the
            // first run: the coverage assertion below caught it at 1 crossing.) The invariant is
            // independent of the drift's magnitude — `AdvectCells` banks whatever remainder it is
            // given — so winding the speed up costs nothing but buys dozens of crossings, while
            // `Time.deltaTime` stays REAL and variable, which is the half of this that EditMode
            // cannot supply. The axis is off the camera's pan so the window move and the content
            // move stay independent.
            var driftPerSecond = new Vector2(40f, -26f);

            var residual = Vector2.zero;
            Vector2 lattice = FoamBuffer.WorldCellOrigin(Vector2.zero, extent);
            Vector2 contentMoved = Vector2.zero;
            Vector2 expected = Vector2.zero;
            float elapsed = 0f;
            int crossings = 0;
            float worstError = 0f;

            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;

                // Pan the camera on an awkward curve: never a whole number of cells, never periodic
                // with the frame rate.
                _camera.transform.position = new Vector3(3.7f * Mathf.Sin(elapsed * 1.3f),
                                                         2.1f * Mathf.Cos(elapsed * 0.7f), -10f);

                Vector3 camPos = cam.transform.position;
                Vector2 newLattice = FoamBuffer.WorldCellOrigin(new Vector2(camPos.x, camPos.y), extent);
                Vector2Int driftCells = FoamBuffer.AdvectCells(ref residual, driftPerSecond * dt);
                if (driftCells != Vector2Int.zero) crossings++;

                // The window's own move is compensated exactly by the content scroll, so the mark stays
                // on its water; what remains is the DRIFT, and that is what must be smooth. Every frame
                // counts, including the first — the frame after a scene load can carry a large dt, and
                // dropping its whole-cell step would offset the run permanently.
                contentMoved += new Vector2(driftCells.x, driftCells.y) * FoamBuffer.CellSize;
                lattice = newLattice;

                expected += driftPerSecond * dt;
                Vector2 drawn = contentMoved + (FoamBuffer.DrawOrigin(lattice, residual) - lattice);
                worstError = Mathf.Max(worstError, (drawn - expected).magnitude);
            }

            // The anti-vacuous guard, and it is not decoration: it is what caught the first version
            // of this test running 120 near-instant batchmode frames and crossing a single cell.
            Assert.Greater(crossings, 20,
                $"only {crossings} whole-cell scrolls in the whole run, so the frames that used to " +
                "teleport were barely exercised. Raise the drift or the frame count — do not trust " +
                "this pass.");

            // Half a cell is the size of the artefact being ruled out; the true error is a float
            // accumulation and lands orders of magnitude under it.
            Assert.Less(worstError, FoamBuffer.CellSize * 0.5f,
                $"Across {crossings} whole-cell scrolls the drawn foam wandered {worstError:0.0000} m " +
                "from where the drift actually carried it. That gap IS the band jumping as one unit " +
                "relative to the water it sits in.");
        }

        [UnityTest]
        public IEnumerator WithNoWind_TheWindowNeverMovesOffItsLattice()
        {
            // The A/B, through the live path: a windless sea must publish exactly the window that
            // shipped before this round, so nothing about a calm harbour can have changed.
            const float extent = 96f;
            Camera cam = MakeCamera();
            var residual = Vector2.zero;
            float elapsed = 0f;

            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
                elapsed += Time.deltaTime;
                _camera.transform.position = new Vector3(5f * Mathf.Sin(elapsed), 4f * Mathf.Cos(elapsed), -10f);

                Vector3 camPos = cam.transform.position;
                Vector2 lattice = FoamBuffer.WorldCellOrigin(new Vector2(camPos.x, camPos.y), extent);
                FoamBuffer.AdvectCells(ref residual, Vector2.zero);
                Vector2 drawn = FoamBuffer.DrawOrigin(lattice, residual);

                Assert.AreEqual(lattice.x, drawn.x, 0f, $"frame {frame}: x left the lattice with no drift");
                Assert.AreEqual(lattice.y, drawn.y, 0f, $"frame {frame}: y left the lattice with no drift");
            }
        }

        [UnityTest]
        public IEnumerator TheWindowStaysWorldAnchored_UnderASubCellPan()
        {
            // The cell law itself, re-proved on the live camera: the mark a hull leaves must sit on its
            // patch of water while the camera creeps. This is the guarantee the drift fix must not have
            // traded away — the two live in the same published vector, so a fix to one CAN break the
            // other, and only a test that watches both would notice.
            const float extent = 96f;
            Camera cam = MakeCamera();
            var residual = Vector2.zero;
            var mark = new Vector2(12.3456f, -7.891f);

            float? firstWorld = null;
            for (int frame = 0; frame < 40; frame++)
            {
                yield return null;
                // Creep by well under one cell per frame (0.125 m), on both axes.
                _camera.transform.position += new Vector3(0.011f, -0.007f, 0f);

                Vector3 camPos = cam.transform.position;
                Vector2 lattice = FoamBuffer.WorldCellOrigin(new Vector2(camPos.x, camPos.y), extent);
                FoamBuffer.AdvectCells(ref residual, Vector2.zero);
                Vector2 drawnOrigin = FoamBuffer.DrawOrigin(lattice, residual);

                Vector2Int cell = FoamBuffer.SampleCell(mark, drawnOrigin);
                Vector2Int lattCell = FoamBuffer.SampleCell(mark, lattice);
                Assert.AreEqual(lattCell, cell,
                    $"frame {frame}: the drawn window put the mark in a different cell from the lattice " +
                    "window with no drift at all — the residual is leaking.");

                // The cell INDEX may step as the camera-centred window walks the lattice; what must not
                // move is the patch of WATER that index resolves to. Comparing the index alone would be
                // wrong (it is supposed to change); comparing the world position it addresses is the
                // actual crawl claim — and it is not true by construction, because it is the pair
                // (origin, index) that has to stay consistent.
                float worldOfCell = drawnOrigin.x + cell.x * FoamBuffer.CellSize;
                firstWorld ??= worldOfCell;
                Assert.AreEqual(firstWorld.Value, worldOfCell, 1e-4f,
                    $"frame {frame}: the world position the mark's buffer cell addresses moved under a " +
                    "sub-cell pan. That is the crawl the cell law exists to prevent, and the drift fix " +
                    "shares the published vector with it — so a fix to one CAN break the other.");
            }
            Assert.IsNotNull(firstWorld, "the loop never ran");
        }
    }
}
