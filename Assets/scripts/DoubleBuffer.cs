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

    public bool enableRandomWrite
    {
        set
        {
            a.enableRandomWrite = value;
            b.enableRandomWrite = value;
        }
    }

    public DoubleBuffer(RenderTexture source)
    {
        a = source;
        b = new RenderTexture(source);
    }

    public void Swap()
    {
        aIsCurrent = !aIsCurrent;
    }

    public void Release()
    {
        a.Release();
        b.Release();
    }

    public static implicit operator RenderTexture(DoubleBuffer buffer)
    {
        return buffer.Current;
    }

    public static implicit operator RenderTargetIdentifier(DoubleBuffer buffer)
    {
        return new RenderTargetIdentifier(buffer.Current);
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
}