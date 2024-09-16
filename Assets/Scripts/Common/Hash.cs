using Unity.Mathematics;

public static class Hash
{
    // Using Permuted Congruential Generator
    // Original source code: https://www.shadertoy.com/view/XlGcRh

    private static uint UInt1Base(uint value)
    {
        value = value * 747796405u + 2891336453u;
        value = ((value >> (int)((value >> 28) + 4u)) ^ value) * 277803737u;
        return (value >> 22) ^ value;
    }
    private static uint2 UInt2Base(uint2 value)
    {
        value = value * 1664525u + 1013904223u;
        value.x += value.y * 1664525u;
        value.y += value.x * 1664525u;
        value ^= value >> 16;
        value.x += value.y * 1664525u;
        value.y += value.x * 1664525u;
        value ^= value >> 16;
        return value;
    }
    private static uint3 UInt3Base(uint3 value)
    {
        value = value * 1664525u + 1013904223u;
        value.x += value.y * value.z;
        value.y += value.z * value.x;
        value.z += value.x * value.y;
        value ^= value >> 16;
        value.x += value.y * value.z;
        value.y += value.z * value.x;
        value.z += value.x * value.y;
        return value;
    }
    private static uint4 UInt4Base(uint4 value)
    {
        value = value * 1664525u + 1013904223u;
        value.x += value.y * value.w;
        value.y += value.z * value.x;
        value.z += value.x * value.y;
        value.w += value.y * value.z;
        value ^= value >> 16;
        value.x += value.y * value.w;
        value.y += value.z * value.x;
        value.z += value.x * value.y;
        value.w += value.y * value.z;
        return value;
    }

    // random uint => random float[0, 1)
    private static float UInt2Float01(uint value)
    {
        return (value & 0x00ffffffu) * math.asfloat(0x33800000);
    }
    private static float2 UInt2Float01(uint2 value)
    {
        return (float2)(value & 0x00ffffffu) * math.asfloat(0x33800000);
    }
    private static float3 UInt2Float01(uint3 value)
    {
        return (float3)(value & 0x00ffffffu) * math.asfloat(0x33800000);
    }
    private static float4 UInt2Float01(uint4 value)
    {
        return (float4)(value & 0x00ffffffu) * math.asfloat(0x33800000);
    }
    // random uint => random bool
    private static bool UInt2Bool(uint value)
    {
        return (value & 0x00000001u) == 1;
    }
    private static bool2 UInt2Bool(uint2 value)
    {
        return (value & 0x00000001u) == 1;
    }
    private static bool3 UInt2Bool(uint3 value)
    {
        return (value & 0x00000001u) == 1;
    }
    private static bool4 UInt2Bool(uint4 value)
    {
        return (value & 0x00000001u) == 1;
    }

    // uint => uint
    public static uint UInt(uint value)
    {
        return UInt1Base(value);
    }
    public static uint UInt(uint2 value)
    {
        return UInt2Base(value).y;
    }
    public static uint UInt(uint3 value)
    {
        return UInt3Base(value).z;
    }
    public static uint UInt(uint4 value)
    {
        return UInt4Base(value).w;
    }

    public static uint2 UInt2(uint value)
    {
        return UInt2Base(new uint2(value, ~value));
    }
    public static uint2 UInt2(uint2 value)
    {
        return UInt2Base(value);
    }
    public static uint2 UInt2(uint3 value)
    {
        return UInt3Base(value).yz;
    }
    public static uint2 UInt2(uint4 value)
    {
        return UInt4Base(value).zw;
    }

    public static uint3 UInt3(uint value)
    {
        return UInt3Base(new uint3(value, ~value, value << 1));
    }
    public static uint3 UInt3(uint2 value)
    {
        return UInt3Base(new uint3(value.x, value.y, ~value.x));
    }
    public static uint3 UInt3(uint3 value)
    {
        return UInt3Base(value);
    }
    public static uint3 UInt3(uint4 value)
    {
        return UInt4Base(value).yzw;
    }

