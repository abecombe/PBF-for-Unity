Shader "BackGround/CircularGradient"
{
    CGINCLUDE

    #include "../../Common.hlsl"

    float4 _InsideColor;
    float4 _OutsideColor;

    inline float rand(float2 uv, float scale = 3)
    {
        return lerp(-scale/255, scale/255, frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453));
    }

    float4 Frag(v2f_default i) : SV_Target
    {
        const float dist = distance(i.texcoord, (float2)0.5);
        float4 color = lerp(_InsideColor, _OutsideColor, dist * 2);
        color.rgb += rand(i.texcoord);
        return color;
    }

    ENDCG

    Properties
    {
        _InsideColor("Insise Color", Color) = (0,0,0,1)
        _OutsideColor("Outsise Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" }

        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha

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