#ifndef HH_DAYLIGHT_SWELL_READ_INCLUDED
#define HH_DAYLIGHT_SWELL_READ_INCLUDED

// W1: the caller supplies the existing posterized band and unchanged calm gate.
// For C >= 0 and g in [0,1], output lies in [(1-S*g)*C, (1+S*g)*C].
// Negative channels stay negative: this layer never manufactures a brightness floor.
// C# twin: HiddenHarbours.Art.DaylightSwellRead.Relative. No texture/field reads.
float3 HHDaylightSwellReadRelative(float3 colour, float readBand, float strength, float gate)
{
    if (strength <= 0.001 || gate <= 0.001) return colour;
    float amount = clamp(readBand, -1.0, 1.0) * saturate(strength) * gate;
    return colour + max(colour, 0.0) * amount;
}

#endif
