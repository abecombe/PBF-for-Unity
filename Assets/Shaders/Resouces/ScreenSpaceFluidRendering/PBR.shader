Shader "ScreenSpaceFluidRendering/PBR"
{
    CGINCLUDE

    #include "../../Common.hlsl"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    float _NearClipPlane;
    float _FarClipPlane;

    float2 _ClipToViewConst;

    float _Roughness = 0.01;
    float _Metallic = 0.3;
    float3 _Albedo = float3(0.0825, 0.402, 0.5785);

    inline float3 GetEyeSpacePos(float2 screen_space_uv)
    {
        const float depth = tex2D(_MainTex, screen_space_uv).r * _FarClipPlane;
        return float3(depth * (screen_space_uv * 2.0 - 1.0) * _ClipToViewConst, -depth);
    }

    inline void CalcEyeSpacePosNormViewVec(in float2 screen_space_uv, out float3 eye_space_pos, out float3 eye_space_norm, out float3 eye_space_view_vec)
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

        eye_space_view_vec = -normalize(eye_space_pos);
    }

    inline void CalcWorldSpacePosNormViewVec(
        in float3 eye_space_pos, in float3 eye_space_norm, in float3 eye_space_view_vec,
        out float3 world_space_pos, out float3 world_space_norm, out float3 world_space_view_vec)
    {
        // Transform from Eye Space to World Space
        world_space_pos = mul(UNITY_MATRIX_I_V, float4(eye_space_pos, 1)).xyz;
        world_space_norm = normalize(mul(UNITY_MATRIX_I_V, float4(eye_space_norm, 0)).xyz);
        world_space_view_vec = normalize(mul(UNITY_MATRIX_I_V, float4(eye_space_view_vec, 0)).xyz);
    }

    static const float DielectricF0 = 0.04f;
    float3 BRDF(float3 albedo, float metallic, float perceptual_roughness, float3 normal, float3 view_dir, float3 indirect_diffuse, float3 indirect_specular)
    {
        const float reflectivity = lerp(DielectricF0, 1, metallic);

        // Indirect Diffuse
        const float3 diffuse = albedo * (1 - reflectivity) * indirect_diffuse;

        // Indirect Specular
        const float ndotv = abs(dot(normal, view_dir));
        const float alpha = perceptual_roughness * perceptual_roughness;
        const float surface_reduction = 1.0 / (alpha * alpha + 1.0);
        const float3 f0 = lerp(DielectricF0, albedo, metallic);
        const float f90 = saturate(1 - perceptual_roughness + reflectivity);
        const float3 specular = surface_reduction * indirect_specular * lerp(f0, f90, pow(1 - ndotv, 5));

        return diffuse + specular;
    }

    float4 Frag(v2f_default i) : SV_Target
    {
        const float2 screen_space_uv = i.texcoord;

        float3 eye_space_pos;
        float3 eye_space_norm;
        float3 eye_space_view_vec;
        CalcEyeSpacePosNormViewVec(screen_space_uv, eye_space_pos, eye_space_norm, eye_space_view_vec);

        float3 world_space_pos;
        float3 world_space_norm;
        float3 world_space_view_vec;
        CalcWorldSpacePosNormViewVec(eye_space_pos, eye_space_norm, eye_space_view_vec, world_space_pos, world_space_norm, world_space_view_vec);

        // PBR
        const half metallic = _Metallic;
        const half perceptual_roughness = _Roughness;

        // Indirect Diffuse
        half3 indirect_diffuse = ShadeSHPerPixel(world_space_norm, 0, world_space_pos);

        // roughnessに対応する鏡面反射のミップマップレベルを求める
        const half3 refl_dir = reflect(-world_space_view_vec, world_space_norm);
        half mip = perceptual_roughness * (1.7 - 0.7 * perceptual_roughness);
        // 間接光の鏡面反射（リフレクションプローブのブレンドとかは考慮しない）
        mip *= UNITY_SPECCUBE_LOD_STEPS;
        half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, refl_dir, mip);
        half3 indirect_specular = DecodeHDR(rgbm, unity_SpecCube0_HDR);

        half3 c = BRDF(_Albedo, metallic, perceptual_roughness, world_space_norm, world_space_view_vec, indirect_diffuse, indirect_specular);

        return eye_space_pos.z != -_FarClipPlane ? float4(c, saturate(tex2D(_MainTex, screen_space_uv).a)) : 0;
    }

    ENDCG

    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" "LightMode"="ForwardBase" }

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