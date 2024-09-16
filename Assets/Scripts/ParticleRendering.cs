using Abecombe.GPUUtil;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class ParticleRendering : SingletonMonoBehaviour<ParticleRendering>
{
    [SerializeField] private int _resolutionWidth = 1000;
    [SerializeField] private bool _useRealResolution = false;
    [SerializeField] private int _ambientOcclusionSampleCount = 64;
    [SerializeField] private float _ambientOcclusionSampleLength = 1f;
    [SerializeField] private Color _ambientOcclusionColor = Color.black;
    [SerializeField] private float _ambientOcclusionMaxRate = 1f;
    [SerializeField] private int _ambientOcclusionSeed = 0;

    [SerializeField] private float _roughness = 0.01f;
    [SerializeField] private float _metallic = 0.3f;
    [SerializeField] private Color _albedo = Color.blue;

    [SerializeField] private Mesh _quadMesh;

    private int ResolutionWidth => _useRealResolution ? Screen.width :_resolutionWidth;
    private int ResolutionHeight => _useRealResolution ? Screen.height : _resolutionWidth * Screen.height / Screen.width;

    private Camera _camera;
    private Camera _mainCamera;

    private GPUTexture2D _cameraTargetTexture = new();

    private LayerMask _layerMask;
    private Material _particleInstanceMaterial;
    private MaterialPropertyBlock _mpb;
    private GPUBufferWithArgs _particleRenderingBufferWithArgs = new();

    private Material _ambientOcclusionMaterial;
    private Material _preparePbrMaterial;

    private GPUBuffer<float3> _samplingPointsBuffer = new();

    private CommandBuffer _commandBuffer;

    private Renderer _quadRenderer;
    private Material _quadMaterial;

    private void OnEnable()
    {
        _camera = gameObject.GetComponent<Camera>();
        _camera.cullingMask = 1 << gameObject.layer;

        _mainCamera = Camera.main;

        _cameraTargetTexture.Init(ResolutionWidth, ResolutionHeight, RenderTextureFormat.ARGBFloat);
        _camera.targetTexture = _cameraTargetTexture;

        _layerMask = gameObject.layer;
        _particleInstanceMaterial = new Material(Shader.Find("ParticleRendering/ParticleInstance"));
        _mpb = new MaterialPropertyBlock();

        _commandBuffer = new CommandBuffer();
        _camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _commandBuffer);

        _ambientOcclusionMaterial = new Material(Shader.Find("ParticleRendering/AmbientOcclusion"));
        _preparePbrMaterial = new Material(Shader.Find("ParticleRendering/PreparePBR"));
        SetAmbientOcclusionSamplingPointsBuffer();

        _quadRenderer = transform.parent.GetComponentInChildren<Renderer>();
        _quadRenderer.enabled = true;
        _quadRenderer.material = new Material(Shader.Find("ParticleRendering/QuadRenderer"));
        _quadMaterial = _quadRenderer.sharedMaterial;
        _quadMaterial.SetTexture("_MainTex", _cameraTargetTexture);
    }

    public void Render(GPUBuffer<float4> particleRenderingBuffer, float particleRadius)
    {
        SetupCamera();

        RenderParticles(particleRenderingBuffer, particleRadius);

        ApplyPostEffect();

        PbrRender();
    }

    public void SetupCamera()
    {
        _camera.transform.position = _mainCamera.transform.position;
        _camera.transform.rotation = _mainCamera.transform.rotation;
        _camera.fieldOfView = _mainCamera.fieldOfView;
        _camera.nearClipPlane = _mainCamera.nearClipPlane;
        _camera.farClipPlane = _mainCamera.farClipPlane;

        if (_cameraTargetTexture.Width == ResolutionWidth && _cameraTargetTexture.Height == ResolutionHeight) return;

        _cameraTargetTexture.Init(ResolutionWidth, ResolutionHeight, RenderTextureFormat.ARGBFloat);
        _camera.targetTexture = _cameraTargetTexture;
        _quadMaterial.SetTexture("_MainTex", _cameraTargetTexture);
    }

    private void RenderParticles(GPUBuffer<float4> particleRenderingBuffer, float particleRadius)
    {
        _particleRenderingBufferWithArgs.CheckArgsChanged((uint)particleRenderingBuffer.Size, 1);

        _mpb.SetBuffer("_ParticleRenderingBuffer", particleRenderingBuffer);
        _mpb.SetFloat("_Radius", particleRadius);
        _mpb.SetFloat("_NearClipPlane", _camera.nearClipPlane);
        _mpb.SetFloat("_FarClipPlane", _camera.farClipPlane);
        _mpb.SetVector("_SlowColor", new Color(0f, 0.3891521f, 0.7735849f, 1f));
        _mpb.SetVector("_FastColor", new Color(0.5999911f, 0.7552593f, 0.9150943f, 1f));
        _mpb.SetVector("_VelocityRange", new Vector2(2f, 8f));
        _mpb.SetFloat("_FresnelPower", 0.4f);

        CustomGraphics.DrawProceduralIndirect(_particleInstanceMaterial, _mpb, _particleRenderingBufferWithArgs, _layerMask);
    }

    private void SetAmbientOcclusionSamplingPointsBuffer()
    {
        if (_samplingPointsBuffer.Data != null && _samplingPointsBuffer.Size == _ambientOcclusionSampleCount) return;

        _samplingPointsBuffer.Init(_ambientOcclusionSampleCount);
        var samplingPoints = new float3[_ambientOcclusionSampleCount];
        for (int i = 0; i < _ambientOcclusionSampleCount; i++)
        {
            samplingPoints[i] = Hash.RandomInUnitSphere(new uint2((uint)i, (uint)_ambientOcclusionSeed));
            samplingPoints[i].z = math.abs(samplingPoints[i].z);
        }
        _samplingPointsBuffer.SetData(samplingPoints);
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

        // Ambient Occlusion
        int aoRT = Shader.PropertyToID("_AOTex");
        _commandBuffer.GetTemporaryRT(aoRT, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
        _ambientOcclusionMaterial.SetFloat("_NearClipPlane", _camera.nearClipPlane);
        _ambientOcclusionMaterial.SetFloat("_FarClipPlane", _camera.farClipPlane);
        float tanFov = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        _ambientOcclusionMaterial.SetVector("_ClipToViewConst", new Vector2(tanFov, tanFov / _camera.aspect));
        _ambientOcclusionMaterial.SetInt("_SampleCount", _ambientOcclusionSampleCount);
        SetAmbientOcclusionSamplingPointsBuffer();
        _ambientOcclusionMaterial.SetBuffer("_SamplingPointsBuffer", _samplingPointsBuffer);
        _ambientOcclusionMaterial.SetFloat("_AmbientOcclusionSampleLength", _ambientOcclusionSampleLength);
        _ambientOcclusionMaterial.SetFloat("_AmbientOcclusionMaxRate", _ambientOcclusionMaxRate);
        _commandBuffer.Blit(sourceRT, aoRT, _ambientOcclusionMaterial, 0);

        // prepare for PBR
        _commandBuffer.Blit(sourceRT, BuiltinRenderTextureType.CameraTarget);

        _commandBuffer.ReleaseTemporaryRT(aoRT);
        _commandBuffer.ReleaseTemporaryRT(sourceRT);
    }

    private void PbrRender()
    {
        _quadMaterial.SetTexture("_MainTex", _cameraTargetTexture);
        _quadMaterial.SetFloat("_NearClipPlane", _camera.nearClipPlane);
        _quadMaterial.SetFloat("_FarClipPlane", _camera.farClipPlane);
        _quadMaterial.SetFloat("_Roughness", _roughness);
        _quadMaterial.SetFloat("_Metallic", _metallic);
        _quadMaterial.SetVector("_Albedo", _albedo);
        _quadMaterial.SetVector("_AmbientOcclusionColor", _ambientOcclusionColor);
        float tanFov = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        _quadMaterial.SetVector("_ClipToViewConst", new Vector2(tanFov, tanFov / _camera.aspect));
    }

    public void OnDisable()
    {
        _particleRenderingBufferWithArgs.Dispose();
        _samplingPointsBuffer.Dispose();
        _camera.RemoveAllCommandBuffers();
    }
}