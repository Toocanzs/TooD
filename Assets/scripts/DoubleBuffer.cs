using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class DoubleBuffer
{
    private RenderTexture a, b;

    private bool aIsCurrent = true;
    
    public int2 Dimensions => new int2(Current.width, Current.height);

    public RenderTexture Current => aIsCurrent ? a : b;
    public RenderTexture Other => aIsCurrent ? b : a;
    public bool IsCreated => a.IsCreated() || b.IsCreated();

    public bool enableRandomWrite
    {
        set
        {
            a.enableRandomWrite = value;
            b.enableRandomWrite = value;
        }
    }

    public TextureWrapMode wrapMode
    {
        set
        {
            a.wrapMode = value;
            b.wrapMode = value;
        }
    }

    public DoubleBuffer(RenderTexture source)
    {
        a = source;
        b = new RenderTexture(source);
    }

    /*public void Swap()
    {
        aIsCurrent = !aIsCurrent;
    }*/
    //I HATE DOUBLE BUFFERING

    public void Release()
    {
        a.Release();
        b.Release();
    }
    public void Create()
    {
        a.Create();
        b.Create();
    }
}

public static class RenderTextureExtensions
{
    public static DoubleBuffer ToDoubleBuffer(this RenderTexture renderTexture)
    {
        return new DoubleBuffer(renderTexture);
    }
    
    public static void ReleaseIfExists(this RenderTexture rt)
    {
        if(rt != null && rt.IsCreated())
            rt.Release();
    }
        
    public static void ReleaseIfExists(this DoubleBuffer rt)
    {
        if(rt != null && rt.IsCreated)
            rt.Release();
    }
}