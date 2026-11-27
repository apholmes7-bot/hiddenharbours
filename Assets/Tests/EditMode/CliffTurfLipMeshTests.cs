using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests
{
    /// <summary>
    /// The turf lip is laid RIGID on a jagged brow (owner 11 26: the brow strip's grass was stretched). Every
    /// station carries the same metres of strip above and below its own line point, so a jag moves the strip's
    /// rows and never squeezes them, and a face cut short cuts the strip instead of squeezing it.
    /// </summary>
    public class CliffTurfLipMeshTests
    {
        const float StripS = 64f, Inland = 10f / 32f, Face = 86f / 32f;

        static CliffWallSample[] Wall(int n, float drop)
        {
            var s = new CliffWallSample[n];
            for (int i = 0; i < n; i++)
            {
                var brow = new Vector2(i * 0.5f, 0f);
                s[i] = new CliffWallSample(brow, brow + new Vector2(0f, -1.5f), drop);
            }
            return s;
        }

        // A brow pushed in and out by station, like the mix's jag.
        static float Jag(float u, float t) => t > 0f ? 0f : 0.6f * Mathf.Sin(u * 97f);

        [Test]
        public void EveryStationCarriesTheSameMetresOfStrip_HoweverTheBrowJags()
        {
            CliffWallColumns columns = CliffWallSurface.Unsliced(Wall(24, 6f));
            CliffWallMeshData m = CliffWallSurface.TurfLipMesh(columns, StripS, Inland, Face, 3f, 12f, Jag, 1f, Vector3.zero);
            Assert.AreEqual(24 * 2, m.Verts.Length, "two vertices a station");
            float texelsPerMetreV = 1f / (Inland + Face);
            bool moved = false;
            for (int c = 0; c < 24; c++)
            {
                Vector3 top = m.Verts[c * 2], bottom = m.Verts[c * 2 + 1];
                float metres = Vector2.Distance(top, bottom);
                Assert.AreEqual(Inland + Face, metres, 1e-4f, $"station {c}: the strip is squeezed or stretched");
                Assert.AreEqual(metres * texelsPerMetreV, m.Uvs[c * 2].y - m.Uvs[c * 2 + 1].y, 1e-5f,
                    $"station {c}: v does not count the metres drawn");
                Assert.AreEqual((3f + c * 0.5f) / StripS, m.Uvs[c * 2].x, 1e-5f, $"station {c}: u is not metres along the brow");
                if (c > 0 && Mathf.Abs(m.Verts[c * 2].y - m.Verts[(c - 1) * 2].y) > 0.05f) moved = true;
            }
            Assert.IsTrue(moved, "the lip did not follow the jag at all");
        }

        // A wall whose brows run up the screen and whose toes lie east of them: it faces across the screen.
        static CliffWallSample[] EastWall(int n, float drop)
        {
            var s = new CliffWallSample[n];
            for (int i = 0; i < n; i++)
            {
                var brow = new Vector2(0f, i * 0.5f);
                s[i] = new CliffWallSample(brow, brow + new Vector2(1.5f, 0f), drop);
            }
            return s;
        }

        // One step in the brow line, half a metre out, a quarter of the way down the tile.
        static float OneStep(float u, float t) => u > 0.25f ? 0.5f : 0f;

        [Test]
        public void OnAWallAcrossTheScreen_TheLipDissolvesAtAStepInTheBrow_AndNowhereElse()
        {
            var jag = new CliffJag(0f, 0f, Vector3.zero, Vector3.zero, 0f, 1, new Vector2(0.9f, 0.95f));
            CliffWallColumns columns = CliffWallSurface.Unsliced(EastWall(24, 6f));
            CliffWallMeshData plain = CliffWallSurface.TurfLipMesh(columns, StripS, Inland, Face, 0f, 12f, OneStep, 1f,
                                                                   Vector3.zero, default, jag, 0f);
            CliffWallMeshData faded = CliffWallSurface.TurfLipMesh(columns, StripS, Inland, Face, 0f, 12f, OneStep, 1f,
                                                                   Vector3.zero, default, jag, 0.25f);
            CollectionAssert.AreEqual(plain.Verts, faded.Verts, "the fade moves no vertex");
            int dropped = 0;
            for (int c = 0; c < 24; c++)
            {
                float was = plain.Colors[c * 2].a, now = faded.Colors[c * 2].a;
                Assert.AreEqual(now, faded.Colors[c * 2 + 1].a, "a station's two vertices fade together");
                if (now < was - 0.5f) dropped++;
                else Assert.AreEqual(was, now, $"station {c}: the lip faded away from any step");
            }
            Assert.AreEqual(2, dropped, "the lip must dissolve at the two stations of the step, and only there");
        }

        [Test]
        public void OnAWallDownTheScreen_TheRiserFadeChangesNothing()
        {
            var jag = new CliffJag(0f, 0f, Vector3.zero, Vector3.zero, 0f, 1, new Vector2(0.9f, 0.95f));
            CliffWallColumns columns = CliffWallSurface.Unsliced(Wall(24, 6f));
            CliffWallMeshData plain = CliffWallSurface.TurfLipMesh(columns, StripS, Inland, Face, 0f, 12f, OneStep, 1f,
                                                                   Vector3.zero, default, jag, 0f);
            CliffWallMeshData faded = CliffWallSurface.TurfLipMesh(columns, StripS, Inland, Face, 0f, 12f, OneStep, 1f,
                                                                   Vector3.zero, default, jag, 0.25f);
            CollectionAssert.AreEqual(plain.Verts, faded.Verts);
            CollectionAssert.AreEqual(plain.Colors, faded.Colors, "a south wall has no riser to see: its lip is untouched");
        }

        [Test]
        public void AShortFaceCutsTheStrip_ItDoesNotSqueezeIt()
        {
            // A 0.5 m drop draws far less face than the strip is long.
            CliffWallColumns columns = CliffWallSurface.Unsliced(Wall(4, 0.5f));
            CliffWallMeshData m = CliffWallSurface.TurfLipMesh(columns, StripS, Inland, Face, 0f, 12f, (u, t) => 0f, 1f, Vector3.zero);
            for (int c = 0; c < 4; c++)
            {
                float metres = Vector2.Distance(m.Verts[c * 2], m.Verts[c * 2 + 1]);
                Assert.Less(metres, Inland + Face - 0.5f, "the face is short: the strip must be too");
                Assert.AreEqual(metres / (Inland + Face), 1f - m.Uvs[c * 2 + 1].y, 1e-5f,
                    "a cut strip keeps its texel size: v stops where the metres stop");
            }
        }
    }
}
