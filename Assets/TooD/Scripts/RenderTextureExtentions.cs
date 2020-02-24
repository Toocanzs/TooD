using Unity.Mathematics;
using UnityEngine;

public static class RenderTextureExtensions
{
    public static void ReleaseIfExists(this RenderTexture rt)
    {
        if(rt != null && rt.IsCreated())
            rt.Release();
    }

    public static int2 Dimensions(this RenderTexture rt)
    {
        return new int2(rt.width, rt.height);
    }
}