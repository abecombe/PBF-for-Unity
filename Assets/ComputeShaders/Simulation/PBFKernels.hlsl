#ifndef CS_SIMULATION_PBF_KERNEL_HLSL
#define CS_SIMULATION_PBF_KERNEL_HLSL

static const int grid_range[6] = { -1, 1, -1, 1, -1, 1 };

float2 _Poly6WCoefs; // 315 / (64 * PI * h^9), -945 / (32 * PI * h^9)
float2 _SpikyWCoefs; // 15 / (PI * h^6), -45 / (PI * h^6)

float2 _CohesionWCoefs; // 32 / (PI * h^9), -h^6 / 64

inline float Poly6W(float3 r, float h)
{
    const float r_sq = dot(r, r);
    const float h_sq = h * h;
    const float q = h_sq - r_sq;

    return r_sq < h_sq ? _Poly6WCoefs.x * q * q * q : 0;
}
inline float Poly6W(float r_len, float h)
{
    const float r_sq = r_len * r_len;
    const float h_sq = h * h;
    const float q = h_sq - r_sq;

    return r_sq < h_sq ? _Poly6WCoefs.x * q * q * q : 0;
}

inline float3 Poly6WGrad(float3 r, float h)
{
    const float r_sq = dot(r, r);
    const float h_sq = h * h;
    const float q = h_sq - r_sq;

    return r_sq < h_sq ? _Poly6WCoefs.y * q * q * r : 0;
}

inline float SpikyW(float3 r, float h)
{
    const float r_len = length(r);
    const float q = h - r_len;

    return r_len < h ? _SpikyWCoefs.x * q * q * q : 0;
}

inline float3 SpikyWGrad(float3 r, float h)
{
    const float r_len = length(r);
    const float q = h - r_len;

    return 0 < r_len && r_len < h ? _SpikyWCoefs.y * q * q * r / r_len : 0;
}

inline float3 W(float3 r, float h)
{
    return Poly6W(r, h);
}
inline float3 W(float r_len, float h)
{
    return Poly6W(r_len, h);
}

inline float3 GradW(float3 r, float h)
{
    return SpikyWGrad(r, h);
}

inline float CohesionW(float r, float h)
{
    const float r3 = r * r * r;
    const float q = h - r;
    const float q3 = q * q * q;

    return
        r > h ? 0.0 :
        r > 0.5 * h ? _CohesionWCoefs.x * q3 * r3
        : 2.0 * _CohesionWCoefs.x * q3 * r3 + _CohesionWCoefs.y;
}

#endif /* CS_SIMULATION_PBF_KERNEL_HLSL */