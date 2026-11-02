#ifndef HH_PLANT_WIND_INCLUDED
#define HH_PLANT_WIND_INCLUDED

float2 PlantWindOffset(float2 root, float time, float2 windWorld, float sway)
{
    float gust = sin(dot(root, float2(0.37, 0.21)) - time * 1.7);
    float2 offset = windWorld * sway * (0.65 + 0.35 * gust);
    return offset;
}

#endif
