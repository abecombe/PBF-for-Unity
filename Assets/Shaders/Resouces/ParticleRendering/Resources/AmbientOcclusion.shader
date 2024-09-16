Shader "ParticleRendering/AmbientOcclusion"
{
    CGINCLUDE

    #include "../../../Common.hlsl"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    float _NearClipPlane;
    float _FarClipPlane;

    float2 _ClipToViewConst;

    StructuredBuffer<float3> _SamplingPointsBuffer;
    int _SampleCount;
    float _AmbientOcclusionSampleLength;
    float _AmbientOcclusionMaxRate;

    inline float3 GetEyeSpacePos(float2 screen_space_uv)
    {
        const float depth = -tex2D(_MainTex, screen_space_uv).a * _FarClipPlane;
        return float3(-depth * (screen_space_uv * 2.0 - 1.0) * _ClipToViewConst, depth);
    }

    inline void CalcEyeSpacePosNorm(in float2 screen_space_uv, out float3 eye_space_pos, out float3 eye_space_norm)
    {
        eye_space_pos = GetEyeSpacePos(screen_space_uv);

        const float2 inv_res = _MainTex_TexelSize.xy;
        float3 ddx = GetEyeSpacePos(screen_space_uv + float2(inv_res.x, 0)) - eye_space_pos;
        float3 ddx2 = eye_space_pos - GetEyeSpacePos(screen_space_uv - float2(inv_res.x, 0));
        ddx = abs(ddx.z) > abs(ddx2.z) ? ddx2 : ddx;
        float3 ddy = GetEyeSpacePos(screen_space_uv + float2(0, inv_res.y)) - eye_space_pos;
        float3 ddy2 = eye_space_pos - GetEyeSpacePos(screen_space_uv - float2(0, inv_res.y));
        ddy = abs(ddy.z) > abs(ddy2.z) ? ddy2 : ddy;
        eye_space_norm = normalize(cross(ddx, ddy));
    }

    inline bool CompareEyeSpaceZvsRealDepth(float3 eye_space_pos) // if true, the point is occluded
    {
        const float2 screen_space_uv = (eye_space_pos.xy / (-eye_space_pos.z) / _ClipToViewConst + 1.0) * 0.5;
        const float dist = -eye_space_pos.z - tex2D(_MainTex, screen_space_uv).a * _FarClipPlane;
        return dist > 0 && dist < 1.0f;
    }

    float3x3 GetTBNMatrix(float3 eye_space_normal)
    {
        const float3 tangent = float3(1, 0, 0);
        const float3 bitangent = float3(0, 1, 0);
        const float3 normal = eye_space_normal;
        return float3x3(tangent, bitangent, normal);
    }

    float4 Frag(v2f_default i) : SV_Target
    {
        float3 eye_space_pos;
        float3 eye_space_norm;
        CalcEyeSpacePosNorm(i.texcoord, eye_space_pos, eye_space_norm);

        int counter = 0;
        for (int j = 0; j < _SampleCount; j++)
        {
            float3 offset = _SamplingPointsBuffer[j] * _AmbientOcclusionSampleLength;
            offset = mul(GetTBNMatrix(eye_space_norm), (float3x1)offset);
            if (CompareEyeSpaceZvsRealDepth(eye_space_pos + offset)) counter++;
        }

        if (tex2D(_MainTex, i.texcoord).a == 0) return float4(0, 0, 0, 0);

        return float4(lerp(tex2D(_MainTex, i.texcoord).rgb, (float3)0, 0), 1);
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