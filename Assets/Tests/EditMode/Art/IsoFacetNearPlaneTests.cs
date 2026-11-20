using System;
using System.IO;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// A tall mast is not cut flat by the camera's near plane.
    ///
    /// <para>Height comes toward the camera in the facet pass (the rig's own depth, then the ADR 0033
    /// shear), and every scene camera stands about 10 m in front of the water with near 0.3. Sloop 88's
    /// mast passed the plane above roughly 14 to 22 m, depending on where she sat, and was clipped flat
    /// near 26 m of her 37; the tanker's stern house too. The vertex program now eases depth toward the
    /// plane inside a 4 m band. CI has no GPU, so these check the shader's own expression and the
    /// properties of that curve on a copy of it.</para>
    /// </summary>
    public class IsoFacetNearPlaneTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursIsoFacet.shader";
        const float Ease = 4f;

        // A copy of HHKeepBehindNearPlane; TheShader_EasesDepthAfterTheShear_AndOnlyForTheFacetDepth pins it to the source.
        static float Eased(float z, float zNear)
        {
            float edge = zNear + Ease, t = edge - z;
            return t > 0f ? edge - Ease * t / (t + Ease) : z;
        }

        [Test]
        public void TheShader_EasesDepthAfterTheShear_AndOnlyForTheFacetDepth()
        {
            string src = File.ReadAllText(ShaderPath);
            StringAssert.Contains("#define HH_NEAR_EASE 4.0", src);
            StringAssert.Contains("return t > 0.0 ? edge - HH_NEAR_EASE * t / (t + HH_NEAR_EASE) : z;", src);
            StringAssert.Contains("if (unity_OrthoParams.w < 0.5) return z;", src);

            int shear = src.IndexOf("wp.z  -= (wp.y - _HullShear.y) * _HullShear.x;", StringComparison.Ordinal);
            int bias = src.IndexOf("wp.z  -= v.attrs.z;", StringComparison.Ordinal);
            int ease = src.IndexOf("wp.z   = HHKeepBehindNearPlane(wp.z);", StringComparison.Ordinal);
            int clip = src.IndexOf("o.positionCS = TransformWorldToHClip(wp);", StringComparison.Ordinal);
            Assert.That(shear > 0 && bias > shear && ease > bias && clip > ease,
                "The ease must come after the shear and the face bias and before the clip transform, inside the " +
                "non-reflection branch, so the guard pass (which reads this positionCS) lands on the same pixels.");
        }

        [Test]
        public void A37MetreMasthead_StaysBehindTheNearPlane()
        {
            // Rice Point's camera: z -10, near 0.3. Measured on main: Sloop 88's top reached sheared z -31.
            float zNear = -10f + 0.3f + 0.01f;
            foreach (float z in new[] { -31f, -60f, -200f })
                Assert.Greater(Eased(z, zNear), zNear, $"depth {z} must land behind the near plane");
        }

        [Test]
        public void Depth_KeepsEveryOrdering_AndIsUntouchedBeyondTheBand()
        {
            float zNear = -9.69f;
            float prev = float.NegativeInfinity;
            for (float z = -80f; z <= 20f; z += 0.25f)
            {
                float e = Eased(z, zNear);
                Assert.Greater(e, prev, $"eased depth must rise with depth (at {z})");
                prev = e;
                if (z >= zNear + Ease) Assert.AreEqual(z, e, 0f, $"beyond the band depth is unchanged (at {z})");
            }
            // A metre of real depth near a 50 m masthead is still a measurable step in a Depth32 target.
            Assert.Greater(Eased(-50f, zNear) - Eased(-51f, zNear), 1e-3f);
        }

        [Test]
        public void TheWaterline_IsOutsideTheBand()
        {
            // Every hull root measured in the looks sat at sheared z +5.4 or deeper; the band ends at -5.69.
            float zNear = -10f + 0.3f + 0.01f;
            Assert.Less(zNear + Ease, 0f);
            Assert.AreEqual(5.4f, Eased(5.4f, zNear), 0f);
        }
    }
}
