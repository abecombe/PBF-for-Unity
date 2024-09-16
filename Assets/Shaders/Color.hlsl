#ifndef _SHADER_COLOR_HLSL_
#define _SHADER_COLOR_HLSL_

static const float PI = 3.1415926535897932384626433832795;

static const float3x3 RgbToYiq = { 0.299, 0.587, 0.114, 0.595716, -0.274453, -0.321263, 0.211456, -0.522591, 0.311135 };
static const float3x3 YiqToRgb = { 1.0, 0.9563, 0.6210, 1.0, -0.2721, -0.6474, 1.0, -1.1070, 1.7046 };
static const float3x3 LinToLms = { 3.90405e-1, 5.49941e-1, 8.92632e-3, 7.08416e-2, 9.63172e-1, 1.35775e-3, 2.31082e-2, 1.28021e-1, 9.36245e-1 };
static const float3x3 LmsToLin = { 2.85847e+0, -1.62879e+0, -2.48910e-2, -2.10182e-1, 1.15820e+0, 3.24281e-4, -4.18120e-2, -1.18169e-1, 1.06867e+0 };

float3 ConvertRgbToHsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

    const float d = q.x - min(q.w, q.y);
    const float e = 1.0e-5;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 ConvertHsvToRgb(float3 c)
{
    const float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    const float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

/**
 * \brief HSVをずらす
 */
float3 HsvShift(float3 color, float3 hsvShift)
{
    float3 hsv = ConvertRgbToHsv(color);
    hsv.x += hsvShift.x;
    hsv.x %= 1.0;
    hsv.y = hsv.y * hsvShift.y;
    hsv.z = hsv.z * hsvShift.z;
    return ConvertHsvToRgb(hsv);
}

/**
 * \brief YIQ色空間で色相をずらす
 * https://mofu-dev.com/blog/introducing-yiq/
 */
float3 HueShiftYiq(float3 color, float hueShift)
{
    float3 yColor = mul(RgbToYiq, saturate(color - 0.0000001));

    const float originalHue = atan2(yColor.b, yColor.g);
    const float finalHue = originalHue - hueShift * 2.0 * PI;

    const float chroma = sqrt(yColor.b * yColor.b + yColor.g * yColor.g);
    const float3 yFinalColor = float3(yColor.r, chroma * cos(finalHue), chroma * sin(finalHue));

    return mul(YiqToRgb, yFinalColor);
}

/**
 * \brief ホワイトバランスを調整する
 * \param c 入力色
 * \param balance ホワイトバランス (ColorUtility.ComputeColorBalance()でC#側で計算した値)
 * \return ホワイトバランス調整後の色
 */
float3 WhiteBalance(float3 c, float3 balance)
{
    float3 lms = mul(LinToLms, c);
    lms *= balance;
    return mul(LmsToLin, lms);
}

/**
 * \brief 値の強さによって色相を変える
 */
float3 CalcStrengthColor(float val)
{
    float len = length(val);
    return ConvertHsvToRgb(float3(1.0 - saturate(len), saturate(2.0 - clamp(len, 0.0, 1.25)), len));
}

/**
 * \brief コントラストを調節する
 */
float3 AdjustContrast(float3 color, float contrast)
{
    return color < 0.5 ? pow(color * 2, contrast) * 0.5 : (1.0 - pow(2.0 * (1.0 - color), contrast) * 0.5);
}

#endif /* _SHADER_COLOR_HLSL_ */