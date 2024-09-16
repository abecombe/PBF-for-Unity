using System;
using Abecombe.FPSUtil;
using Abecombe.GPUUtil;
using RosettaUI;
using Unity.Mathematics;
using UnityEngine;

public class FLIPSimulation : MonoBehaviour, IDisposable
{
    #region Structs & Enums
    private struct Particle
    {
        public uint ID;
        public float3 Position;
        public float3 Velocity;
    }

    private enum SimulationArea
    {
        Cube,
        Sphere
    }

    private enum KernelFunction
    {
        Linear,
        Quadratic
    }

    private enum AdvectionMethod
    {
        ForwardEuler,
        SecondOrderRungeKutta,
        ThirdOrderRungeKutta
    }

    private enum Quality
    {
        Low,
        Medium,
        High,
        Ultra
    }
    #endregion

    #region Properties
    private float DeltaTime => 1f / (_fpsSetter.TargetFPS * _frameSimulationIteration);

    private const int NumParticleInACell = 8;
    private int NumParticles => ParticleInitGridSize.x * ParticleInitGridSize.y * ParticleInitGridSize.z * NumParticleInACell;
    private int NumGrids => GridSize.x * GridSize.y * GridSize.z;
    private int NumPlusOneGrids => (GridSize.x + 1) * (GridSize.y + 1) * (GridSize.z + 1);

    [SerializeField] private SimulationArea _simulationArea = SimulationArea.Cube;

    // Quality
    [SerializeField] private Quality _quality = Quality.Medium;
    private readonly float[] _qualityToGridSpacing = { 0.5f, 0.4f, 0.3f, 0.2f };

    // Particle Params
    [SerializeField] private float3 _particleInitRangeMin;
    [SerializeField] private float3 _particleInitRangeMax;
    private int3 ParticleInitGridMin => math.clamp((int3)math.round((_particleInitRangeMin - GridMin) * GridInvSpacing - 0.5f), 0, GridSize - 1);
    private int3 ParticleInitGridMax => math.clamp((int3)math.round((_particleInitRangeMax - GridMin) * GridInvSpacing - 0.5f), ParticleInitGridMin, GridSize - 1);
    private int3 ParticleInitGridSize => ParticleInitGridMax - ParticleInitGridMin + 1;
    private float ParticleRadius => GridSpacing * 0.4f;

    // Grid Params
    private float3 TempSimulationSize => transform.localScale;
    private float3 SimulationSize => (float3)GridSize * GridSpacing;
    private float3 GridMin => -SimulationSize / 2f;
    private float3 GridMax => SimulationSize / 2f;
    private int3 GridSize => (int3)math.ceil(TempSimulationSize / GridSpacing);
    private float GridSpacing => _qualityToGridSpacing[(int)_quality];
    private float GridInvSpacing => 1f / GridSpacing;
    private uint _numNonSolidCells = 0;
    private int NonSolidCellsDispatchSize => _useNonSolidCellFiltering ? (int)_numNonSolidCells : NumGrids;

    // Particle Data Buffers
    private GPUDoubleBuffer<Particle> _particleBuffer = new();
    private GPUBuffer<float4> _particleRenderingBuffer = new(); // xyz: position, w: speed

    // Grid Data Buffers
    private GPUBuffer<uint2> _gridParticleIDBuffer = new();
    private GPUDoubleBuffer<uint> _gridTypeBuffer = new();
    private GPUBuffer<float3> _gridVelocityBuffer = new();
    private GPUBuffer<float3> _gridOriginalVelocityBuffer = new();
    private GPUBuffer<uint> _gridNonSolidCellIDBuffer = new();
    private GPUBuffer<float3> _gridFluidRatioBuffer = new();
    // viscous diffusion
    private GPUDoubleBuffer<float3> _gridDiffusionBuffer = new();
    private GPUDoubleBuffer<uint3> _gridAxisTypeBuffer = new();
    // pressure projection
    private GPUBuffer<float> _gridDivergenceBuffer = new();
    private GPUDoubleBuffer<float> _gridPressureBuffer = new();
    // density projection
    private GPUBuffer<float> _gridWeightBuffer = new();
    private GPUBuffer<uint> _gridUIntWeightBuffer = new();
    private GPUDoubleBuffer<float> _gridDensityPressureBuffer = new();
    private GPUBuffer<float3> _gridPositionModifyBuffer = new();
    private GPUBuffer<float> _gridFloatZeroBuffer = new();

