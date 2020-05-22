using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class TooDRenderer : ScriptableRenderer
{
    private TooDSpriteRenderPass tooDSpriteRenderPass;
    private ComputeShader computeShader;
    private int probeRaycastMainKernel;
    private int FillGutterKernel;

    public TooDRenderer(TooDRendererData data) : base(data)
    {
        tooDSpriteRenderPass = new TooDSpriteRenderPass();
        computeShader = (ComputeShader) Resources.Load("ProbeRaycast");
        if (computeShader == null)
        {
            Debug.LogError("Cannot find raycast shader");
        }

        probeRaycastMainKernel = computeShader.FindKernel("GenerateProbeData");
        FillGutterKernel = computeShader.FindKernel("FillGutter");
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var colorTargetHandle = RenderTargetHandle.CameraTarget;
        ConfigureCameraTarget(colorTargetHandle.Identifier(), BuiltinRenderTextureType.CameraTarget);

        tooDSpriteRenderPass.ConfigureTarget(colorTargetHandle.Identifier());
        EnqueuePass(tooDSpriteRenderPass);
        if (IrradianceProbeManager.Instance != null)
        {
            var i = IrradianceProbeManager.Instance;
            CommandBuffer command = CommandBufferPool.Get("GenerateProbeData");
            command.Clear();
            command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "WallBuffer", i.wallBuffer);
            command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "IrradianceBuffer", i.irradianceBuffer);
            command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "AverageIrradianceBuffer", i.averageIrradianceBuffer);
            command.SetComputeFloatParam(computeShader, "probeSeparation", i.probeSeparation);

            float2 origin = i.GetProbeAreaOrigin() + i.OriginOffset;
            command.SetComputeFloatParams(computeShader, "probeStartPosition", origin.x, origin.y);
            command.SetComputeIntParam(computeShader, "directionCount", i.directionCount);
            command.SetComputeIntParam(computeShader, "maxRayLength", i.MaxRayLength);
            command.SetComputeIntParams(computeShader, "wallBufferSize", i.wallBuffer.width, i.wallBuffer.height);
            command.SetComputeIntParam(computeShader, "gutterSize", IrradianceProbeManager.GutterSize);
            command.SetComputeMatrixParam(computeShader, "worldToWallBuffer", i.worldToWallBuffer);
            command.SetComputeIntParams(computeShader, "probeCount", i.probeCounts.x, i.probeCounts.y);
            command.SetComputeFloatParam(computeShader, "HYSTERESIS", Time.deltaTime * i.hysteresis);
            command.SetComputeIntParam(computeShader, "pixelsPerUnit", i.pixelsPerUnit);
            command.SetComputeFloatParam(computeShader, "randomRayOffset", Random.Range(0, (2 * math.PI)/i.directionCount));
            command.DispatchCompute(computeShader, probeRaycastMainKernel,
                (i.probeCounts.x + 63)/64, i.probeCounts.y, 1);
            
            command.SetComputeTextureParam(computeShader, FillGutterKernel, "IrradianceBuffer", i.irradianceBuffer);
            command.DispatchCompute(computeShader, FillGutterKernel, (i.probeCounts.x + 63)/64, i.probeCounts.y, 1);
            
            //TODO: look at using 3d thread group, 3rd paramter for each direction? idk
            context.ExecuteCommandBuffer(command);
            command.Clear();
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