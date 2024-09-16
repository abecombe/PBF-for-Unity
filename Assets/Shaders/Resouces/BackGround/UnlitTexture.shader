Shader "BackGround/UnlitTexture"
{
    CGINCLUDE

    #include "../../Common.hlsl"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    float4 Frag(v2f_default i) : SV_Target
    {
        return tex2D(_MainTex, i.texcoord);
    }

    ENDCG

    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags{ "RenderType" = "Opaque" }

        Cull Back
        Blend One Zero
        BlendOp Add

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