    // Compute Shaders
    private GPUComputeShader _particleInitCs;
    private GPUComputeShader _cellTypeCs;
    private GPUComputeShader _particleToGridCs;
    private GPUComputeShader _externalForceCs;
    private GPUComputeShader _diffusionCs;
    private GPUComputeShader _pressureProjectionCs;
    private GPUComputeShader _gridToParticleCs;
    private GPUComputeShader _particleAdvectionCs;
    private GPUComputeShader _densityProjectionCs;
    private GPUComputeShader _renderingCs;

    // Grid Sort Helper
    private GridSortHelper<Particle> _gridSortHelper = new();

    // Non-Solid Cell ID Filtering
    [SerializeField] private bool _useNonSolidCellFiltering = true;
    private NonSolidCellFiltering _nonSolidCellFiltering = new();

    // Simulation Params
    [SerializeField] [Tooltip("0 is full PIC, 1 is full FLIP")] [Range(0f, 1f)] private float _flipness = 0.99f;
    [SerializeField] private Vector3 _gravity = Vector3.down * 9.8f;
    [SerializeField] [Range(0f, 10f)] private float _viscosity = 0f;
    [SerializeField] [Range(0f, 5f)] private float _mouseForce = 1.32f;
    [SerializeField] [Range(0f, 5f)] private float _mouseForceRange = 2.25f;
    [SerializeField] private KernelFunction _kernelFunction = KernelFunction.Linear;
    [SerializeField] private AdvectionMethod _advectionMethod = AdvectionMethod.ForwardEuler;
    [SerializeField] private bool _activeDensityProjection = true;
    [SerializeField] [Range(1, 30)] private uint _diffusionJacobiIteration = 15;
    [SerializeField] [Range(1, 120)] private uint _pressureProjectionJacobiIteration = 15;
    [SerializeField] [Range(1, 60)] private uint _densityProjectionJacobiIteration = 30;

    // RosettaUI
    private RosettaUIRoot _rosettaUIRoot;
    [SerializeField] private KeyCode _toggleUIKey = KeyCode.Tab;

    // Rendering
    private Material _particleInstanceMaterial;
    private MaterialPropertyBlock _mpb;
    private GPUBufferWithArgs _particleRenderingBufferWithArgs = new();
    [SerializeField] private Mesh _cubeMesh;
    [SerializeField] private Mesh _shapeMesh;
    // [SerializeField] private Material _simulationAreaCubeMaterial;
    // [SerializeField] private Material _simulationAreaSphereMaterial;
    private ScreenSpaceFluidRendering _screenSpaceFluidRendering;
    private ParticleRendering _particleRendering;

    // FPS Settings
    private int _frameSimulationIteration = 1;
    private FPSSetter _fpsSetter;
    private FPSCounter _fpsCounter;
    #endregion

    #region Initialize Functions
    private void InitFPS()
    {
        _fpsSetter = FindObjectOfType<FPSSetter>();
        if (_fpsSetter == null)
        {
            _fpsSetter = gameObject.AddComponent<FPSSetter>();
        }
        _fpsSetter.enabled = true;

        _fpsCounter = FindObjectOfType<FPSCounter>();
        if (_fpsCounter == null)
        {
            _fpsCounter = gameObject.AddComponent<FPSCounter>();
        }
        _fpsCounter.enabled = true;
    }

    private void InitComputeShaders()
    {
        _particleInitCs = new GPUComputeShader("ParticleInitCS");
        _cellTypeCs = new GPUComputeShader("CellTypeCS");
        _particleToGridCs = new GPUComputeShader("ParticleToGridCS");
        _externalForceCs = new GPUComputeShader("ExternalForceCS");
        _diffusionCs = new GPUComputeShader("DiffusionCS");
        _pressureProjectionCs = new GPUComputeShader("PressureProjectionCS");
        _gridToParticleCs = new GPUComputeShader("GridToParticleCS");
        _particleAdvectionCs = new GPUComputeShader("ParticleAdvectionCS");
        _densityProjectionCs = new GPUComputeShader("DensityProjectionCS");
        _renderingCs = new GPUComputeShader("RenderingCS");
    }

