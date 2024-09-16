Shader "ScreenSpaceFluidRendering/NarrowRangeFilter"
{
    CGINCLUDE

    #include "../../Common.hlsl"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    float _ParticleRadius;

    float _NearClipPlane;
    float _FarClipPlane;

    float2 _Direction;

    int _FilterRadius;
    float _ThresholdRatio;
    float _ClampRatio;

    inline float GetDepthNormalized(float2 texcoord)
    {
        return tex2D(_MainTex, texcoord).r;
    }

    inline float GetDepth(float2 texcoord)
    {
        return tex2D(_MainTex, texcoord).r * _FarClipPlane;
    }

    // Reference from Narrow-Range Filter in this thesis by Nghia Truong and Cem Yuksel.
    // https://ttnghia.github.io/pdf/NarrowRangeFilter.pdf

    float Frag1D(v2f_default i) : SV_Target
    {
        const float depth_normalized = GetDepthNormalized(i.texcoord);
        const float depth = depth_normalized * _FarClipPlane;

        const float2 direction = normalize(_Direction) * _MainTex_TexelSize.xy;

        const float threshold   = _ParticleRadius * _ThresholdRatio;
        const float upper       = depth - threshold;
        const float lower       = depth + threshold;
        const float lower_clamp = depth + _ParticleRadius * _ClampRatio;

        float sum  = 0;
        float wsum = 0;
        const float variance = 2.0 * _FilterRadius * _FilterRadius / 9.0;
        for (int x = -_FilterRadius; x <= _FilterRadius; x++)
        {
            float sample_depth = GetDepth(i.texcoord + direction * x);
            float w = exp(-x*x / variance);
            if (sample_depth < upper) {
                w = 0;
            } else if (sample_depth > lower) {
                sample_depth = lower_clamp;
            }
            sum += sample_depth * w;
            wsum += w;
        }
        const float final_depth = depth_normalized < 1 ? sum / wsum / _FarClipPlane : 1;

        return final_depth;
    }

    float Frag2D(v2f_default i) : SV_Target
    {
        const float depth_normalized = GetDepthNormalized(i.texcoord);
        const float depth = depth_normalized * _FarClipPlane;

        const float2 inv_res = _MainTex_TexelSize.xy;

        const float threshold   = _ParticleRadius * _ThresholdRatio;
        const float upper       = depth - threshold;
        const float lower       = depth + threshold;
        const float lower_clamp = depth + _ParticleRadius * _ClampRatio;

        float sum  = 0;
        float wsum = 0;
        const float variance = 2.0 * _FilterRadius * _FilterRadius / 9.0;
        for (int x = -_FilterRadius; x <= _FilterRadius; x++)
        {
            for (int y = -_FilterRadius; y <= _FilterRadius; y++)
            {
                if (x * x + y * y <= _FilterRadius * _FilterRadius)
                {
                    float sample_depth = GetDepth(i.texcoord + float2(inv_res.x * x, inv_res.y * y));
                    float w = exp((-x*x-y*y) / variance);
                    if (sample_depth < upper) {
                        w = 0;
                    } else if (sample_depth > lower) {
                        sample_depth = lower_clamp;
                    }
                    sum += sample_depth * w;
                    wsum += w;
                }
            }
        }
        const float final_depth = depth_normalized < 1 ? sum / wsum / _FarClipPlane : 1;

        return final_depth;
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
            #pragma fragment Frag1D
            ENDCG
        }

        // 1
        Pass
        {
            CGPROGRAM
            #pragma target   5.0
            #pragma vertex   VertDefault
            #pragma fragment Frag2D
            ENDCG
        }
    }
}