using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TooD2
{
    public static class CommandBufferExtensions
    {
        public static void GenerateTempReadableCopy(this CommandBuffer command, int shaderPropertyId, RenderTexture rt)
        {
            command.GetTemporaryRT(shaderPropertyId, rt.descriptor);
            command.CopyTexture(rt,shaderPropertyId);
        }
    }
}