    private void InitSimulationArea()
    {
        switch (_simulationArea)
        {
            case SimulationArea.Cube:
                this.gameObject.transform.localScale = new Vector3(20f, 20f, 20f);
                _particleInitRangeMin = new float3(-9f, -5f, -8f);
                _particleInitRangeMax = new float3(1f, 8f, 8f);
                // GetComponent<MeshFilter>().mesh = _cubeMesh;
                // GetComponent<MeshRenderer>().sharedMaterial = _simulationAreaCubeMaterial;
                break;
            case SimulationArea.Sphere:
                this.gameObject.transform.localScale = new Vector3(24f, 24f, 24f);
                _particleInitRangeMin = new float3(-5f, -7f, -5f);
                _particleInitRangeMax = new float3(5f, 9f, 5f);
                // GetComponent<MeshFilter>().mesh = _shapeMesh;
                // GetComponent<MeshRenderer>().sharedMaterial = _simulationAreaSphereMaterial;
                break;
        }
    }

    private void InitParticleBuffers()
    {
        _particleBuffer.Init(NumParticles);
        _particleRenderingBuffer.Init(NumParticles);

        // init particle
        var cs = _particleInitCs;
        var k = cs.FindKernel("InitParticle");
        SetConstants(cs);
        cs.SetInts("_ParticleInitGridMin", ParticleInitGridMin);
        cs.SetInts("_ParticleInitGridMax", ParticleInitGridMax);
        cs.SetInts("_ParticleInitGridSize", ParticleInitGridSize);
        k.SetBuffer("_ParticleBufferWrite", _particleBuffer.Read);
        k.Dispatch(NumParticles);
    }

    private void InitGridBuffers()
    {
        _gridParticleIDBuffer.Init(NumGrids);
        _gridTypeBuffer.Init(NumGrids);
        _gridVelocityBuffer.Init(NumGrids);
        _gridOriginalVelocityBuffer.Init(NumGrids);
        _gridNonSolidCellIDBuffer.Init(NumGrids);
        _gridDiffusionBuffer.Init(NumGrids);
        _gridAxisTypeBuffer.Init(NumGrids);
        _gridDivergenceBuffer.Init(NumGrids);
        _gridPressureBuffer.Init(NumGrids);
        _gridWeightBuffer.Init(NumGrids);
        _gridUIntWeightBuffer.Init(NumGrids);
        _gridDensityPressureBuffer.Init(NumGrids);
        _gridPositionModifyBuffer.Init(NumGrids);
        _gridFloatZeroBuffer.Init(NumGrids);
        _gridFluidRatioBuffer.Init(NumGrids);
    }

    private void InitGPUBuffers()
    {
        InitParticleBuffers();
        InitGridBuffers();
    }
    #endregion

    #region Update Functions
    private void SetConstants(GPUComputeShader cs)
    {
        cs.SetFloat("_DeltaTime", DeltaTime);

        cs.SetVector("_GridMin", GridMin);
        cs.SetVector("_GridMax", GridMax);
        cs.SetInts("_GridSize", GridSize);
        cs.SetFloat("_GridSpacing", GridSpacing);
        cs.SetFloat("_GridInvSpacing", GridInvSpacing);

        switch (_kernelFunction)
        {
            case KernelFunction.Linear:
                cs.EnableKeyword("USE_LINEAR_KERNEL");
                cs.DisableKeyword("USE_QUADRATIC_KERNEL");
                break;
            case KernelFunction.Quadratic:
                cs.DisableKeyword("USE_LINEAR_KERNEL");
                cs.EnableKeyword("USE_QUADRATIC_KERNEL");
                break;
        }

        switch (_simulationArea)
        {
            case SimulationArea.Cube:
                cs.EnableKeyword("IS_CUBE_AREA_SIMULATION");
                cs.DisableKeyword("IS_SPHERE_AREA_SIMULATION");
                break;
            case SimulationArea.Sphere:
                cs.DisableKeyword("IS_CUBE_AREA_SIMULATION");
                cs.EnableKeyword("IS_SPHERE_AREA_SIMULATION");
                break;
        }

        switch (_useNonSolidCellFiltering)
        {
            case true:
                cs.EnableKeyword("USE_NON_SOLID_CELL_FILTERING");
                break;
            case false:
                cs.DisableKeyword("USE_NON_SOLID_CELL_FILTERING");
                break;
        }
    }

