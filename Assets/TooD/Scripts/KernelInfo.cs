using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public struct KernelInfo
{
    public int index;
    public int3 numthreads;

    public KernelInfo(ComputeShader shader, string name)
    {
        index = shader.FindKernel(name);
        shader.GetKernelThreadGroupSizes(index, out uint x, out uint y, out uint z);
        numthreads = new int3((int)x,(int)y,(int)z);
    }
}

public static class CommandBufferExtensions
{
    public static void DispatchCompute(this CommandBuffer command, ComputeShader shader, int kernelId, int3 numthreads, int3 dataSize)
    {
        int3 groups = (dataSize + numthreads - 1) / numthreads;
        command.DispatchCompute(shader, kernelId, groups.x, groups.y, groups.z);
    }
}