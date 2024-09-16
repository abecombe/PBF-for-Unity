#ifndef SHADER_COMMON_HLSL
#define SHADER_COMMON_HLSL

#include "UnityCG.cginc"
#include "UnityStandardUtils.cginc"

struct appdata_default
{
    float4 vertex : POSITION;
    float2 texcoord : TEXCOORD0;
};

struct v2f_default
{
    float4 vertex : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

v2f_default VertDefault(appdata_default v)
{
    v2f_default o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.texcoord = v.texcoord;
    return o;
}

#endif /* SHADER_COMMON_HLSL */