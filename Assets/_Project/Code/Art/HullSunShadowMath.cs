using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>The sun projection in the hull's pre-view metres; depth-buffer calibration is not altitude.</summary>
    public static class HullSunShadowMath
    {
        public static bool IsActive(float elevation, float strength, float enabled)
            => elevation > 0f && strength > 0f && enabled > 0f;

        public static Matrix4x4 Pose(float heading, float elevation, float roll, float pitch)
            => IsoFacetMath.RigToWorld(0, elevation) * IsoFacetMath.RigToWorld(heading, elevation, roll, pitch);

        public static Vector4 Projection(Vector2 direction, float length, float elevation,
                                         float heavePixels, float ridePixels, int pixelsPerMetre)
        {
            float radians = elevation * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float height = (heavePixels - ridePixels) / (Mathf.Max(1, pixelsPerMetre) * Mathf.Max(cosine, 1e-5f));
            return new Vector4(direction.x * length * cosine, direction.y * length * cosine,
                               Mathf.Sin(radians), height);
        }

        public static Vector3 Project(Vector3 point, Matrix4x4 pose, Vector4 projection, float waterline)
        {
            Vector3 p = pose.MultiplyPoint3x4(point);
            float height = Mathf.Max(0f, p.z + projection.w);
            return new Vector3(p.x + height * projection.x,
                               p.y * projection.z + waterline + height * projection.y, 0f);
        }

        public static Bounds ProjectBounds(Bounds source, Matrix4x4 pose, Vector4 projection, float waterline)
        {
            Vector3 first = pose.MultiplyPoint3x4(source.min);
            Bounds result = new Bounds(new Vector3(first.x, first.y * projection.z + waterline, 0), Vector3.zero);
            float height = Mathf.Max(0, first.z + projection.w);
            Vector3 low = source.min, high = source.max;
            for (int i = 1; i < 8; i++)
            {
                Vector3 p = pose.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? low.x : high.x,
                    (i & 2) == 0 ? low.y : high.y, (i & 4) == 0 ? low.z : high.z));
                result.Encapsulate(new Vector3(p.x, p.y * projection.z + waterline, 0));
                height = Mathf.Max(height, p.z + projection.w);
            }
            Vector3 min = result.min, max = result.max;
            Vector3 reach = new Vector3(height * projection.x, height * projection.y, 0);
            result.Encapsulate(min + reach); result.Encapsulate(max + reach);
            result.Expand(Vector3.forward);
            // Bounds stores centre/extents; cover round-off from the eight scalar transform stages.
            const float floatRelativePrecision = 1.1920929e-7f;
            result.Expand(result.size.magnitude * (8f * floatRelativePrecision));
            return result;
        }

        public static bool IsOwnId(float alpha, int hullId, int foreId, int span)
        {
            if (hullId <= 0 || alpha <= 0.5f / 255f) return false;
            if (Mathf.Abs(alpha * 255f - hullId) < 0.5f) return true;
            float d = alpha * 255f - foreId;
            return foreId > 0 && d > -0.5f && d < span - 0.5f;
        }
    }
}
