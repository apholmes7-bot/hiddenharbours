#ifndef HH_HULL_CAUSTICS_INCLUDED
#define HH_HULL_CAUSTICS_INCLUDED
float4 _HHHullCausticPlane;
// width, owner strength, wet/active, maximum absolute upward normal
float4 _HHHullCausticSettings;
// F0, facet tilt degrees, Schlick power, chop dimming; all from GameConfig.
float _HHHullCausticPreGain;
float4 _HHHullCausticOptics;
float4 _HHHullCausticIrradiance; // power, floor, reserved, reserved
// Posed iso elevation sine/cosine and the chop-scaled away-facing floor.
float4 _HHHullCausticBearing;
float4 _SunDir;
float _ShadowStrength;
float _HHHullCausticPainted, _HHHullCausticEnvelope;
float _PixelsPerUnit, _NoiseScale, _BandScaleResponse, _Chop, _Flow;
float _CausticScale, _PaintScale, _UntileStrength, _CausticTexStrength;
float _CausticCurvatureBlend, _CausticCurvatureStep, _CausticCurvatureGain;
float _CausticAmount, _CausticDayGate, _SunElevation;
float4 _CausticColor, _DayNightTint;
TEXTURE2D(_CausticTex); SAMPLER(sampler_CausticTex);
#define HH_CAUSTIC_VEINS
#include "CausticPattern.hlsl"
#include "HullCausticWeight.hlsl"
#endif
