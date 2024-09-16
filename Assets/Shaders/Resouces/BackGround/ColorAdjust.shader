Shader "BackGround/ColorAdjust"
{
    CGINCLUDE

    #include "../../Common.hlsl"
    #include "../../Color.hlsl"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    float _HueShift;
    float _SaturationMultiplier;
    float _ValueMultiplier;
    float _YiqShift;
    float3 _WhiteBalance;
    float _BlackLevel;
    float _WhiteLevel;
    float _Gamma;
    float _Contrast;
    float _Exposure;

    half4 Frag(v2f_default i) : SV_Target
    {
        half4 color = tex2D(_MainTex, i.texcoord);
        color.a = 1;

        // Apply HSV shift
        color.rgb = saturate(HsvShift(color.rgb, float3(_HueShift, _SaturationMultiplier, _ValueMultiplier)));
        // Apply YIQ shift
        color.rgb = saturate(HueShiftYiq(color, _YiqShift));
        // Apply Gamma
        color.rgb = pow(color.rgb, _Gamma);
        // Apply Contrast
        color.rgb = AdjustContrast(color.rgb, _Contrast);
        // Adjust Post Exposure
        color.rgb *= _Exposure;
        // Apply Black and White levels
        color.rgb = saturate(color.rgb - _BlackLevel) / (_WhiteLevel - _BlackLevel);

        color = saturate(color);

        return color;
    }

    ENDCG

    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags{ "RenderType" = "Opaque" }

        ZTest Always
        Cull Off
        ZWrite Off
        Blend Off

        // 0
        Pass
        {
            CGPROGRAM
            #pragma target   5.0
            #pragma vertex   VertDefault
            #pragma fragment Frag
            ENDCG
        }
    }
}