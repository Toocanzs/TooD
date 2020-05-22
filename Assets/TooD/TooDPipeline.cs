using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TooDRenderer : ScriptableRenderer
{
    private TooDSpriteRenderPass tooDSpriteRenderPass;
    private ComputeShader computeShader;
    private int probeRaycastMainKernel;
    private int JFA_set_seedKernel;
    private int JFA_floodKernel;
    private int JFA_distKernel;

    public TooDRenderer(TooDRendererData data) : base(data)
    {
        tooDSpriteRenderPass = new TooDSpriteRenderPass();
        computeShader = (ComputeShader) Resources.Load("ProbeRaycast");
        if (computeShader == null)
        {
            Debug.LogError("Cannot find raycast shader");
        }

        probeRaycastMainKernel = computeShader.FindKernel("GenerateProbeData");
        JFA_set_seedKernel = computeShader.FindKernel("JFA_set_seed");
        JFA_floodKernel = computeShader.FindKernel("JFA_flood");
        JFA_distKernel = computeShader.FindKernel("JFA_dist");
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
            command.SetComputeFloatParam(computeShader, "maxRayLength", i.MaxRayLength);
            command.SetComputeIntParams(computeShader, "wallBufferSize", i.wallBuffer.width, i.wallBuffer.height);
            command.SetComputeIntParam(computeShader, "gutterSize", IrradianceProbeManager.GutterSize);
            command.SetComputeMatrixParam(computeShader, "worldToWallBuffer", i.worldToWallBuffer);
            command.SetComputeIntParams(computeShader, "probeCount", i.probeCounts.x, i.probeCounts.y);
            command.SetComputeFloatParam(computeShader, "HYSTERESIS", Time.deltaTime * 5);
            command.SetComputeIntParam(computeShader, "pixelsPerUnit", i.pixelsPerUnit);

            
            command.SetComputeTextureParam(computeShader, JFA_set_seedKernel, "WallBuffer", i.wallBuffer);
            command.SetComputeTextureParam(computeShader, JFA_set_seedKernel, "ExteriorDistanceBuffer", i.sdfBuffer.Current);
            command.DispatchCompute(computeShader, JFA_set_seedKernel, (i.wallBuffer.width + 63) / 64, i.wallBuffer.height, 1);

            int maxSteps = 10;
            int maxOffsetPower = 10;
            //2^maxOffsetPower = how far it will jump maximum
            //ie: 2^8 means 256 pixels away is the max the jump value will ever be
            for (int jfaStepIndex = 0; jfaStepIndex < maxSteps; jfaStepIndex++)
            {
                int pow = math.max(0, maxOffsetPower - jfaStepIndex);
                int level = 1<<pow;

                //Grab the source and current from an index rather than executing the command buffer at each iteration of the loop
                //as long as maxSteps is even, current will remain current
                RenderTexture source = jfaStepIndex % 2 == 0 ? i.sdfBuffer.Current : i.sdfBuffer.Other;
                RenderTexture dest = jfaStepIndex % 2 == 0 ? i.sdfBuffer.Other : i.sdfBuffer.Current;
                command.SetComputeIntParam(computeShader, "stepWidth", level);
                command.SetComputeTextureParam(computeShader, JFA_floodKernel, "JFA_Source", source);
                command.SetComputeTextureParam(computeShader, JFA_floodKernel, "JFA_Dest", dest);
                
                command.DispatchCompute(computeShader, JFA_floodKernel, (i.wallBuffer.width + 63) / 64, i.wallBuffer.height, 1);
            }
            
            command.SetComputeTextureParam(computeShader, JFA_distKernel, "JFA_Source", i.sdfBuffer.Current);
            command.SetComputeTextureParam(computeShader, JFA_distKernel, "JFA_Dest", i.sdfBuffer.Other);
            command.DispatchCompute(computeShader, JFA_distKernel, (i.wallBuffer.width + 63) / 64, i.wallBuffer.height, 1);

            
            command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "SDF_Buffer", i.sdfBuffer.Other);
            
            command.DispatchCompute(computeShader, probeRaycastMainKernel,
                (i.probeCounts.x + 63)/64, i.probeCounts.y, 1);
            //TODO: look at using 3d thread group, 3rd paramter for each direction? idk
            context.ExecuteCommandBuffer(command);
            command.Clear();
            
            i.sdfBuffer.Swap();//Swap after so current is other cause we did a blit for calculating sdf distance
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