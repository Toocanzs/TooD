using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TooDRenderer : ScriptableRenderer
{
    private TooDSpriteRenderPass tooDSpriteRenderPass;
    private ComputeShader probeRaycastShader;
    private int probeRaycastMainKernel;

    public TooDRenderer(TooDRendererData data) : base(data)
    {
        tooDSpriteRenderPass = new TooDSpriteRenderPass();
        probeRaycastShader = (ComputeShader) Resources.Load("ProbeRaycast");
        if (probeRaycastShader == null)
        {
            Debug.LogError("Cannot find raycast shader");
        }

        probeRaycastMainKernel = probeRaycastShader.FindKernel("GenerateProbeData");
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var colorTargetHandle = RenderTargetHandle.CameraTarget;
        ConfigureCameraTarget(colorTargetHandle.Identifier(), BuiltinRenderTextureType.CameraTarget);

        tooDSpriteRenderPass.ConfigureTarget(colorTargetHandle.Identifier());
        EnqueuePass(tooDSpriteRenderPass);
        if (IrradianceProbeManager.Instance != null)
        {
            CommandBuffer command = CommandBufferPool.Get($"GenerateProbeData");
            command.Clear();
            command.SetComputeTextureParam(probeRaycastShader, probeRaycastMainKernel, "WallBuffer",
                IrradianceProbeManager.Instance.wallBuffer);
            command.SetComputeTextureParam(probeRaycastShader, probeRaycastMainKernel, "IrradianceBuffer",
                IrradianceProbeManager.Instance.irradianceBuffer);
            command.SetComputeTextureParam(probeRaycastShader, probeRaycastMainKernel, "AverageIrradianceBuffer",
                IrradianceProbeManager.Instance.averageIrradianceBuffer);

            command.SetComputeFloatParam(probeRaycastShader, "probeSeparation",
                IrradianceProbeManager.Instance.probeSeparation);

            float2 origin = IrradianceProbeManager.Instance.GetProbeAreaOrigin() +
                            IrradianceProbeManager.Instance.OriginOffset;
            command.SetComputeFloatParams(probeRaycastShader, "probeStartPosition",
                origin.x, origin.y);
            command.SetComputeIntParam(probeRaycastShader, "directionCount",
                IrradianceProbeManager.Instance.directionCount);
            command.SetComputeFloatParam(probeRaycastShader, "maxRayLength",
                IrradianceProbeManager.Instance.MaxRayLength);
            command.SetComputeIntParams(probeRaycastShader, "wallBufferSize",
                IrradianceProbeManager.Instance.wallBuffer.width, IrradianceProbeManager.Instance.wallBuffer.height);
            command.SetComputeIntParam(probeRaycastShader, "gutterSize", IrradianceProbeManager.GutterSize);
            command.SetComputeMatrixParam(probeRaycastShader, "worldToWallBuffer",
                IrradianceProbeManager.Instance.worldToWallBuffer);
            command.SetComputeMatrixParam(probeRaycastShader, "wallBufferToWorld", math.fastinverse(IrradianceProbeManager.Instance.worldToWallBuffer));
            command.SetComputeIntParams(probeRaycastShader, "probeCount", 
                IrradianceProbeManager.Instance.probeCounts.x, IrradianceProbeManager.Instance.probeCounts.y);


            command.DispatchCompute(probeRaycastShader, probeRaycastMainKernel,
                IrradianceProbeManager.Instance.probeCounts.x, IrradianceProbeManager.Instance.probeCounts.y, 1);
            context.ExecuteCommandBuffer(command);
            command.Clear();
            //TODO: gutter? We only need it once we start sampling directions instead of averages
        }
    }
}

public class TooDSpriteRenderPass : ScriptableRenderPass
{
    static readonly List<ShaderTagId> normalShaderTags = new List<ShaderTagId>() {new ShaderTagId("Universal2D")};
    static readonly List<ShaderTagId> lightingShaderTags = new List<ShaderTagId>() {new ShaderTagId("TooDLighting")};
    static SortingLayer[] sortingLayers;

    public TooDSpriteRenderPass()
    {
        if (sortingLayers == null)
            sortingLayers = SortingLayer.layers;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var camera = renderingData.cameraData.camera;
        CommandBuffer command = CommandBufferPool.Get($"Custom 2D {camera.name}");
        command.Clear();

        var renderTargetIdentifier = colorAttachment;
        var tags = normalShaderTags;
        var clearFlags = ClearFlag.None;
        if (camera.TryGetComponent(out TooDIrradianceCamera _))
        {
            tags = lightingShaderTags;
            if (Application.isPlaying)
            {
                renderTargetIdentifier = IrradianceProbeManager.Instance.wallBuffer;
                clearFlags = ClearFlag.All;
            }
        }

        DrawingSettings combinedDrawSettings =
            CreateDrawingSettings(tags, ref renderingData, SortingCriteria.CommonTransparent);

        FilteringSettings filterSettings = new FilteringSettings();
        filterSettings.renderQueueRange = RenderQueueRange.all;
        filterSettings.layerMask = -1;
        filterSettings.renderingLayerMask = 0xFFFFFFFF;
        filterSettings.sortingLayerRange = SortingLayerRange.all;

        for (int i = 0; i < sortingLayers.Length; i++)
        {
            command.Clear();
            short layerValue = (short) sortingLayers[i].value;
            var lowerBound = (i == 0) ? short.MinValue : layerValue;
            var upperBound = (i == sortingLayers.Length - 1) ? short.MaxValue : layerValue;
            filterSettings.sortingLayerRange = new SortingLayerRange(lowerBound, upperBound);

            CoreUtils.SetRenderTarget(command, renderTargetIdentifier, RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store, clearFlags, camera.backgroundColor);
            context.ExecuteCommandBuffer(command);

            context.DrawRenderers(renderingData.cullResults, ref combinedDrawSettings, ref filterSettings);
        }

        CommandBufferPool.Release(command);
    }
}