    // determining cell types
    private void DispatchCellType()
    {
        var cs = _cellTypeCs;

        SetConstants(cs);

        // set my cell type
        var k = cs.FindKernel("SetMyCellType");
        k.SetBuffer("_GridParticleIDBufferRead", _gridParticleIDBuffer);
        k.SetBuffer("_GridTypeBufferWrite", _gridTypeBuffer.Read);
        k.SetBuffer("_GridNonSolidCellIDBufferWrite", _gridNonSolidCellIDBuffer);
        k.Dispatch(NumGrids);

        if (_useNonSolidCellFiltering)
            _nonSolidCellFiltering.Filter(_gridNonSolidCellIDBuffer, out _numNonSolidCells);

        // set neighbor cell types
        k = cs.FindKernel("SetNeighborCellTypes");
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridTypeBufferWrite", _gridTypeBuffer.Write);
        k.Dispatch(NumGrids);
        _gridTypeBuffer.Swap();

        // set fluid ratio
        k = cs.FindKernel("SetFluidRatio");
        k.SetBuffer("_GridFluidRatioBufferWrite", _gridFluidRatioBuffer);
        k.Dispatch(NumGrids);
    }

    // transferring velocity from particle to grid
    private void DispatchParticleToGrid()
    {
        var cs = _particleToGridCs;

        SetConstants(cs);

        var k = cs.FindKernel("InitGridVelocity");
        k.SetBuffer("_GridVelocityBufferWrite", _gridVelocityBuffer);
        k.SetBuffer("_GridOriginalVelocityBufferWrite", _gridOriginalVelocityBuffer);
        k.Dispatch(NumGrids);

        k = cs.FindKernel("ParticleToGrid");
        k.SetBuffer("_ParticleBufferRead", _particleBuffer.Read);
        k.SetBuffer("_GridParticleIDBufferRead", _gridParticleIDBuffer);
        k.SetBuffer("_GridVelocityBufferWrite", _gridVelocityBuffer);
        k.SetBuffer("_GridOriginalVelocityBufferWrite", _gridOriginalVelocityBuffer);
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridNonSolidCellIDBufferRead", _gridNonSolidCellIDBuffer);
        k.Dispatch(NonSolidCellsDispatchSize);
    }

    // external force term with reference to https://github.com/dli/fluid
    private float2 _lastMousePlane = float2.zero;
    private void DispatchExternalForce(bool isFirstIteration)
    {
        var cs = _externalForceCs;
        var k = cs.FindKernel("AddExternalForce");

        SetConstants(cs);

        k.SetBuffer("_GridVelocityBufferRW", _gridVelocityBuffer);
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridNonSolidCellIDBufferRead", _gridNonSolidCellIDBuffer);

        cs.SetVector("_Gravity", _gravity);

        if (isFirstIteration)
        {
            var cam = Camera.main;
            var mouseRay = cam.ScreenPointToRay(Input.mousePosition);
            cs.SetVector("_RayOrigin", mouseRay.origin);
            cs.SetVector("_RayDirection", mouseRay.direction);

            var height = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f;
            var width = height * Screen.width / Screen.height;
            var mousePlane = ((float3)Input.mousePosition).xy / new float2(Screen.width, Screen.height) - 0.5f;
            mousePlane *= new float2(width, height);
            mousePlane *= cam.GetComponent<OrbitCamera>().Distance;
            var cameraViewMatrix = cam.worldToCameraMatrix;
            var cameraRight = new float3(cameraViewMatrix[0], cameraViewMatrix[4], cameraViewMatrix[8]);
            var cameraUp = new float3(cameraViewMatrix[1], cameraViewMatrix[5], cameraViewMatrix[9]);
            var mouseVelocity = (mousePlane - _lastMousePlane) / Time.smoothDeltaTime;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || Time.frameCount <= 1)
                mouseVelocity = float2.zero;
            _lastMousePlane = mousePlane;
            var mouseAxisVelocity = mouseVelocity.x * cameraRight + mouseVelocity.y * cameraUp;
            cs.SetVector("_MouseForceParameter", new float4(mouseAxisVelocity * _mouseForce, _mouseForceRange));
        }