    public static uint4 UInt4(uint value)
    {
        return UInt4Base(new uint4(value, ~value, value << 1, ~value << 1));
    }
    public static uint4 UInt4(uint2 value)
    {
        return UInt4Base(new uint4(value.x, value.y, ~value.x, ~value.y));
    }
    public static uint4 UInt4(uint3 value)
    {
        return UInt4Base(new uint4(value.x, value.y, value.z, ~value.x));
    }
    public static uint4 UInt4(uint4 value)
    {
        return UInt4Base(value);
    }

    // float => uint
    public static uint UInt(float value)
    {
        return UInt(math.asuint(value));
    }
    public static uint UInt(float2 value)
    {
        return UInt(math.asuint(value));
    }
    public static uint UInt(float3 value)
    {
        return UInt(math.asuint(value));
    }
    public static uint UInt(float4 value)
    {
        return UInt(math.asuint(value));
    }

    public static uint2 UInt2(float value)
    {
        return UInt2(math.asuint(value));
    }
    public static uint2 UInt2(float2 value)
    {
        return UInt2(math.asuint(value));
    }
    public static uint2 UInt2(float3 value)
    {
        return UInt2(math.asuint(value));
    }
    public static uint2 UInt2(float4 value)
    {
        return UInt2(math.asuint(value));
    }

    public static uint3 UInt3(float value)
    {
        return UInt3(math.asuint(value));
    }
    public static uint3 UInt3(float2 value)
    {
        return UInt3(math.asuint(value));
    }
    public static uint3 UInt3(float3 value)
    {
        return UInt3(math.asuint(value));
    }
    public static uint3 UInt3(float4 value)
    {
        return UInt3(math.asuint(value));
    }

    public static uint4 UInt4(float value)
    {
        return UInt4(math.asuint(value));
    }
    public static uint4 UInt4(float2 value)
    {
        return UInt4(math.asuint(value));
    }
    public static uint4 UInt4(float3 value)
    {
        return UInt4(math.asuint(value));
    }
    public static uint4 UInt4(float4 value)
    {
        return UInt4(math.asuint(value));
    }

    // uint => float[0, 1)
    public static float Float(uint value)
    {
        return UInt2Float01(UInt(value));
    }
    public static float Float(uint2 value)
    {
        return UInt2Float01(UInt(value));
    }
    public static float Float(uint3 value)
    {
        return UInt2Float01(UInt(value));
    }
    public static float Float(uint4 value)
    {
        return UInt2Float01(UInt(value));
    }

    public static float2 Float2(uint value)
    {
        return UInt2Float01(UInt2(value));
    }
    public static float2 Float2(uint2 value)
    {
        return UInt2Float01(UInt2(value));
    }
    public static float2 Float2(uint3 value)
    {
        return UInt2Float01(UInt2(value));
    }
    public static float2 Float2(uint4 value)
    {
        return UInt2Float01(UInt2(value));
    }

    public static float3 Float3(uint value)
    {
        return UInt2Float01(UInt3(value));
    }
    public static float3 Float3(uint2 value)
    {
        return UInt2Float01(UInt3(value));
    }
    public static float3 Float3(uint3 value)
    {
        return UInt2Float01(UInt3(value));
    }
    public static float3 Float3(uint4 value)
    {
        return UInt2Float01(UInt3(value));
    }

    public static float4 Float4(uint value)
    {
        return UInt2Float01(UInt4(value));
    }
    public static float4 Float4(uint2 value)
    {
        return UInt2Float01(UInt4(value));
    }
    public static float4 Float4(uint3 value)
    {
        return UInt2Float01(UInt4(value));
    }
    public static float4 Float4(uint4 value)
    {
        return UInt2Float01(UInt4(value));
    }

    // float => float[0, 1)
    public static float Float(float value)
    {
        return UInt2Float01(UInt(value));
    }
    public static float Float(float2 value)
    {
        return UInt2Float01(UInt(value));
    }
    public static float Float(float3 value)
    {
        return UInt2Float01(UInt(value));
    }
    public static float Float(float4 value)
    {
        return UInt2Float01(UInt(value));
    }

