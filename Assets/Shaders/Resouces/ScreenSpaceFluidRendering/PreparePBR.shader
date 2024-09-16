Shader "ScreenSpaceFluidRendering/PreparePBR"
{
    CGINCLUDE

    #include "../../Common.hlsl"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    sampler2D _FilterTex;
    float4 _FilterTex_TexelSize;

    sampler2D _BlurTex;
    float4 _BlurTex_TexelSize;

    float4 Frag(v2f_default i) : SV_Target
    {
        return float4((float3)tex2D(_FilterTex, i.texcoord).r, tex2D(_BlurTex, i.texcoord).r);
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