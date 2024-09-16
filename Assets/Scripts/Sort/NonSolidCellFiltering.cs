using Abecombe.GPUBufferOperators;
using UnityEngine;

public class NonSolidCellFiltering : GPUFiltering
{
    protected override void LoadComputeShader()
    {
        FilteringCs = Resources.Load<ComputeShader>("NonSolidCellFilteringCS");
    }
}