    public static float2 Float2(float value)
    {
        return UInt2Float01(UInt2(value));
    }
    public static float2 Float2(float2 value)
    {
        return UInt2Float01(UInt2(value));
    }
    public static float2 Float2(float3 value)
    {
        return UInt2Float01(UInt2(value));
    }
    public static float2 Float2(float4 value)
    {
        return UInt2Float01(UInt2(value));
    }

    public static float3 Float3(float value)
    {
        return UInt2Float01(UInt3(value));
    }
    public static float3 Float3(float2 value)
    {
        return UInt2Float01(UInt3(value));
    }
    public static float3 Float3(float3 value)
    {
        return UInt2Float01(UInt3(value));
    }
    public static float3 Float3(float4 value)
    {
        return UInt2Float01(UInt3(value));
    }

    public static float4 Float4(float value)
    {
        return UInt2Float01(UInt4(value));
    }
    public static float4 Float4(float2 value)
    {
        return UInt2Float01(UInt4(value));
    }
    public static float4 Float4(float3 value)
    {
        return UInt2Float01(UInt4(value));
    }
    public static float4 Float4(float4 value)
    {
        return UInt2Float01(UInt4(value));
    }

    // uint => range[min, max)
    public static float Range(uint value, float min, float max)
    {
        return Float(value) * (max - min) + min;
    }
    public static float Range(uint2 value, float min, float max)
    {
        return Float(value) * (max - min) + min;
    }
    public static float Range(uint3 value, float min, float max)
    {
        return Float(value) * (max - min) + min;
    }
    public static float Range(uint4 value, float min, float max)
    {
        return Float(value) * (max - min) + min;
    }

    public static float2 Range2(uint value, float2 min, float2 max)
    {
        return Float2(value) * (max - min) + min;
    }
    public static float2 Range2(uint2 value, float2 min, float2 max)
    {
        return Float2(value) * (max - min) + min;
    }
    public static float2 Range2(uint3 value, float2 min, float2 max)
    {
        return Float2(value) * (max - min) + min;
    }
    public static float2 Range2(uint4 value, float2 min, float2 max)
    {
        return Float2(value) * (max - min) + min;
    }

    public static float3 Range3(uint value, float3 min, float3 max)
    {
        return Float3(value) * (max - min) + min;
    }
    public static float3 Range3(uint2 value, float3 min, float3 max)
    {
        return Float3(value) * (max - min) + min;
    }
    public static float3 Range3(uint3 value, float3 min, float3 max)
    {
        return Float3(value) * (max - min) + min;
    }
    public static float3 Range3(uint4 value, float3 min, float3 max)
    {
        return Float3(value) * (max - min) + min;
    }

    public static float4 Range4(uint value, float4 min, float4 max)
    {
        return Float4(value) * (max - min) + min;
    }
    public static float4 Range4(uint2 value, float4 min, float4 max)
    {
        return Float4(value) * (max - min) + min;
    }
    public static float4 Range4(uint3 value, float4 min, float4 max)
    {
        return Float4(value) * (max - min) + min;
    }
    public static float4 Range4(uint4 value, float4 min, float4 max)
    {
        return Float4(value) * (max - min) + min;
    }

    // float => range[min, max)
    public static float Range(float value, float min, float max)
    {
        return Float(value) * (max - min) + min;
    }
    public static float Range(float2 value, float min, float max)
    {
        return Float(value) * (max - min) + min;
    }
    public static float Range(float3 value, float min, float max)
    {
        return Float(value) * (max - min) + min;
    }
    public static float Range(float4 value, float min, float max)
    {
        return Float(value) * (max - min) + min;
    }

    public static float2 Range2(float value, float2 min, float2 max)
    {
        return Float2(value) * (max - min) + min;
    }
    public static float2 Range2(float2 value, float2 min, float2 max)
    {
        return Float2(value) * (max - min) + min;
    }
    public static float2 Range2(float3 value, float2 min, float2 max)
    {
        return Float2(value) * (max - min) + min;
    }
    public static float2 Range2(float4 value, float2 min, float2 max)
    {
        return Float2(value) * (max - min) + min;
    }

