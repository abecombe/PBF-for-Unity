using System;
using System.Linq;
using UnityEngine;

[Serializable]
public class CustomAnimationCurve : IDisposable
{
    private const int CurveTextureWidth = 256;
    private const int GraphTextureWidth = 384;
    private const int GraphTextureHeight = 48;

    [SerializeField]
    private AnimationCurve _curve = new();
    private CustomKeyframe[] _keys = Array.Empty<CustomKeyframe>();
    private CustomKeyframe[] _prevCustomKeys;
    private Keyframe[] _prevKeys;

    private Texture2D _curveTex;
    private Texture2D _graphTex;

    private readonly TextureWrapMode _wrapMode;
    private readonly Color32[] _fillColorArray = Enumerable.Repeat(new Color32(0, 0, 0, 255), GraphTextureWidth * GraphTextureHeight).ToArray();

    public CustomAnimationCurve(TextureWrapMode wrapMode = TextureWrapMode.Clamp)
    {
        _wrapMode = wrapMode;
    }

    public Texture2D CurveTex()
    {
        if (_curveTex == null)
        {
            OnCurveChanged();
        }
        return _curveTex;
    }

    private void CreateTextures()
    {
        CreateCurveTexture();
        CreateGraphTexture();
    }

    private void CreateCurveTexture()
    {
        if (_curveTex == null)
        {
            _curveTex = new Texture2D(CurveTextureWidth, 1, TextureFormat.RFloat, false)
            {
                hideFlags = HideFlags.DontSave,
                wrapMode = _wrapMode
            };
        }

        for (var i = 0; i < CurveTextureWidth; i++)
        {
            var value = _curve.Evaluate((i + 0.5f) / CurveTextureWidth);
            _curveTex.SetPixel(i, 0, new Color(value, 0, 0, 1));
        }
        _curveTex.Apply();
    }

    private void CreateGraphTexture()
    {
        _graphTex ??= new Texture2D(GraphTextureWidth, GraphTextureHeight, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp
        };

        _graphTex.SetPixels32(_fillColorArray);
        for (var i = 0; i < GraphTextureWidth; i++)
        {
            var value = _curve.Evaluate((i + 0.5f) / GraphTextureWidth);
            _graphTex.SetPixel(i, (int)(value * GraphTextureHeight), Color.yellow);
        }
        _graphTex.Apply();
    }

    private bool CheckKeysChanged()
    {
        var currKeys = _keys;
        var prevKeys = _prevCustomKeys ?? _keys;

        _prevCustomKeys = (CustomKeyframe[])currKeys.Clone();

        return currKeys.Length != prevKeys.Length || currKeys.Where((key, i) => !key.Equals(prevKeys[i])).Any();
    }
    private void OnKeysChanged()
    {
        _curve.keys = _keys.Select(k => (Keyframe)k).ToArray();
        _prevKeys = (Keyframe[])_curve.keys.Clone();

        CreateTextures();
    }

    private bool CheckCurveChanged()
    {
        var currKeys = _curve.keys;
        var prevKeys = _prevKeys ?? _curve.keys;

        _prevKeys = (Keyframe[])currKeys.Clone();

        return currKeys.Length != prevKeys.Length || currKeys.Where((key, i) => !key.Equals(prevKeys[i])).Any();
    }
    private void OnCurveChanged()
    {
        _keys = _curve.keys.Select(k => (CustomKeyframe)k).ToArray();
        _prevCustomKeys = (CustomKeyframe[])_keys.Clone();

        CreateTextures();
    }

    // public RowElement CreateCurveElement(string label)
    // {
    //     if (_curveTex is null) OnCurveChanged();
    //
    //     return UI.Row(
    //         UI.Image(_graphTex).SetHeight(40f).SetWidth(320f),
    //         UI.WindowLauncher("Edit Curve - " + label,
    //             UI.Window("Edit Curve - " + label,
    //                 UI.Image(_graphTex).SetHeight(80f).SetWidth(640f),
    //                 CustomUI.BlankLine(),
    //                 UI.Field("KeyFrames", () => _keys)
    //             )
    //         )
    //     );
    // }

    public void Tick()
    {
        bool change;

        change = CheckKeysChanged();
        if (change) OnKeysChanged();

        change = CheckCurveChanged();
        if (change) OnCurveChanged();
    }

    public void Dispose()
    {
        if (_curveTex is not null) { Texture2D.Destroy(_curveTex); _curveTex = null; }
        if (_graphTex is not null) { Texture2D.Destroy(_graphTex); _graphTex = null; }
    }
}

[Serializable]
public struct CustomKeyframe
{
    public float Time;
    public float Value;
    public float InOutTangent;

    public static implicit operator Keyframe(CustomKeyframe value)
    {
        return new Keyframe(value.Time, value.Value, value.InOutTangent, value.InOutTangent);
    }

    public static implicit operator CustomKeyframe(Keyframe value)
    {
        return new CustomKeyframe { Time = value.time, Value = value.value, InOutTangent = value.inTangent };
    }
}