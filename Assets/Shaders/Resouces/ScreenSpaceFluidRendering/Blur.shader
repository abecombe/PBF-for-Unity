Shader "ScreenSpaceFluidRendering/Blur"
{
    CGINCLUDE

    #include "../../Common.hlsl"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    sampler1D _AlphaLookupTex;
    float2 _AlphaLookupRange;

    float2 _Direction;
    float _SampleStep;

    half FragLookup(v2f_default i) : SV_Target
    {
        return tex1D(_AlphaLookupTex, saturate((tex2D(_MainTex, i.texcoord).a - _AlphaLookupRange.x) / (_AlphaLookupRange.y - _AlphaLookupRange.x)));
    }

    inline half BoxFilter(float2 texcoord)
    {
        const float4 D = _MainTex_TexelSize.xyxy * float4(-1.0, -1.0, 1.0, 1.0);

        half value = 0;
        value += tex2D(_MainTex, texcoord + D.xy).r;
        value += tex2D(_MainTex, texcoord + D.zy).r;
        value += tex2D(_MainTex, texcoord + D.xw).r;
        value += tex2D(_MainTex, texcoord + D.zw).r;
        value *= 0.25f;

        return value;
    }

    half FragPrefilter4(v2f_default i) : SV_Target
    {
        return BoxFilter(i.texcoord);
    }

    static const float weights[8] =
    {
        0.12445063, 0.116910554, 0.096922256, 0.070909835,
        0.04578283, 0.02608627, 0.013117, 0.0058206334
    };

    half Frag(v2f_default i) : SV_Target
    {
        const float2 direction = _Direction * _MainTex_TexelSize.xy;
        half value = 0;
        for (int j = 0; j < 8; j++)
        {
            const float2 offset = direction * ((j + 1) * _SampleStep - 1);
            value += tex2D(_MainTex, i.texcoord + offset).r * weights[j];
            value += tex2D(_MainTex, i.texcoord - offset).r * weights[j];
        }

        return value;
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
            #pragma fragment FragLookup
            ENDCG
        }

        // 1
        Pass
        {
            CGPROGRAM
            #pragma target   5.0
            #pragma vertex   VertDefault
            #pragma fragment FragPrefilter4
            ENDCG
        }

        // 2
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