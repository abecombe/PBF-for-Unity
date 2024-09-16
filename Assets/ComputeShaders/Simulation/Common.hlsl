#ifndef CS_SIMULATION_COMMON_HLSL
#define CS_SIMULATION_COMMON_HLSL

float _DeltaTime;

#include "Assets/Packages/GPUUtil/DispatchHelper.hlsl"

#include "../Constant.hlsl"

#include "../GridData.hlsl"
#include "../GridHelper.hlsl"

#include "Particle.hlsl"
#include "BoundaryCondition.hlsl"

#include "PBFKernels.hlsl"


#endif /* CS_SIMULATION_COMMON_HLSL */