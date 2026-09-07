using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// ⭐ <b>THE RULE A DRAWN WALL MEETS THE SEA ON — and the proof that it is not a second one.</b>
    ///
    /// <para>The owner, Nine Mile Creek north wall, 06:11 on 2026-09-06: <i>"boats arent touching but not
    /// sitting in water"</i>. <see cref="TidalFaceWaterline"/> is the answer, and the whole risk in it is
    /// that a wall could end up riding an arithmetic of its own — a few centimetres per metre of range
    /// away from the hull tied to it, which nobody could adjudicate by eye and which
    /// <c>a-weight-and-its-colour-must-come-from-one-publisher</c> is the standing warning about. So the
    /// first test here is an IDENTITY against <see cref="TidalRide.ScreenRise"/> across the range, not a
    /// spot check: a transcription that agreed at mean water and drifted at the springs would pass a spot
    /// check and is exactly the defect worth guarding.</para>
    ///
    /// <para>The rest is the OFF contract, which is what makes this safe to land on placed art: an
    /// unconfigured face, a face told not to ride, and a scene with no published sea must each draw the
    /// picture they draw today rather than a wall cut at zero.</para>
    /// </summary>
    public class TidalFaceWaterlineTests
    {
        private const string SeaLevelProperty = "_HHSeaLevelWorld";
        private static readonly int SeaLevelId = Shader.PropertyToID(SeaLevelProperty);

        private Vector4 _publishedBefore;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            // ⚠️ A shader GLOBAL survives the test that set it. Snapshot and restore, or a later fixture
            // measures this one's sea.
            _publishedBefore = Shader.GetGlobalVector(SeaLevelId);
        }

        [TearDown]
        public void TearDown()
        {
            Shader.SetGlobalVector(SeaLevelId, _publishedBefore);
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        private TidalFaceWaterline NewFace()
        {
            _go = new GameObject("Face");
            return _go.AddComponent<TidalFaceWaterline>();
        }

        private static void PublishSea(float metres) =>
            Shader.SetGlobalVector(SeaLevelId, new Vector4(metres, 1f, 1f, 1f));

        private static void PublishNoSea() => Shader.SetGlobalVector(SeaLevelId, Vector4.zero);

        /// <summary>
        /// ⭐⭐ <b>THE WALL RIDES CORE'S RULE, NOT A COPY OF IT.</b> An identity over the whole tidal range
        /// and past both springs, at three different lips, so a transcription that is right at mean water
        /// and wrong at the ends cannot hide in it.
        /// </summary>
        [Test]
        public void TheWaterlineIsTidalRideScreenRise_AtEveryStateOfTheTide()
        {
            foreach (float lipY in new[] { 0f, 87f, -12.5f })
            foreach (float lipElevation in new[] { 3f, 3.4f, 0f })
            for (float sea = -4f; sea <= 4.001f; sea += 0.25f)
            {
                float measured = TidalFaceWaterline.WaterlineWorldY(lipY, lipElevation, sea);
                float rule = lipY + TidalRide.ScreenRise(sea, lipElevation);
                Assert.AreEqual(rule, measured, 1e-6f,
                    $"a face whose lip is at y {lipY} and elevation {lipElevation} m put its waterline " +
                    $"at {measured} with the sea at {sea} m, where the ONE rule says {rule}. A wall and " +
                    "the hull lying against it must ride one arithmetic.");
            }
        }

        /// <summary>
        /// The two ends of the range, spelled out in this region's own numbers, because an identity is
        /// only as good as the reader's belief that the rule itself is right. This wharf's deck is 3.00 m
        /// and its lip lands on y 87: at spring high the wall must show its authored 0.80 m of freeboard
        /// and at spring low its whole 5.20 m of bared face, and both fall out of the rule alone.
        /// </summary>
        [Test]
        public void TheWallBaresItsOwnFreeboardAtHighWaterAndItsWholeFaceAtLow()
        {
            const float lipY = 87f, deck = 3f, springHigh = 2.2f, springLow = -2.2f;
            float scale = IsoGround.HeightScale;

            float high = TidalFaceWaterline.WaterlineWorldY(lipY, deck, springHigh);
            float low = TidalFaceWaterline.WaterlineWorldY(lipY, deck, springLow);

            Assert.AreEqual((deck - springHigh) * scale, lipY - high, 1e-4f,
                "at spring high the drawn wall between the water and the lip must be this wharf's own " +
                "freeboard (0.80 m of height), and nothing else");
            Assert.AreEqual((deck - springLow) * scale, lipY - low, 1e-4f,
                "at spring low it must be the whole face the tide bares (5.20 m of height)");
            Assert.Greater(high, low,
                "the water stands HIGHER up the wall at high water — a sign error here would draw the " +
                "flood as an ebb and still pass every magnitude check");
        }

        /// <summary>
        /// ⭐ <b>UNCONFIGURED IS THE SHIPPED PICTURE.</b> Every face already placed in a scene predates
        /// this component; a wall that came back cut at zero — the game's datum, mid-face — would be a
        /// worse defect than the one being fixed, and it would ship on the first import.
        /// </summary>
        [Test]
        public void AnUnconfiguredFaceIsNotCutAtAll()
        {
            TidalFaceWaterline face = NewFace();
            PublishSea(0f);

            Assert.IsFalse(face.IsConfigured, "a fresh component has not been told where its lip is");
            Assert.AreEqual(float.NegativeInfinity, face.WaterlineWorldYNow(),
                "an unconfigured face must report its waterline BELOW everything, which is what an " +
                "uncut wall means — not a cut at the datum");
        }

        /// <summary>The A/B switch: the same placed course, one term changed. Without it a plate pair is
        /// about two months of other merges as much as it is about this change.</summary>
        [Test]
        public void AFaceToldNotToRideDrawsTheOldPicture()
        {
            TidalFaceWaterline face = NewFace();
            face.Configure(lipWorldY: 87f, lipElevation: 3f);
            PublishSea(0f);

            Assert.AreEqual(TidalFaceWaterline.WaterlineWorldY(87f, 3f, 0f), face.WaterlineWorldYNow(),
                1e-4f, "riding, it cuts at the published sea");

            face.SetRidesTheTide(false);
            Assert.AreEqual(float.NegativeInfinity, face.WaterlineWorldYNow(),
                "not riding, it is not cut — the picture the wall drew before this landed");
        }

        /// <summary>
        /// ⚠️ <b>NO PUBLISHED SEA IS NOT A SEA AT ZERO.</b> <see cref="WaterSurface"/> publishes the
        /// all-zero vector on disable precisely so a stopped play session cannot leave a cliff — or now a
        /// quay — standing in a sea that is not there. A consumer that read <c>x</c> without <c>w</c>
        /// would cut every wall at the game's datum in the editor, which is 2.3 units up a 5-unit face.
        /// </summary>
        [Test]
        public void NoPublishedSeaLeavesTheFaceUncut()
        {
            TidalFaceWaterline face = NewFace();
            face.Configure(lipWorldY: 87f, lipElevation: 3f);

            PublishNoSea();
            Assert.IsFalse(TidalFaceWaterline.TryPublishedSeaLevel(out _),
                "all-zero is the unset state, and the w component is the flag that says so");
            Assert.AreEqual(float.NegativeInfinity, face.WaterlineWorldYNow(),
                "with no sea published the wall must draw whole");

            PublishSea(1.25f);
            Assert.IsTrue(TidalFaceWaterline.TryPublishedSeaLevel(out float published));
            Assert.AreEqual(1.25f, published, 1e-5f, "and it reads the level that was published");
        }

        /// <summary>
        /// ⭐ <b>THE CAMERA'S SCALE IS PUSHED, NOT WRITTEN IN THE SHADER.</b> A face's rows are heights,
        /// and a height draws <see cref="IsoGround.HeightScale"/> up-screen — 0.766, which is
        /// <c>cos(40°)</c> and NOT <see cref="IsoGround.GroundDepthScale"/>'s 0.643. A literal in the
        /// HLSL would be a second definition of the camera that no C# change could ever move, and the
        /// two are 19% apart, which on this wall is 0.96 units of waterline at the springs.
        /// </summary>
        [Test]
        public void ThePushedHeightScaleIsIsoGroundsOwn()
        {
            TidalFaceWaterline face = NewFace();
            face.Configure(lipWorldY: 87f, lipElevation: 3f);

            var renderer = face.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, "the component requires the renderer it pushes to");

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Vector4 pushed = block.GetVector(Shader.PropertyToID(TidalFaceWaterline.TideProperty));

            Assert.AreEqual(87f, pushed.x, 1e-4f, "x is the lip's world y");
            Assert.AreEqual(3f, pushed.y, 1e-4f, "y is the lip's elevation in metres");
            Assert.AreEqual(IsoGround.HeightScale, pushed.z, 1e-6f,
                "z must be IsoGround.HeightScale itself, so the shader can never drift from the camera");
            Assert.AreEqual(1f, pushed.w, 1e-6f, "w is the on flag, set by Configure");
            Assert.AreNotEqual(IsoGround.GroundDepthScale, pushed.z,
                "the ground-depth scale is the OTHER half of this camera and 19% short — the mistake " +
                "this project has made before");
        }
    }
}

