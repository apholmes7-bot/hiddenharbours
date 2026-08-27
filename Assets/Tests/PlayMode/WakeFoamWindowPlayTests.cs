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

            // A drift the buffer will spend many whole cells of over the run, on an axis that is not
            // aligned with the camera's pan — so the window move and the content move are independent.
            var driftPerSecond = new Vector2(0.9f, -0.55f);

            var residual = Vector2.zero;
            Vector2 lattice = FoamBuffer.WorldCellOrigin(Vector2.zero, extent);
            Vector2 contentMoved = Vector2.zero;
            Vector2 expected = Vector2.zero;
            bool primed = false;
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
                // on its water; what remains is the DRIFT, and that is what must be smooth.
                if (primed) contentMoved += new Vector2(driftCells.x, driftCells.y) * FoamBuffer.CellSize;
                lattice = newLattice;
                primed = true;

                expected += driftPerSecond * dt;
                Vector2 drawn = contentMoved + (FoamBuffer.DrawOrigin(lattice, residual) - lattice);
                worstError = Mathf.Max(worstError, (drawn - expected).magnitude);
            }

            Assert.Greater(crossings, 10,
                "the run never spent enough whole cells to exercise the frames that used to teleport — " +
                "raise the drift or the frame count rather than trusting this pass.");

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

            Vector2Int? firstCell = null;
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

                firstCell ??= cell;
                // The cell index MUST change as the lattice steps (the window is camera-centred), but
                // the mark's WORLD position through that window must not.
                float worldX = drawnOrigin.x + cell.x * FoamBuffer.CellSize;
                Assert.LessOrEqual(Mathf.Abs(worldX - mark.x), FoamBuffer.CellSize + 1e-4f,
                    $"frame {frame}: the mark drifted off its patch of water under a sub-cell pan — " +
                    "that is the crawl the cell law exists to prevent.");
            }
            Assert.IsNotNull(firstCell, "the loop never ran");
        }
    }
}