        k.Dispatch(NonSolidCellsDispatchSize);
    }

    // viscous diffusion term
    private void DispatchDiffusion()
    {
        if (_viscosity <= 0f) return;

        var cs = _cellTypeCs;

        SetConstants(cs);

        // set my cell axis type
        var k = cs.FindKernel("SetMyCellAxisType");
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridAxisTypeBufferWrite", _gridAxisTypeBuffer.Read);
        k.Dispatch(NumGrids);

        // set neighbor cell axis types
        k = cs.FindKernel("SetNeighborCellAxisTypes");
        k.SetBuffer("_GridAxisTypeBufferRead", _gridAxisTypeBuffer.Read);
        k.SetBuffer("_GridAxisTypeBufferWrite", _gridAxisTypeBuffer.Write);
        k.Dispatch(NumGrids);
        _gridAxisTypeBuffer.Swap();

        cs = _diffusionCs;

        SetConstants(cs);

        // diffuse
        k = cs.FindKernel("Diffuse");
        float temp1 = _viscosity * DeltaTime;
        float3 temp2 = 1f / GridSpacing / GridSpacing;
        float temp3 = 1f / (1f + 2f * (temp2.x + temp2.y + temp2.z) * temp1);
        float4 diffusionParameter = new(temp1 * temp2 * temp3, temp3);
        cs.SetVector("_DiffusionParameter", diffusionParameter);
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridAxisTypeBufferRead", _gridAxisTypeBuffer.Read);
        k.SetBuffer("_GridVelocityBufferRead", _gridVelocityBuffer);
        k.SetBuffer("_GridNonSolidCellIDBufferRead", _gridNonSolidCellIDBuffer);
        for (uint i = 0; i < _diffusionJacobiIteration; i++)
        {
            k.SetBuffer("_GridDiffusionBufferRead", i == 0 ? _gridVelocityBuffer : _gridDiffusionBuffer.Read);
            k.SetBuffer("_GridDiffusionBufferWrite", _gridDiffusionBuffer.Write);
            k.Dispatch(NonSolidCellsDispatchSize);
            _gridDiffusionBuffer.Swap();
        }

        // update velocity
        k = cs.FindKernel("UpdateVelocity");
        k.SetBuffer("_GridVelocityBufferWrite", _gridVelocityBuffer);
        k.SetBuffer("_GridDiffusionBufferRead", _gridDiffusionBuffer.Read);
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridNonSolidCellIDBufferRead", _gridNonSolidCellIDBuffer);
        k.Dispatch(NonSolidCellsDispatchSize);
    }

    // pressure projection term
    private void DispatchPressureProjection()
    {
        var cs = _pressureProjectionCs;

        SetConstants(cs);

        // calc divergence
        var k = cs.FindKernel("CalcDivergence");
        float3 divergenceParameter = 1f / GridSpacing;
        cs.SetVector("_DivergenceParameter", divergenceParameter);
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridVelocityBufferRead", _gridVelocityBuffer);
        k.SetBuffer("_GridDivergenceBufferWrite", _gridDivergenceBuffer);
        k.SetBuffer("_GridNonSolidCellIDBufferRead", _gridNonSolidCellIDBuffer);
        k.SetBuffer("_GridFluidRatioBufferRead", _gridFluidRatioBuffer);
        k.Dispatch(NonSolidCellsDispatchSize);

        // project
        k = cs.FindKernel("Project");
        float3 temp1 = 1f / GridSpacing / GridSpacing;
        float temp2 = 1f / (2f * (temp1.x + temp1.y + temp1.z));
        float4 projectionParameter1 = new((float3)temp2 / GridSpacing / GridSpacing, -temp2);
        cs.SetVector("_PressureProjectionParameter1", projectionParameter1);
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridDivergenceBufferRead", _gridDivergenceBuffer);
        k.SetBuffer("_GridNonSolidCellIDBufferRead", _gridNonSolidCellIDBuffer);
        k.SetBuffer("_GridFluidRatioBufferRead", _gridFluidRatioBuffer);
        for (uint i = 0; i < _pressureProjectionJacobiIteration; i++)
        {
            k.SetBuffer("_GridPressureBufferRead", _gridPressureBuffer.Read);
            k.SetBuffer("_GridPressureBufferWrite", _gridPressureBuffer.Write);
            k.Dispatch(NonSolidCellsDispatchSize);
            _gridPressureBuffer.Swap();
        }

        // update velocity
        k = cs.FindKernel("UpdateVelocity");
        float3 projectionParameter2 = 1f / GridSpacing;
        cs.SetVector("_PressureProjectionParameter2", projectionParameter2);
        k.SetBuffer("_GridVelocityBufferRW", _gridVelocityBuffer);
        k.SetBuffer("_GridPressureBufferRead", _gridPressureBuffer.Read);
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridNonSolidCellIDBufferRead", _gridNonSolidCellIDBuffer);
        k.SetBuffer("_GridFluidRatioBufferRead", _gridFluidRatioBuffer);
        k.Dispatch(NonSolidCellsDispatchSize);
    }

    // transferring velocity from grid to particle
    private void DispatchGridToParticle()
    {
        var cs = _gridToParticleCs;
        var k = cs.FindKernel("GridToParticle");

        SetConstants(cs);

        cs.SetFloat("_Flipness", math.saturate(_flipness));
        k.SetBuffer("_ParticleBufferRW", _particleBuffer.Read);
        k.SetBuffer("_GridVelocityBufferRead", _gridVelocityBuffer);
        k.SetBuffer("_GridOriginalVelocityBufferRead", _gridOriginalVelocityBuffer);

        k.Dispatch(NumParticles);
    }

    // particle advection term
    private void DispatchAdvection()
    {
        var cs = _particleAdvectionCs;
        var k = cs.FindKernel("Advect");

        SetConstants(cs);

        k.SetBuffer("_ParticleBufferRW", _particleBuffer.Read);
        k.SetBuffer("_GridVelocityBufferRead", _gridVelocityBuffer);
        switch (_advectionMethod)
        {
            case AdvectionMethod.ForwardEuler:
                cs.EnableKeyword("USE_RK1");
                cs.DisableKeyword("USE_RK2");
                cs.DisableKeyword("USE_RK3");
                break;
            case AdvectionMethod.SecondOrderRungeKutta:
                cs.DisableKeyword("USE_RK1");
                cs.EnableKeyword("USE_RK2");
                cs.DisableKeyword("USE_RK3");
                break;
            case AdvectionMethod.ThirdOrderRungeKutta:
                cs.DisableKeyword("USE_RK1");
                cs.DisableKeyword("USE_RK2");
                cs.EnableKeyword("USE_RK3");
                break;
        }

        k.Dispatch(NumParticles);
    }

    // density projection term with reference to
    // https://animation.rwth-aachen.de/media/papers/66/2019-TVCG-ImplicitDensityProjection.pdf
    private void DispatchDensityProjection()
    {
        var cs = _densityProjectionCs;

        SetConstants(cs);
        cs.SetFloat("_InvAverageWeight", 1f / NumParticleInACell);

        // init buffers
        var k = cs.FindKernel("InitBuffer");
        k.SetBuffer("_GridTypeBufferRW", _gridTypeBuffer.Read);
        k.SetBuffer("_GridUIntWeightBufferWrite", _gridUIntWeightBuffer);
        k.SetBuffer("_GridPositionModifyBufferWrite", _gridPositionModifyBuffer);
        k.Dispatch(NumGrids);

        // interlocked add weight
        k = cs.FindKernel("InterlockedAddWeight");
        k.SetBuffer("_ParticleBufferRead", _particleBuffer.Read);
        k.SetBuffer("_GridTypeBufferRW", _gridTypeBuffer.Read);
        k.SetBuffer("_GridUIntWeightBufferWrite", _gridUIntWeightBuffer);
        k.Dispatch(NumParticles);

        // set neighbor cell types
        k = cs.FindKernel("SetNeighborCellTypes");
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridTypeBufferWrite", _gridTypeBuffer.Write);
        k.Dispatch(NumGrids);
        _gridTypeBuffer.Swap();

        // calc grid weight
        k = cs.FindKernel("CalcGridWeight");
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridUIntWeightBufferRead", _gridUIntWeightBuffer);
        k.SetBuffer("_GridWeightBufferWrite", _gridWeightBuffer);
        k.SetBuffer("_GridNonSolidCellIDBufferRead", _gridNonSolidCellIDBuffer);
        cs.SetVector("_GhostWeight", new Vector3(36f / 64f, 6f / 64f, 1f / 64f));
        k.Dispatch(NonSolidCellsDispatchSize);

        // project
        k = cs.FindKernel("Project");
        float3 temp1 = 1f / GridSpacing / GridSpacing;
        float temp2 = 1f / (2f * (temp1.x + temp1.y + temp1.z));
        float4 projectionParameter1 = new((float3)temp2 / GridSpacing / GridSpacing, -temp2);
        cs.SetVector("_DensityProjectionParameter1", projectionParameter1);
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridWeightBufferRead", _gridWeightBuffer);
        k.SetBuffer("_GridNonSolidCellIDBufferRead", _gridNonSolidCellIDBuffer);
        for (uint i = 0; i < _densityProjectionJacobiIteration; i++)
        {
            k.SetBuffer("_GridDensityPressureBufferRead", i == 0 ? _gridFloatZeroBuffer : _gridDensityPressureBuffer.Read);
            k.SetBuffer("_GridDensityPressureBufferWrite", _gridDensityPressureBuffer.Write);
            k.Dispatch(NonSolidCellsDispatchSize);
            _gridDensityPressureBuffer.Swap();
        }

        // calc grid delta position
        k = cs.FindKernel("CalcPositionModify");
        float3 projectionParameter2 = 1f / GridSpacing;
        cs.SetVector("_DensityProjectionParameter2", projectionParameter2);
        k.SetBuffer("_GridTypeBufferRead", _gridTypeBuffer.Read);
        k.SetBuffer("_GridDensityPressureBufferRead", _gridDensityPressureBuffer.Read);
        k.SetBuffer("_GridPositionModifyBufferWrite", _gridPositionModifyBuffer);
        k.SetBuffer("_GridNonSolidCellIDBufferRead", _gridNonSolidCellIDBuffer);
        k.Dispatch(NonSolidCellsDispatchSize);

        // update particle position
        k = cs.FindKernel("UpdatePosition");
        k.SetBuffer("_ParticleBufferRW", _particleBuffer.Read);
        k.SetBuffer("_GridPositionModifyBufferRead", _gridPositionModifyBuffer);
        k.Dispatch(NumParticles);
    }

    private void Render()
    {
        var cs = _renderingCs;
        var k = cs.FindKernel("PrepareRendering");

        k.SetBuffer("_ParticleBufferRead", _particleBuffer.Read);
        k.SetBuffer("_ParticleRenderingBufferWrite", _particleRenderingBuffer);

        k.Dispatch(NumParticles);

        ParticleRendering.Instance?.Render(_particleRenderingBuffer, ParticleRadius);
    }
    #endregion

    #region Release Buffers
    public void Dispose()
    {
        _particleBuffer.Dispose();
        _particleRenderingBuffer.Dispose();

        _gridParticleIDBuffer.Dispose();
        _gridTypeBuffer.Dispose();
        _gridVelocityBuffer.Dispose();
        _gridOriginalVelocityBuffer.Dispose();
        _gridNonSolidCellIDBuffer.Dispose();
        _gridDiffusionBuffer.Dispose();
        _gridAxisTypeBuffer.Dispose();
        _gridDivergenceBuffer.Dispose();
        _gridPressureBuffer.Dispose();
        _gridWeightBuffer.Dispose();
        _gridUIntWeightBuffer.Dispose();
        _gridDensityPressureBuffer.Dispose();
        _gridPositionModifyBuffer.Dispose();
        _gridFloatZeroBuffer.Dispose();
        _gridFluidRatioBuffer.Dispose();

        _gridSortHelper.Dispose();
        _nonSolidCellFiltering.Dispose();

        _particleRenderingBufferWithArgs.Dispose();
    }
    #endregion

    #region RosettaUI
    private void InitRosettaUI()
    {
        var window = UI.Window(
            $"Settings ( press {_toggleUIKey.ToString()} to open / close )",
            UI.Indent(UI.Box(UI.Indent(
                UI.Space().SetHeight(5f),
                UI.Field("Simulation Area", () => _simulationArea)
                    .RegisterValueChangeCallback(() =>
                    {
                        InitSimulationArea();
                        InitGPUBuffers();
                    }),
                UI.Space().SetHeight(10f),
                UI.Field("Quality", () => _quality)
                    .RegisterValueChangeCallback(InitGPUBuffers),
                UI.Indent(
                    UI.FieldReadOnly("Num Particles", () => NumParticles),
                    UI.FieldReadOnly("Num Grids", () => NumGrids)
                ),
                UI.Space().SetHeight(10f),
                UI.Label("Non Solid Cell Filtering"),
                UI.Indent(
                    UI.Field("Active", () => _useNonSolidCellFiltering),
                    UI.FieldReadOnly("Num Non Solid Cells", () => _useNonSolidCellFiltering ? _numNonSolidCells : 0)
                ),
                UI.Space().SetHeight(10f),
                UI.Label("Parameter"),
                UI.Indent(
                    UI.Slider("Flipness", () => _flipness),
                    UI.Field("Gravity", () => _gravity),
                    UI.Slider("Viscosity", () => _viscosity)
                ),
                UI.Space().SetHeight(10f),
                UI.Label("Numerical Method"),
                UI.Indent(
                    UI.Field("Kernel Function", () => _kernelFunction),
                    UI.Field("Advection Method", () => _advectionMethod),
                    UI.Field("Density Projection", () => _activeDensityProjection)
                ),
                UI.Space().SetHeight(10f),
                UI.Label("Interaction"),
                UI.Indent(
                    UI.Slider("Mouse Force", () => _mouseForce),
                    UI.Slider("Mouse Force Range", () => _mouseForceRange)
                ),
                UI.Space().SetHeight(10f),
                UI.Label("Jacobi Iteration"),
                UI.Indent(
                    UI.Slider("Diffusion", () => _diffusionJacobiIteration),
                    UI.Slider("Pressure Projection", () => _pressureProjectionJacobiIteration),
                    UI.Slider("Density Projection", () => _densityProjectionJacobiIteration)
                ),
                UI.Space().SetHeight(10f),
                UI.Label("FPS Setting"),
                UI.Indent(
                    UI.Field("Frame Sim Iteration", () => _frameSimulationIteration, value => _frameSimulationIteration = math.clamp(value, 1, 10)),
                    UI.Slider("Target FPS", () => _fpsSetter.TargetFPS, 15, 120),
                    UI.Field("Show FPS", () => _fpsCounter.ShowFPS)
                ),
                UI.Space().SetHeight(10f),
                UI.Button("Restart", InitGPUBuffers),
                UI.Space().SetHeight(5f)
            )))
        );
        window.Closable = false;

        _rosettaUIRoot = FindObjectOfType<RosettaUIRoot>();
        _rosettaUIRoot.Build(window);
    }

    private void UpdateRosettaUI()
    {
        if (Input.GetKeyDown(_toggleUIKey))
            _rosettaUIRoot.enabled = !_rosettaUIRoot.enabled;
    }
    #endregion

    #region MonoBehaviour
    private void OnEnable()
    {
        InitFPS();

        InitSimulationArea();
        InitComputeShaders();
        InitGPUBuffers();
    }

    private void Start()
    {
        InitRosettaUI();
    }

    private void Update()
    {
        for (int i = 0; i < _frameSimulationIteration; i++)
        {
            _gridSortHelper.Sort(_particleBuffer, _gridParticleIDBuffer, GridMin, GridMax, GridSize, GridSpacing);
            DispatchCellType();
            DispatchParticleToGrid();
            DispatchExternalForce(i == 0);
            DispatchDiffusion();
            DispatchPressureProjection();
            DispatchGridToParticle();
            DispatchAdvection();
            if (_activeDensityProjection) DispatchDensityProjection();
        }

        Render();

        UpdateRosettaUI();
    }

    private void OnDisable()
    {
        Dispose();
    }
    #endregion
}