using Abecombe.GPUUtil;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class BackGroundController : MonoBehaviour
{
    [SerializeField] private int _blurPreshrink = 4;
    [SerializeField] private int _blurIterations = 1;
    [SerializeField] private int _blurSampleStep = 1;

    [SerializeField] [Range(0f, 1f)] private float _hueShift = 0f;
    [SerializeField] [Range(0f, 2f)] private float _saturationMultiplier = 1f;
    [SerializeField] [Range(0f, 5f)] private float _valueMultiplier = 1f;
    [SerializeField] [Range(0f, 1f)] private float _hueShiftYiq = 0f;
    [SerializeField] [Range(-2f, 2f)] private float _blackLevel = 0f;
    [SerializeField] [Range(-2f, 2f)] private float _whiteLevel = 1f;
    [SerializeField] [Range(0f, 10f)] private float _gamma = 1f;
    [SerializeField] [Range(0f, 5f)] private float _contrast = 1f;
    [SerializeField] [Range(0f, 50f)] private float _exposure = 1f;

    [SerializeField] private Renderer _renderer;

    private Camera _camera;
    private Camera _mainCamera;

    private GPUTexture2D _cameraTargetTexture = new();

    private Material _blurMaterial;
    private Material _colorAdjustMaterial;

    private CommandBuffer _commandBuffer;

    private void OnEnable()
    {
        _camera = gameObject.GetComponentInChildren<Camera>();

        _mainCamera = Camera.main;

        _camera.fieldOfView = _mainCamera.fieldOfView;

        _cameraTargetTexture.Init(Screen.width, Screen.height, RenderTextureFormat.ARGBHalf);
        _camera.targetTexture = _cameraTargetTexture;

        _commandBuffer = new CommandBuffer();
        _camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _commandBuffer);

        _blurMaterial = new Material(Shader.Find("BackGround/Blur"));
        _colorAdjustMaterial = new Material(Shader.Find("BackGround/ColorAdjust"));

        _renderer.material = new Material(Shader.Find("BackGround/UnlitTexture"));
        _renderer.material.SetTexture("_MainTex", _cameraTargetTexture);
    }

    public void Render()
    {
        SetupCamera();

        ApplyPostEffect();
    }

    public void SetupCamera()
    {
        _camera.transform.position = _mainCamera.transform.position;
        _camera.transform.rotation = _mainCamera.transform.rotation;

        if (_cameraTargetTexture.Width != Screen.width || _cameraTargetTexture.Height != Screen.height)
        {
            _cameraTargetTexture.Init(Screen.width, Screen.height, RenderTextureFormat.ARGBHalf);
            _camera.targetTexture = _cameraTargetTexture;
            _renderer.material.SetTexture("_MainTex", _cameraTargetTexture);
        }
    }

    private void ApplyPostEffect()
    {
        _commandBuffer.Clear();

        int2 sourceRes = new int2(_cameraTargetTexture.Width, _cameraTargetTexture.Height);

        int texID = 0;
        int tempRT;

        int sourceRT = Shader.PropertyToID("_SourceTex");
        _commandBuffer.GetTemporaryRT(sourceRT, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
        _commandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, sourceRT);

        // blur
        // down sampling
        int2 shrinkRes = sourceRes / _blurPreshrink;
        int blurRT = Shader.PropertyToID("_Texture" + texID++);
        _commandBuffer.GetTemporaryRT(blurRT, shrinkRes.x, shrinkRes.y, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
        _commandBuffer.Blit(sourceRT, blurRT, _blurMaterial, 0);

        // apply blur
        _blurMaterial.SetFloat("_SampleStep", _blurSampleStep);
        for (int i = 0; i < _blurIterations; i++)
        {
            // horizontal
            tempRT = Shader.PropertyToID("_Texture" + texID++);
            _commandBuffer.GetTemporaryRT(tempRT, shrinkRes.x, shrinkRes.y, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
            _blurMaterial.SetVector("_Direction", new Vector2(1f, 0f));
            _commandBuffer.Blit(blurRT, tempRT, _blurMaterial, 1);
            _commandBuffer.ReleaseTemporaryRT(blurRT);
            blurRT = tempRT;

            // vertical
            string texName = i < _blurIterations - 1 ? "_Texture" + texID++ : "_BlurTex";
            tempRT = Shader.PropertyToID(texName);
            _commandBuffer.GetTemporaryRT(tempRT, shrinkRes.x, shrinkRes.y, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
            _blurMaterial.SetVector("_Direction", new Vector2(0f, 1f));
            _commandBuffer.Blit(blurRT, tempRT, _blurMaterial, 1);
            _commandBuffer.ReleaseTemporaryRT(blurRT);
            blurRT = tempRT;
        }

        // color adjust
        _colorAdjustMaterial.SetFloat("_HueShift", _hueShift);
        _colorAdjustMaterial.SetFloat("_SaturationMultiplier", _saturationMultiplier);
        _colorAdjustMaterial.SetFloat("_ValueMultiplier", _valueMultiplier);
        _colorAdjustMaterial.SetFloat("_YiqShift", _hueShiftYiq);
        _colorAdjustMaterial.SetFloat("_BlackLevel", _blackLevel);
        _colorAdjustMaterial.SetFloat("_WhiteLevel", _whiteLevel);
        _colorAdjustMaterial.SetFloat("_Gamma", _gamma);
        _colorAdjustMaterial.SetFloat("_Contrast", _contrast);
        _colorAdjustMaterial.SetFloat("_Exposure", _exposure);

        _commandBuffer.Blit(blurRT, BuiltinRenderTextureType.CameraTarget, _colorAdjustMaterial, 0);

        _commandBuffer.ReleaseTemporaryRT(blurRT);
        _commandBuffer.ReleaseTemporaryRT(sourceRT);
    }

    public void OnDisable()
    {
        _camera.RemoveAllCommandBuffers();
    }
}