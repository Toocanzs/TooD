using System;
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

    private static ComputeShader computeShader = (ComputeShader) Resources.Load("ProbeRaycast");
    private int probeRaycastMainKernel = computeShader.FindKernel("GenerateProbeData");
    private int CopyToFullscreenKernel = computeShader.FindKernel("CopyToFullscreen");
    private int GenerateCosineWeightedKernel = computeShader.FindKernel("GenerateCosineWeighted");
    private int FillGutterKernel = computeShader.FindKernel("FillGutter");

    private float2 oldPos;
    private float3 oldCamPos;
    private float goldenRatio = (1 + math.sqrt(5)) / 2;

    public TooDRenderer(TooDRendererData data) : base(data)
    {
        tooDSpriteRenderPass = new TooDSpriteRenderPass();
        RenderPipelineManager.endCameraRendering += OnEndRenderingCamera;
        RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
    }

    private void OnBeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
    {
        if (IrradianceProbeManager.Instance != null)
        {
            var i = IrradianceProbeManager.Instance;
            float2 center = i.GetCenter();
            float2 scale = i.GetWorldScale();
            float2 resetScale = scale * i.resetAreaPercent;
            float4 resetBounds = new float4(center - resetScale / 2, center + resetScale / 2);

            float2 cameraPos = math.float3(Camera.main.transform.position).xy;

            oldPos = i.GetProbeAreaOrigin();
            oldCamPos = i.transform.position;
            bool moved = false;
            if (math.any(math.bool4(cameraPos.xy < resetBounds.xy, cameraPos.xy > resetBounds.zw)))
            {
                moved = true;
                i.SetCenter(i.transform, cameraPos.xy);
            }
            
            CommandBuffer command = CommandBufferPool.Get("setup");
            command.Clear();
            if (moved)
            {
                float2 probesPerUnit = (i.GetProbeAreaOrigin() - oldPos).xy / i.probeSeparation;
                float2 pixelOffset = probesPerUnit * new float2(i.SingleProbePixelWidth, 1);
                float2 uvOffset = pixelOffset / i.cosineWeightedIrradianceBuffer.Dimensions;
                command.SetGlobalVector("_Offset", uvOffset.xyxy);
                command.Blit(i.cosineWeightedIrradianceBuffer.Current, i.cosineWeightedIrradianceBuffer.Other, i.dataTransferMaterial);
                command.Blit(i.cosineWeightedIrradianceBuffer.Other, i.cosineWeightedIrradianceBuffer.Current);

                command.SetGlobalVector("_Offset", (((i.GetProbeAreaOrigin() - oldPos).xy * i.pixelsPerUnit) / i.fullScreenAverageIrradianceBuffer.Dimensions).xyxy);
                command.Blit(i.fullScreenAverageIrradianceBuffer.Current, i.fullScreenAverageIrradianceBuffer.Other, i.dataTransferMaterial);
                command.Blit(i.fullScreenAverageIrradianceBuffer.Other, i.fullScreenAverageIrradianceBuffer.Current);
            }
            
            i.lightingCamera.transform.position = new float3(i.GetCenter(), i.lightingCamera.transform.position.z);
            i.lightingCamera.orthographicSize = scale.y / 2;
            i.lightingCamera.aspect = scale.x / scale.y;

            command.SetGlobalFloat("G_ProbeSeparation", i.probeSeparation);
            command.SetGlobalTexture("G_AverageIrradianceBuffer", i.fullScreenAverageIrradianceBuffer);
            command.SetGlobalVector("G_ProbeCounts", (float4) i.probeCounts.xyxy);
            command.SetGlobalVector("G_ProbeAreaOrigin", i.GetProbeAreaOrigin().xyxy);
            command.SetGlobalInt("G_PixelsPerUnit", i.pixelsPerUnit);
            context.ExecuteCommandBuffer(command);
            CommandBufferPool.Release(command);
        }
    }

    private int noiseIndex = 0;
    private void OnEndRenderingCamera(ScriptableRenderContext context, Camera camera)
    {
        if (IrradianceProbeManager.Instance != null && camera.TryGetComponent(out TooDIrradianceCamera _))
        {
            var i = IrradianceProbeManager.Instance;
            CommandBuffer command = CommandBufferPool.Get("GenerateProbeData");
            command.Clear();
            RunLighting(command, i);
            context.ExecuteCommandBuffer(command);
            command.Clear();

            CommandBufferPool.Release(command);
        }
    }

    private void RunLighting(CommandBuffer command, IrradianceProbeManager i)
    {
        command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "WallBuffer", i.wallBuffer);
        command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "IrradianceBuffer", i.irradianceBuffer);
        command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "AverageIrradianceBuffer", i.averageIrradiancePerProbeBuffer);
        command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "CosineWeightedIrradianceBuffer", i.cosineWeightedIrradianceBuffer);
        command.SetComputeFloatParam(computeShader, "probeSeparation", i.probeSeparation);

        float2 origin = i.GetProbeAreaOrigin() + i.OriginOffset;
        command.SetComputeFloatParams(computeShader, "probeAreaStartPosition", origin.x, origin.y);
        command.SetComputeIntParam(computeShader, "directionCount", i.directionCount);
        command.SetComputeIntParam(computeShader, "maxRayLength", i.MaxRayLength);
        command.SetComputeIntParams(computeShader, "wallBufferSize", i.wallBuffer.width, i.wallBuffer.height);
        command.SetComputeIntParam(computeShader, "gutterSize", IrradianceProbeManager.GutterSize);

        command.SetComputeIntParams(computeShader, "probeCount", i.probeCounts.x, i.probeCounts.y);
        command.SetComputeFloatParam(computeShader, "HYSTERESIS", Time.deltaTime * i.hysteresis);
        command.SetComputeIntParam(computeShader, "pixelsPerUnit", i.pixelsPerUnit);

        float rayOffset = (2 * math.PI * noiseIndex) * (goldenRatio - 1);

        rayOffset = math.frac(rayOffset) * ((2 * math.PI) / i.directionCount);
        noiseIndex += 4; //This works lol
        //float a = Random.Range(0, (2 * math.PI) / i.directionCount);

        command.SetComputeFloatParam(computeShader, "randomRayOffset", rayOffset);
        command.SetComputeVectorParam(computeShader, "randomProbeOffset",
            new float2(Random.Range(-i.probeSeparation / 2f, i.probeSeparation / 2f), Random.Range(-i.probeSeparation / 2f, i.probeSeparation / 2f)).xyxy);


        float3 pos = new float3(i.GetProbeAreaOrigin(), 0);
        //Translate so origin is at probe origin
        float4x4 step1 = float4x4.Translate(-pos);
        //Scale afterwards to buffersize
        float4x4 step2 = float4x4.Scale(i.pixelsPerUnit);
        var worldToWallBuffer = math.mul(step2, step1);

        command.SetComputeMatrixParam(computeShader, "worldToWallBuffer", worldToWallBuffer);

        var worldDirectionToBufferDirection = float4x4.Scale(1f / i.BufferSize.y, 1f / i.BufferSize.x, 0);
        command.SetComputeVectorParam(computeShader, "_ProbeAreaOrigin", i.GetProbeAreaOrigin().xyxy);
        command.SetComputeMatrixParam(computeShader, "worldDirectionToBufferDirection", worldDirectionToBufferDirection);

        command.DispatchCompute(computeShader, probeRaycastMainKernel,
            (i.probeCounts.x + 63) / 64, i.probeCounts.y, 4);

        //CopyToFullscreen
        command.SetComputeTextureParam(computeShader, CopyToFullscreenKernel, "AverageIrradianceBuffer", i.averageIrradiancePerProbeBuffer);
        command.SetComputeTextureParam(computeShader, CopyToFullscreenKernel, "FullScreenAverage", i.fullScreenAverageIrradianceBuffer);
        command.DispatchCompute(computeShader, CopyToFullscreenKernel,
            (i.fullScreenAverageIrradianceBuffer.Dimensions.x + 63) / 64, i.fullScreenAverageIrradianceBuffer.Dimensions.y, 1);

        command.SetComputeTextureParam(computeShader, GenerateCosineWeightedKernel, "IrradianceBuffer", i.irradianceBuffer);
        command.SetComputeTextureParam(computeShader, GenerateCosineWeightedKernel, "CosineWeightedIrradianceBuffer", i.cosineWeightedIrradianceBuffer);
        command.DispatchCompute(computeShader, GenerateCosineWeightedKernel, (i.probeCounts.x + 63) / 64, i.probeCounts.y, 1);

        command.SetComputeTextureParam(computeShader, FillGutterKernel, "CosineWeightedIrradianceBuffer", i.cosineWeightedIrradianceBuffer);
        command.DispatchCompute(computeShader, FillGutterKernel, (i.probeCounts.x + 63) / 64, i.probeCounts.y, 1);
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var colorTargetHandle = RenderTargetHandle.CameraTarget;
        ConfigureCameraTarget(colorTargetHandle.Identifier(), BuiltinRenderTextureType.CameraTarget);
        tooDSpriteRenderPass.ConfigureTarget(colorTargetHandle.Identifier());
        EnqueuePass(tooDSpriteRenderPass);
    }
}

public class TooDSpriteRenderPass : ScriptableRenderPass
{
    static readonly List<ShaderTagId> normalShaderTags = new List<ShaderTagId>() {new ShaderTagId("Universal2D")};

    static readonly List<ShaderTagId> lightingShaderTags = new List<ShaderTagId>() {new ShaderTagId("TooDLighting")};

    //TODO: separate into two passes. Delete the lighting camera and reuse the default one
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