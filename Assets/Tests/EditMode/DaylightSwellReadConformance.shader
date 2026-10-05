Shader "Hidden/HiddenHarbours/Tests/DaylightSwellReadConformance"
{
    Properties
    {
        _InputColour ("Input RGB", Vector) = (0,0,0,1)
        _SignedBand ("Signed band", Float) = 0
        _SwellReadStrength ("Read strength", Float) = 0
        _Gate ("Existing calm gate", Float) = 0
        _SwellReadBands ("Bands", Float) = 0
        _SwellReadRelative ("Relative switch", Float) = 0
        _LegacyOracle ("Frozen legacy reference", Float) = 0
    }
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Assets/_Project/Art/Shaders/Include/DaylightSwellRead.hlsl"
            float4 _InputColour;
            float _SignedBand, _SwellReadStrength, _Gate, _SwellReadBands;
            float _SwellReadRelative, _LegacyOracle;

            float4 frag(v2f_img input) : SV_Target
            {
                float4 col = _InputColour;
                float swellReadGate = _Gate;
                // Frozen baseline guard/quantizer/add from f0d88ebe (W:5025-5042).
                // This is the independent legacy oracle, NOT a copy of the new relative law.
                // The source guard pins this wrapper's quantizer to the real water fragment.
                if (_SwellReadStrength > 0.001 && swellReadGate > 0.001)
                {
                    float readBand = _SignedBand;
                    if (_SwellReadBands >= 1.0)
                    {
                        float b01 = readBand * 0.5 + 0.5;
                        b01 = floor(b01 * _SwellReadBands + 0.5) / _SwellReadBands;
                        readBand = b01 * 2.0 - 1.0;
                    }
                    if (_LegacyOracle < 0.5 && _SwellReadRelative >= 0.5)
                        col.rgb = HHDaylightSwellReadRelative(col.rgb, readBand, _SwellReadStrength, swellReadGate);
                    else
                        col.rgb += readBand * _SwellReadStrength * swellReadGate * 0.25;
                }
                return col;
            }
            ENDHLSL
        }
    }
}