    public static float3 Range3(float value, float3 min, float3 max)
    {
        return Float3(value) * (max - min) + min;
    }
    public static float3 Range3(float2 value, float3 min, float3 max)
    {
        return Float3(value) * (max - min) + min;
    }
    public static float3 Range3(float3 value, float3 min, float3 max)
    {
        return Float3(value) * (max - min) + min;
    }
    public static float3 Range3(float4 value, float3 min, float3 max)
    {
        return Float3(value) * (max - min) + min;
    }

    public static float4 Range4(float value, float4 min, float4 max)
    {
        return Float4(value) * (max - min) + min;
    }
    public static float4 Range4(float2 value, float4 min, float4 max)
    {
        return Float4(value) * (max - min) + min;
    }
    public static float4 Range4(float3 value, float4 min, float4 max)
    {
        return Float4(value) * (max - min) + min;
    }
    public static float4 Range4(float4 value, float4 min, float4 max)
    {
        return Float4(value) * (max - min) + min;
    }

    // uint => bool
    public static bool Bool(uint value)
    {
        return UInt2Bool(UInt(value));
    }
    public static bool Bool(uint2 value)
    {
        return UInt2Bool(UInt(value));
    }
    public static bool Bool(uint3 value)
    {
        return UInt2Bool(UInt(value));
    }
    public static bool Bool(uint4 value)
    {
        return UInt2Bool(UInt(value));
    }

    public static bool2 Bool2(uint value)
    {
        return UInt2Bool(UInt2(value));
    }
    public static bool2 Bool2(uint2 value)
    {
        return UInt2Bool(UInt2(value));
    }
    public static bool2 Bool2(uint3 value)
    {
        return UInt2Bool(UInt2(value));
    }
    public static bool2 Bool2(uint4 value)
    {
        return UInt2Bool(UInt2(value));
    }

    public static bool3 Bool3(uint value)
    {
        return UInt2Bool(UInt3(value));
    }
    public static bool3 Bool3(uint2 value)
    {
        return UInt2Bool(UInt3(value));
    }
    public static bool3 Bool3(uint3 value)
    {
        return UInt2Bool(UInt3(value));
    }
    public static bool3 Bool3(uint4 value)
    {
        return UInt2Bool(UInt3(value));
    }

    public static bool4 Bool4(uint value)
    {
        return UInt2Bool(UInt4(value));
    }
    public static bool4 Bool4(uint2 value)
    {
        return UInt2Bool(UInt4(value));
    }
    public static bool4 Bool4(uint3 value)
    {
        return UInt2Bool(UInt4(value));
    }
    public static bool4 Bool4(uint4 value)
    {
        return UInt2Bool(UInt4(value));
    }

    // float => bool
    public static bool Bool(float value)
    {
        return UInt2Bool(UInt(value));
    }
    public static bool Bool(float2 value)
    {
        return UInt2Bool(UInt(value));
    }
    public static bool Bool(float3 value)
    {
        return UInt2Bool(UInt(value));
    }
    public static bool Bool(float4 value)
    {
        return UInt2Bool(UInt(value));
    }

    public static bool2 Bool2(float value)
    {
        return UInt2Bool(UInt2(value));
    }
    public static bool2 Bool2(float2 value)
    {
        return UInt2Bool(UInt2(value));
    }
    public static bool2 Bool2(float3 value)
    {
        return UInt2Bool(UInt2(value));
    }
    public static bool2 Bool2(float4 value)
    {
        return UInt2Bool(UInt2(value));
    }

    public static bool3 Bool3(float value)
    {
        return UInt2Bool(UInt3(value));
    }
    public static bool3 Bool3(float2 value)
    {
        return UInt2Bool(UInt3(value));
    }
    public static bool3 Bool3(float3 value)
    {
        return UInt2Bool(UInt3(value));
    }
    public static bool3 Bool3(float4 value)
    {
        return UInt2Bool(UInt3(value));
    }

