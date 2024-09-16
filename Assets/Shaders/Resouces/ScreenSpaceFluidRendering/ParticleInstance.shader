Shader "ScreenSpaceFluidRendering/ParticleInstance"
{
    CGINCLUDE

    #include "../../Common.hlsl"

    static const float quad_mesh_radius = 0.5f;

    StructuredBuffer<float4> _ParticleRenderingBuffer;

    float _Radius;
    float _NearClipPlane;
    float _FarClipPlane;

    struct v2f
    {
        float4 vertex : SV_POSITION;
        float3 camera_space_pos : TEXCOORD0;
        float3 camera_space_sphere_center_pos : TEXCOORD1;
        float sphere_radius : TEXCOORD2;
    };

    // --------------------------------------------------------------------
    // Vertex Shader
    // --------------------------------------------------------------------
    v2f Vertex(appdata_default v, uint id : SV_InstanceID)
    {
        const float3 sphere_world_space_pos = _ParticleRenderingBuffer[id].xyz;
        const float sphere_radius = _Radius;

        v2f o;
        const float3 camera_world_space_pos = _WorldSpaceCameraPos;
        const float3 view_vec = camera_world_space_pos - sphere_world_space_pos;
        const float sphere_cam_dist = length(view_vec);

        // UNITY_MATRIX_V = Camera.worldToCameraMatrix: 右手座標系（z軸が手前）
        const float3 z_axis = normalize(-view_vec);
        const float3 x_axis = normalize(cross(UNITY_MATRIX_V._m10_m11_m12, z_axis));
        const float3 y_axis = normalize(cross(z_axis, x_axis));

        const float nessesary_radius = sphere_cam_dist * sphere_radius / sqrt(sphere_cam_dist * sphere_cam_dist - sphere_radius * sphere_radius);
        const float scale = nessesary_radius / quad_mesh_radius;

        float4x4 mat = 0;

        mat._m00_m10_m20 = x_axis;
        mat._m01_m11_m21 = y_axis;
        mat._m02_m12_m22 = z_axis;
        mat._m00_m10_m20 *= scale;
        mat._m01_m11_m21 *= scale;
        mat._m03_m13_m23 = sphere_world_space_pos;
        mat._m33 = 1;

        const float3 world_space_pos = mul(mat, v.vertex).xyz;

        o.sphere_radius = sphere_radius;
        o.camera_space_pos = mul(UNITY_MATRIX_V, float4(world_space_pos, 1)).xyz;
        o.vertex = mul(UNITY_MATRIX_VP, float4(world_space_pos, 1));
        o.camera_space_sphere_center_pos = mul(UNITY_MATRIX_V, float4(sphere_world_space_pos, 1)).xyz;

        return o;
    }

    // --------------------------------------------------------------------
    // Fragment Shader
    // --------------------------------------------------------------------
    float4 Fragment(v2f i) : SV_Target
    {
        const float3 m = normalize(i.camera_space_pos);
        const float3 minus_a = -i.camera_space_sphere_center_pos;
        const float dot_m_minus_a = dot(m, minus_a);
        const float len_a = length(minus_a);
        const float r = i.sphere_radius;

        const float D = dot_m_minus_a * dot_m_minus_a - (len_a * len_a - r * r);
        if (D < 0) discard;

        const float depth = -((-dot_m_minus_a - sqrt(D)) * m.z);

        if (depth < _NearClipPlane || depth > _FarClipPlane) discard;

        return float4((float3)(depth / _FarClipPlane), 1);
    }

    ENDCG

    Properties
    {
    }

    SubShader
    {
        Tags{ "RenderType" = "Opaque" }

        Cull Back
        ZWrite Off
        ZTest Always
        Blend One One, One One
        BlendOp Min, Add

        Pass
        {
            CGPROGRAM
            #pragma target   5.0
            #pragma vertex   Vertex
            #pragma fragment Fragment
            ENDCG
        }
    }
}