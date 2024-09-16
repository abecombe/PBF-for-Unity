using Abecombe.GPUUtil;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class ScreenSpaceFluidRendering : MonoBehaviour
{
    [SerializeField] private float _particleRadius = 0.1f;

    [SerializeField] private int _resolutionWidth = 1000;

    [SerializeField] private int _narrowRangeFilter1DIterations = 3;
    [SerializeField] private int _narrowRangeFilter2DIterations = 1;
    [SerializeField] private int _narrowRangeFilter1DFilterRadius = 15;
    [SerializeField] private int _narrowRangeFilter2DFilterRadius = 5;
    [SerializeField] private float _narrowRangeFilterThresholdRatio = 8;
    [SerializeField] private float _narrowRangeFilterClampRatio = 1;
    [SerializeField] private int _blurPreshrink = 4;
    [SerializeField] private int _blurIterations = 1;
    [SerializeField] private int _blurSampleStep = 1;

    [SerializeField] private float _roughness = 0.01f;
    [SerializeField] private float _metallic = 0.3f;
    [SerializeField] private Color _albedo = Color.blue;

    [SerializeField] private CustomAnimationCurve _alphaLookupCurve = new();
    [SerializeField] private Vector2 _alphaLookupCurveRange = new(0, 1);

    [SerializeField] private Mesh _quadMesh;

    private Camera _camera;
    private Camera _mainCamera;

    private GPUTexture2D _cameraTargetTexture = new();

    private LayerMask _layerMask;
    private Material _particleInstanceMaterial;
    private MaterialPropertyBlock _mpb;
    private GPUBufferWithArgs _particleRenderingBufferWithArgs = new();

    private Material _narrowRangeFilterMaterial;
    private Material _blurMaterial;
    private Material _preparePbrMaterial;

    private CommandBuffer _commandBuffer;

    private Renderer _pbrRenderer;
    private Material _pbrMaterial;

    private void OnEnable()
    {
        _camera = gameObject.GetComponent<Camera>();
        _camera.cullingMask = 1 << gameObject.layer;

        _mainCamera = Camera.main;

        _camera.fieldOfView = _mainCamera.fieldOfView;

        _cameraTargetTexture.Init(_resolutionWidth, (int)((float)Screen.height / Screen.width * _resolutionWidth));
        _camera.targetTexture = _cameraTargetTexture;

        _layerMask = gameObject.layer;
        _particleInstanceMaterial = new Material(Shader.Find("ScreenSpaceFluidRendering/ParticleInstance"));
        _mpb = new MaterialPropertyBlock();

        _commandBuffer = new CommandBuffer();
        _camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _commandBuffer);

        _narrowRangeFilterMaterial = new Material(Shader.Find("ScreenSpaceFluidRendering/NarrowRangeFilter"));
        _blurMaterial = new Material(Shader.Find("ScreenSpaceFluidRendering/Blur"));
        _preparePbrMaterial = new Material(Shader.Find("ScreenSpaceFluidRendering/PreparePBR"));

        _pbrRenderer = GetComponentInChildren<Renderer>();
        _pbrRenderer.enabled = true;
        _pbrRenderer.material = new Material(Shader.Find("ScreenSpaceFluidRendering/PBR"));
        _pbrMaterial = _pbrRenderer.material;
    }

    public void Render(GPUBuffer<float4> particleRenderingBuffer)
    {
        SetupCamera();

        RenderParticles(particleRenderingBuffer);

        ApplyPostEffect();

        PbrRender();
    }

    public void SetupCamera()
    {
        _camera.transform.position = _mainCamera.transform.position;
        _camera.transform.rotation = _mainCamera.transform.rotation;

        if (_cameraTargetTexture.Width != _resolutionWidth || _cameraTargetTexture.Height != (int)((float)Screen.height / Screen.width * _resolutionWidth))
        {
            _cameraTargetTexture.Init(_resolutionWidth, (int)((float)Screen.height / Screen.width * _resolutionWidth));
            _camera.targetTexture = _cameraTargetTexture;
            _pbrRenderer.material.SetTexture("_MainTex", _cameraTargetTexture);
        }
    }

    private void RenderParticles(GPUBuffer<float4> particleRenderingBuffer)
    {
        _particleRenderingBufferWithArgs.CheckArgsChanged(_quadMesh.GetIndexCount(0), (uint)particleRenderingBuffer.Size);

        _mpb.SetBuffer("_ParticleRenderingBuffer", particleRenderingBuffer);
        _mpb.SetFloat("_Radius", _particleRadius);
        _mpb.SetFloat("_NearClipPlane", _camera.nearClipPlane);
        _mpb.SetFloat("_FarClipPlane", _camera.farClipPlane);

        CustomGraphics.DrawMeshInstancedIndirect(_quadMesh, _particleInstanceMaterial, _mpb, _particleRenderingBufferWithArgs, _layerMask);
    }

    private void ApplyPostEffect()
    {
        _commandBuffer.Clear();

        int2 sourceRes = new int2(_cameraTargetTexture.Width, _cameraTargetTexture.Height);

        int texID = 0;
        int tempRT;

        int sourceRT = Shader.PropertyToID("_SourceTex");
        _commandBuffer.GetTemporaryRT(sourceRT, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
        _commandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, sourceRT);

        // narrow range filter
        int filterRT = Shader.PropertyToID("_Texture" + texID++);
        _commandBuffer.GetTemporaryRT(filterRT, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.RFloat);
        _commandBuffer.Blit(sourceRT, filterRT);

        _narrowRangeFilterMaterial.SetFloat("_ParticleRadius", _particleRadius);
        _narrowRangeFilterMaterial.SetFloat("_NearClipPlane", _camera.nearClipPlane);
        _narrowRangeFilterMaterial.SetFloat("_FarClipPlane", _camera.farClipPlane);
        _narrowRangeFilterMaterial.SetFloat("_ThresholdRatio", _narrowRangeFilterThresholdRatio);
        _narrowRangeFilterMaterial.SetFloat("_ClampRatio", _narrowRangeFilterClampRatio);

        // 1D filter
        _narrowRangeFilterMaterial.SetFloat("_FilterRadius", _narrowRangeFilter1DFilterRadius);
        for (int i = 0; i < _narrowRangeFilter1DIterations; i++)
        {
            // horizontal
            _narrowRangeFilterMaterial.SetVector("_Direction", new Vector2(1f, 0f));
            tempRT = Shader.PropertyToID("_Texture" + texID++);
            _commandBuffer.GetTemporaryRT(tempRT, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.RFloat);
            _commandBuffer.Blit(filterRT, tempRT, _narrowRangeFilterMaterial, 0);
            _commandBuffer.ReleaseTemporaryRT(filterRT);
            filterRT = tempRT;

            // vertical
            _narrowRangeFilterMaterial.SetVector("_Direction", new Vector2(0f, 1f));
            tempRT = Shader.PropertyToID("_Texture" + texID++);
            _commandBuffer.GetTemporaryRT(tempRT, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.RFloat);
            _commandBuffer.Blit(filterRT, tempRT, _narrowRangeFilterMaterial, 0);
            _commandBuffer.ReleaseTemporaryRT(filterRT);
            filterRT = tempRT;
        }

        // 2D filter
        _narrowRangeFilterMaterial.SetFloat("_FilterRadius", _narrowRangeFilter2DFilterRadius);
        for (int i = 0; i < _narrowRangeFilter2DIterations; i++)
        {
            string texName = i < _narrowRangeFilter2DIterations - 1 ? "_Texture" + texID++ : "_FilterTex";
            tempRT = Shader.PropertyToID(texName);
            _commandBuffer.GetTemporaryRT(tempRT, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.RFloat);
            _commandBuffer.Blit(filterRT, tempRT, _narrowRangeFilterMaterial, 1);
            _commandBuffer.ReleaseTemporaryRT(filterRT);
            filterRT = tempRT;
        }

        // blur for alpha
        // lookup alpha
        _alphaLookupCurve.Tick();
        _blurMaterial.SetTexture("_AlphaLookupTex", _alphaLookupCurve.CurveTex());
        _blurMaterial.SetVector("_AlphaLookupRange", _alphaLookupCurveRange);
        tempRT = Shader.PropertyToID("_Texture" + texID++);
        _commandBuffer.GetTemporaryRT(tempRT, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.RHalf);
        _commandBuffer.Blit(sourceRT, tempRT, _blurMaterial, 0);

        // down sampling
        int2 shrinkRes = sourceRes / _blurPreshrink;
        int blurRT = Shader.PropertyToID("_Texture" + texID++);
        _commandBuffer.GetTemporaryRT(blurRT, shrinkRes.x, shrinkRes.y, 0, FilterMode.Bilinear, RenderTextureFormat.RHalf);
        _commandBuffer.Blit(tempRT, blurRT, _blurMaterial, 1);
        _commandBuffer.ReleaseTemporaryRT(tempRT);

        // apply blur
        _blurMaterial.SetFloat("_SampleStep", _blurSampleStep);
        for (int i = 0; i < _blurIterations; i++)
        {
            // horizontal
            tempRT = Shader.PropertyToID("_Texture" + texID++);
            _commandBuffer.GetTemporaryRT(tempRT, shrinkRes.x, shrinkRes.y, 0, FilterMode.Bilinear, RenderTextureFormat.RHalf);
            _blurMaterial.SetVector("_Direction", new Vector2(1f, 0f));
            _commandBuffer.Blit(blurRT, tempRT, _blurMaterial, 2);
            _commandBuffer.ReleaseTemporaryRT(blurRT);
            blurRT = tempRT;

            // vertical
            string texName = i < _blurIterations - 1 ? "_Texture" + texID++ : "_BlurTex";
            tempRT = Shader.PropertyToID(texName);
            _commandBuffer.GetTemporaryRT(tempRT, shrinkRes.x, shrinkRes.y, 0, FilterMode.Bilinear, RenderTextureFormat.RHalf);
            _blurMaterial.SetVector("_Direction", new Vector2(0f, 1f));
            _commandBuffer.Blit(blurRT, tempRT, _blurMaterial, 2);
            _commandBuffer.ReleaseTemporaryRT(blurRT);
            blurRT = tempRT;
        }

        // prepare for PBR
        _commandBuffer.Blit(sourceRT, BuiltinRenderTextureType.CameraTarget, _preparePbrMaterial, 0);

        _commandBuffer.ReleaseTemporaryRT(filterRT);
        _commandBuffer.ReleaseTemporaryRT(blurRT);
        _commandBuffer.ReleaseTemporaryRT(sourceRT);
    }

    private void PbrRender()
    {
        _pbrMaterial.SetTexture("_MainTex", _cameraTargetTexture);
        _pbrMaterial.SetFloat("_NearClipPlane", _camera.nearClipPlane);
        _pbrMaterial.SetFloat("_FarClipPlane", _camera.farClipPlane);
        _pbrMaterial.SetFloat("_Roughness", _roughness);
        _pbrMaterial.SetFloat("_Metallic", _metallic);
        _pbrMaterial.SetVector("_Albedo", _albedo);
        float tanFov = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        _pbrMaterial.SetVector("_ClipToViewConst", new Vector2(tanFov, tanFov / _camera.aspect));
    }

    public void OnDisable()
    {
        _particleRenderingBufferWithArgs.Dispose();
        _camera.RemoveAllCommandBuffers();
        _alphaLookupCurve.Dispose();
    }
}