    public static bool4 Bool4(float value)
    {
        return UInt2Bool(UInt4(value));
    }
    public static bool4 Bool4(float2 value)
    {
        return UInt2Bool(UInt4(value));
    }
    public static bool4 Bool4(float3 value)
    {
        return UInt2Bool(UInt4(value));
    }
    public static bool4 Bool4(float4 value)
    {
        return UInt2Bool(UInt4(value));
    }

    // random on unit circle
    private static float2 RandomOnUnitCircleBase(float float01)
    {
        float theta = 2f * math.PI * float01;
        float2 ret;
        ret.x = math.cos(theta);
        ret.y = math.sin(theta);
        return ret;
    }
    public static float2 RandomOnUnitCircle(uint value)
    {
        return RandomOnUnitCircleBase(Float(value));
    }
    public static float2 RandomOnUnitCircle(uint2 value)
    {
        return RandomOnUnitCircleBase(Float(value));
    }
    public static float2 RandomOnUnitCircle(uint3 value)
    {
        return RandomOnUnitCircleBase(Float(value));
    }
    public static float2 RandomOnUnitCircle(uint4 value)
    {
        return RandomOnUnitCircleBase(Float(value));
    }
    public static float2 RandomOnUnitCircle(float value)
    {
        return RandomOnUnitCircleBase(Float(value));
    }
    public static float2 RandomOnUnitCircle(float2 value)
    {
        return RandomOnUnitCircleBase(Float(value));
    }
    public static float2 RandomOnUnitCircle(float3 value)
    {
        return RandomOnUnitCircleBase(Float(value));
    }
    public static float2 RandomOnUnitCircle(float4 value)
    {
        return RandomOnUnitCircleBase(Float(value));
    }

    // random in unit circle
    private static float2 RandomInUnitCircleBase(float2 float01)
    {
        float theta = 2f * math.PI * float01.x;
        float r = math.sqrt(float01.y);
        float2 ret;
        ret.x = r * math.cos(theta);
        ret.y = r * math.sin(theta);
        return ret;
    }
    public static float2 RandomInUnitCircle(uint value)
    {
        return RandomInUnitCircleBase(Float2(value));
    }
    public static float2 RandomInUnitCircle(uint2 value)
    {
        return RandomInUnitCircleBase(Float2(value));
    }
    public static float2 RandomInUnitCircle(uint3 value)
    {
        return RandomInUnitCircleBase(Float2(value));
    }
    public static float2 RandomInUnitCircle(uint4 value)
    {
        return RandomInUnitCircleBase(Float2(value));
    }
    public static float2 RandomInUnitCircle(float value)
    {
        return RandomInUnitCircleBase(Float2(value));
    }
    public static float2 RandomInUnitCircle(float2 value)
    {
        return RandomInUnitCircleBase(Float2(value));
    }
    public static float2 RandomInUnitCircle(float3 value)
    {
        return RandomInUnitCircleBase(Float2(value));
    }
    public static float2 RandomInUnitCircle(float4 value)
    {
        return RandomInUnitCircleBase(Float2(value));
    }

    // random on unit sphere
    private static float3 RandomOnUnitSphereBase(float2 float01)
    {
        float cosTheta = -2f * float01.x + 1f;
        float sinTheta = math.sqrt(1f - cosTheta * cosTheta);
        float phi = 2f * math.PI * float01.y;
        float3 ret;
        ret.x = sinTheta * math.cos(phi);
        ret.y = sinTheta * math.sin(phi);
        ret.z = cosTheta;
        return ret;
    }
    public static float3 RandomOnUnitSphere(uint value)
    {
        return RandomOnUnitSphereBase(Float2(value));
    }
    public static float3 RandomOnUnitSphere(uint2 value)
    {
        return RandomOnUnitSphereBase(Float2(value));
    }
    public static float3 RandomOnUnitSphere(uint3 value)
    {
        return RandomOnUnitSphereBase(Float2(value));
    }
    public static float3 RandomOnUnitSphere(uint4 value)
    {
        return RandomOnUnitSphereBase(Float2(value));
    }
    public static float3 RandomOnUnitSphere(float value)
    {
        return RandomOnUnitSphereBase(Float2(value));
    }
    public static float3 RandomOnUnitSphere(float2 value)
    {
        return RandomOnUnitSphereBase(Float2(value));
    }
    public static float3 RandomOnUnitSphere(float3 value)
    {
        return RandomOnUnitSphereBase(Float2(value));
    }
    public static float3 RandomOnUnitSphere(float4 value)
    {
        return RandomOnUnitSphereBase(Float2(value));
    }

