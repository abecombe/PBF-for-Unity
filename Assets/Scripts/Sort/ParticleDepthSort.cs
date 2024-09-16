using Abecombe.GPUBufferOperators;
using UnityEngine;

public class ParticleDepthSort : GPURadixSort
{
    protected override void LoadComputeShader()
    {
        RadixSortCs = Resources.Load<ComputeShader>("ParticleDepthSortCS");
    }
}