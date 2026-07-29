#ifndef HIDDEN_HARBOURS_REFLECT_MIRROR_INCLUDED
#define HIDDEN_HARBOURS_REFLECT_MIRROR_INCLUDED

// ReflectMirror.hlsl — the SHARED vertex-stage mirror for the HHReflect pass (ADR 0027 #8).
//
// A reflection is the source reflected in the plane it stands on. ADR 0026 already settled that a
// rig's pivot IS its ground contact, so the mirror axis needs no new convention — only that pivot's
// world Y:  y' = 2*pivotY - y.  For a mesh hull the same formula runs against the ADR 0023
// calibrated iso-depth waterplane, which is where that hull actually meets the sea.
//
// ⚠️ TWINNED: HiddenHarbours.Art.WaterReflectionWarp.MirrorY / Mirror, tested headless with measured
// sabotages. Change one, change BOTH in the same PR.
//
// ================================================================================================
// 🔴 unity_ObjectToWorld IS IDENTITY FOR A SpriteRenderer — the pivot MUST be published
// ================================================================================================
// Unity submits sprite meshes with their vertices ALREADY IN WORLD SPACE and an identity
// object-to-world matrix, so _m03/_m13 reads (0,0) for EVERY sprite in the scene. Mirror about "the
// object origin" and every sprite reflects about the world origin instead of its own feet — not a
// subtle error, a total one. Measured on this repo 2026-07-29 in the tree lane (a 3 m-range lamp lit
// three trees equally from the origin); the facet renderer learned the same lesson with _HullOrigin.
//
// So the axis arrives as a PUBLISHED per-renderer property, written by ReflectiveObject into the
// renderer's MaterialPropertyBlock:
//
//        _HHReflectOrigin.xy = the ground-contact pivot, world space
//        _HHReflectOrigin.w  = 1 when a renderer published one; 0 = "nobody did"
//
// With w = 0 the mirror REFUSES to run and collapses the geometry instead of reflecting about the
// origin. A missing reflection is a bug you go and find; a reflection pinned to the world origin is a
// bug that looks like a haunted sea.

// The per-renderer mirror axis (MaterialPropertyBlock; declare it in the consumer's CBUFFER so it
// batches like any other per-renderer property).
//   float4 _HHReflectOrigin;
//   float  _HHReflectLit;

// Reflect a WORLD position about the published ground-contact pivot. Returns the input unchanged
// when nothing published a pivot — the caller is expected to check HHReflectPublished() and drop the
// vertex rather than draw an unmirrored copy on top of the source.
float3 HHReflectMirrorWS(float3 positionWS, float4 reflectOrigin)
{
    positionWS.y = 2.0 * reflectOrigin.y - positionWS.y;
    return positionWS;
}

// Did a renderer publish a real mirror axis? (See the banner: w = 1 means published.)
bool HHReflectPublished(float4 reflectOrigin)
{
    return reflectOrigin.w > 0.5;
}

// The clip-space position of a mirrored vertex, or a DEGENERATE position when no axis was published.
// Collapsing to a single point rasterises nothing at all, which is the safe failure: a reflector that
// forgot its ReflectiveObject draws NOTHING rather than a copy of itself at the world origin.
float4 HHReflectPositionCS(float3 positionWS, float4 reflectOrigin)
{
    if (!HHReflectPublished(reflectOrigin))
        return float4(0.0, 0.0, -2.0, 1.0);   // behind the near plane on every projection -> clipped
    return TransformWorldToHClip(HHReflectMirrorWS(positionWS, reflectOrigin));
}

// PREMULTIPLIED output for the reflection target, plus the §11.6 light-content contract.
//
// The pass writes premultiplied colour (rgb *= a) and blends One OneMinusSrcAlpha, so overlapping
// reflectors composite correctly in ONE target with no sorting contract. That is also what makes the
// water's pre/post-grade split free: an ORDINARY reflection's rgb can never exceed its coverage,
// while a NIGHT-LIT source writes its colour already divided by the day/night tint — far ABOVE
// coverage at night, ~unchanged by day. The excess over coverage IS the compensated light content,
// so no flag channel, no second target and no extra uniform are needed.
// Twin: WaterReflectionWarp.SplitLitShare.
float4 HHReflectPremultiply(float3 rgb, float alpha, float lit, float3 dayNightTint)
{
    if (lit > 0.5)
    {
        // The moon glitter's compensation, verbatim: divide by the tint the overlay will multiply
        // back. The floor is what stops a black tint producing infinities.
        rgb /= max(dayNightTint, 0.02);
    }
    return float4(rgb * alpha, alpha);
}

#endif // HIDDEN_HARBOURS_REFLECT_MIRROR_INCLUDED
