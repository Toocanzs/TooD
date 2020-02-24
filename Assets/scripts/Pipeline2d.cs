using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public class Custom2dRenderer : ScriptableRenderer
{
    private Custom2DRenderPass custom2DRenderPass;
    public Custom2dRenderer(Custom2dRendererData data) : base(data)
    {
        custom2DRenderPass = new Custom2DRenderPass();
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var colorTargetHandle = RenderTargetHandle.CameraTarget;
        ConfigureCameraTarget(colorTargetHandle.Identifier(), BuiltinRenderTextureType.CameraTarget);
        
        custom2DRenderPass.ConfigureTarget(colorTargetHandle.Identifier());
        EnqueuePass(custom2DRenderPass);
    }
}

public class Custom2DRenderPass : ScriptableRenderPass
{
    static readonly ShaderTagId k_CombinedRenderingPassNameOld = new ShaderTagId("Lightweight2D");
    static readonly ShaderTagId k_CombinedRenderingPassName = new ShaderTagId("Universal2D");
    static readonly ShaderTagId k_LegacyPassName = new ShaderTagId("SRPDefaultUnlit");
    static readonly List<ShaderTagId> k_ShaderTags = new List<ShaderTagId>() { k_LegacyPassName, k_CombinedRenderingPassName, k_CombinedRenderingPassNameOld };
    static SortingLayer[] sortingLayers;
    public Custom2DRenderPass()
    {
        if (sortingLayers == null)
            sortingLayers = SortingLayer.layers;
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        CommandBuffer command = CommandBufferPool.Get("Custom 2D");
        command.Clear();
        
        CameraClearFlags clearFlags = camera.clearFlags;
        command.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0,
            (clearFlags & CameraClearFlags.Color) != 0,
            camera.backgroundColor);
        context.ExecuteCommandBuffer(command);
        
        DrawingSettings combinedDrawSettings = CreateDrawingSettings(k_ShaderTags, ref renderingData, SortingCriteria.CommonTransparent);
        
        FilteringSettings filterSettings = new FilteringSettings();
        filterSettings.renderQueueRange = RenderQueueRange.all;
        filterSettings.layerMask = -1;
        filterSettings.renderingLayerMask = 0xFFFFFFFF;
        filterSettings.sortingLayerRange = SortingLayerRange.all;

        for (int i = 0; i < sortingLayers.Length; i++)
        {
            command.Clear();
            short layerValue = (short)sortingLayers[i].value;
            var lowerBound = (i == 0) ? short.MinValue : layerValue;
            var upperBound = (i == sortingLayers.Length - 1) ? short.MaxValue : layerValue;
            filterSettings.sortingLayerRange = new SortingLayerRange(lowerBound, upperBound);
            
            CoreUtils.SetRenderTarget(command, colorAttachment, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, ClearFlag.None, Color.white);
            context.ExecuteCommandBuffer(command);

            context.DrawRenderers(renderingData.cullResults, ref combinedDrawSettings, ref filterSettings);
        }
        
        CommandBufferPool.Release(command);
    }
}