    // random in unit sphere
    private static float3 RandomInUnitSphereBase(float3 float01)
    {
        float cosTheta = -2f * float01.x + 1f;
        float sinTheta = math.sqrt(1f - cosTheta * cosTheta);
        float phi = 2f * math.PI * float01.y;
        float r = math.pow(float01.z, 1f / 3f);
        float3 ret;
        ret.x = r * sinTheta * math.cos(phi);
        ret.y = r * sinTheta * math.sin(phi);
        ret.z = r * cosTheta;
        return ret;
    }
    public static float3 RandomInUnitSphere(uint value)
    {
        return RandomInUnitSphereBase(Float3(value));
    }
    public static float3 RandomInUnitSphere(uint2 value)
    {
        return RandomInUnitSphereBase(Float3(value));
    }
    public static float3 RandomInUnitSphere(uint3 value)
    {
        return RandomInUnitSphereBase(Float3(value));
    }
    public static float3 RandomInUnitSphere(uint4 value)
    {
        return RandomInUnitSphereBase(Float3(value));
    }
    public static float3 RandomInUnitSphere(float value)
    {
        return RandomInUnitSphereBase(Float3(value));
    }
    public static float3 RandomInUnitSphere(float2 value)
    {
        return RandomInUnitSphereBase(Float3(value));
    }
    public static float3 RandomInUnitSphere(float3 value)
    {
        return RandomInUnitSphereBase(Float3(value));
    }
    public static float3 RandomInUnitSphere(float4 value)
    {
        return RandomInUnitSphereBase(Float3(value));
    }

    // random in triangle
    private static float3 RandomInTriangleBase(float3 p0, float3 p1, float3 p2, float2 float01)
    {
        return (1f - math.sqrt(float01.x)) * p0 + math.sqrt(float01.x) * (1f - float01.y) * p1 + math.sqrt(float01.x) * float01.y * p2;
    }
    public static float3 RandomInTriangle(float3 p0, float3 p1, float3 p2, uint value)
    {
        return RandomInTriangleBase(p0, p1, p2, Float2(value));
    }
    public static float3 RandomInTriangle(float3 p0, float3 p1, float3 p2, uint2 value)
    {
        return RandomInTriangleBase(p0, p1, p2, Float2(value));
    }
    public static float3 RandomInTriangle(float3 p0, float3 p1, float3 p2, uint3 value)
    {
        return RandomInTriangleBase(p0, p1, p2, Float2(value));
    }
    public static float3 RandomInTriangle(float3 p0, float3 p1, float3 p2, uint4 value)
    {
        return RandomInTriangleBase(p0, p1, p2, Float2(value));
    }
    public static float3 RandomInTriangle(float3 p0, float3 p1, float3 p2, float value)
    {
        return RandomInTriangleBase(p0, p1, p2, Float2(value));
    }
    public static float3 RandomInTriangle(float3 p0, float3 p1, float3 p2, float2 value)
    {
        return RandomInTriangleBase(p0, p1, p2, Float2(value));
    }
    public static float3 RandomInTriangle(float3 p0, float3 p1, float3 p2, float3 value)
    {
        return RandomInTriangleBase(p0, p1, p2, Float2(value));
    }
    public static float3 RandomInTriangle(float3 p0, float3 p1, float3 p2, float4 value)
    {
        return RandomInTriangleBase(p0, p1, p2, Float2(value));
    }
}