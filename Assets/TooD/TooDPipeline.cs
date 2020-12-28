﻿using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace TooD
{
    public class TooDRenderer : ScriptableRenderer
    {
        private TooDSpriteRenderPass tooDSpriteRenderPass;

        private static ComputeShader computeShader = (ComputeShader) Resources.Load("ProbeRaycast");
        private int probeRaycastMainKernel = computeShader.FindKernel("GenerateProbeData");
        private int GenerateCosineWeightedKernel = computeShader.FindKernel("GenerateCosineWeighted");
        private int FillGutterKernel = computeShader.FindKernel("FillGutter");

        private static ComputeShader jfaShader = (ComputeShader) Resources.Load("JFA");
        private KernelInfo JFA_set_seedKernel = new KernelInfo(jfaShader, "JFA_set_seed");
        private KernelInfo JFA_floodKernel = new KernelInfo(jfaShader, "JFA_flood");
        private KernelInfo JFA_distKernel = new KernelInfo(jfaShader, "JFA_dist");

        private float2 oldPos;
        private int noiseIndex = 0;
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
                    command.Blit(i.cosineWeightedIrradianceBuffer.Current, i.cosineWeightedIrradianceBuffer.Other,
                        i.dataTransferMaterial);
                    i.cosineWeightedIrradianceBuffer.Swap();

                    command.SetGlobalVector("_Offset",
                        (((i.GetProbeAreaOrigin() - oldPos).xy * i.pixelsPerUnit) /
                         i.fullScreenAverageIrradianceBuffer.Dimensions).xyxy);
                    command.Blit(i.fullScreenAverageIrradianceBuffer.Current, i.fullScreenAverageIrradianceBuffer.Other,
                        i.dataTransferMaterial);
                    i.fullScreenAverageIrradianceBuffer.Swap();
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
                command.Clear();

                //DoJfa(context, command, i);
                context.ExecuteCommandBuffer(command);
                command.Clear();
                i.SetDebug();
                CommandBufferPool.Release(command);
            }
        }

        private void OnEndRenderingCamera(ScriptableRenderContext context, Camera camera)
        {
            if (IrradianceProbeManager.Instance != null && camera.TryGetComponent(out TooDIrradianceCamera _))
            {
                var i = IrradianceProbeManager.Instance;
                CommandBuffer command = CommandBufferPool.Get("GenerateProbeData");
                command.Clear();
                RunLighting(context, command, i);

                command.Clear();

                CommandBufferPool.Release(command);
            }
        }

        private void RunLighting(ScriptableRenderContext context, CommandBuffer command, IrradianceProbeManager i)
        {
            command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "WallBuffer", i.wallBuffer);
            command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "IrradianceBuffer",
                i.irradianceBuffer);
            command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "AverageIrradianceBuffer",
                i.averageIrradiancePerProbeBuffer);
            command.SetComputeTextureParam(computeShader, probeRaycastMainKernel, "CosineWeightedIrradianceBuffer",
                i.cosineWeightedIrradianceBuffer);
            command.SetComputeFloatParam(computeShader, "probeSeparation", i.probeSeparation);

            float2 origin = i.GetProbeAreaOrigin() + i.OriginOffset;
            command.SetComputeFloatParams(computeShader, "probeAreaStartPosition", origin.x, origin.y);
            command.SetComputeIntParam(computeShader, "directionCount", i.directionCount);
            command.SetComputeIntParam(computeShader, "maxDirectRayLength", i.MaxRayLength);
            command.SetComputeIntParams(computeShader, "wallBufferSize", i.wallBuffer.width, i.wallBuffer.height);
            command.SetComputeIntParam(computeShader, "gutterSize", IrradianceProbeManager.GutterSize);

            command.SetComputeIntParams(computeShader, "probeCount", i.probeCounts.x, i.probeCounts.y);
            command.SetComputeFloatParam(computeShader, "HYSTERESIS", Time.deltaTime * i.hysteresis);
            command.SetComputeIntParam(computeShader, "pixelsPerUnit", i.pixelsPerUnit);

            float rayOffset = (2 * math.PI * noiseIndex) * (goldenRatio - 1);

            rayOffset = math.frac(rayOffset) * ((2 * math.PI) / i.directionCount);
            noiseIndex += 4; //This works lol

            float2 randomProbeOffset = new float2(Random.Range(-i.probeSeparation, i.probeSeparation),
                Random.Range(-i.probeSeparation, i.probeSeparation)) / 2f;
            command.SetComputeFloatParam(computeShader, "randomRayOffset", rayOffset);
            command.SetComputeVectorParam(computeShader, "randomProbeOffset", randomProbeOffset.xyxy);


            float3 pos = new float3(i.GetProbeAreaOrigin(), 0);
            //Translate so origin is at probe origin
            float4x4 step1 = float4x4.Translate(-pos);
            //Scale afterwards to buffersize
            float4x4 step2 = float4x4.Scale(i.pixelsPerUnit);
            var worldToWallBuffer = math.mul(step2, step1);

            command.SetComputeMatrixParam(computeShader, "worldToWallBuffer", worldToWallBuffer);

            var worldDirectionToBufferDirection = float4x4.Scale(1f / i.BufferSize.y, 1f / i.BufferSize.x, 0);
            command.SetComputeVectorParam(computeShader, "_ProbeAreaOrigin", i.GetProbeAreaOrigin().xyxy);
            command.SetComputeMatrixParam(computeShader, "worldDirectionToBufferDirection",
                worldDirectionToBufferDirection);

            command.DispatchCompute(computeShader, probeRaycastMainKernel,
                (i.probeCounts.x + 63) / 64, i.probeCounts.y, 2);

            /*
            command.SetComputeTextureParam(computeShader, CopyToFullscreenKernel, "AverageIrradianceBuffer", i.averageIrradiancePerProbeBuffer);
            command.SetComputeTextureParam(computeShader, CopyToFullscreenKernel, "FullScreenAverage", i.fullScreenAverageIrradianceBuffer);
            command.DispatchCompute(computeShader, CopyToFullscreenKernel,
                (i.fullScreenAverageIrradianceBuffer.Dimensions.x + 63) / 64, i.fullScreenAverageIrradianceBuffer.Dimensions.y, 1);*/

            float2 averageOffset = -randomProbeOffset * i.pixelsPerUnit * i.probeSeparation;
            averageOffset /= i.fullScreenAverageIrradianceBuffer.Dimensions;

            i.fullScreenCopyMaterial.SetVector("_Offset", averageOffset.xyxy);
            i.fullScreenCopyMaterial.SetFloat("HYSTERESIS", Time.deltaTime * i.hysteresis);
            i.fullScreenCopyMaterial.SetTexture("_FullScreenAverage", i.fullScreenAverageIrradianceBuffer.Current);
            command.Blit(i.averageIrradiancePerProbeBuffer, i.fullScreenAverageIrradianceBuffer.Other,
                i.fullScreenCopyMaterial);
            i.fullScreenAverageIrradianceBuffer.Swap();

            command.SetComputeTextureParam(computeShader, GenerateCosineWeightedKernel, "IrradianceBuffer",
                i.irradianceBuffer);
            command.SetComputeTextureParam(computeShader, GenerateCosineWeightedKernel,
                "CosineWeightedIrradianceBuffer", i.cosineWeightedIrradianceBuffer);
            command.DispatchCompute(computeShader, GenerateCosineWeightedKernel, (i.probeCounts.x + 63) / 64,
                i.probeCounts.y, 1);

            command.SetComputeTextureParam(computeShader, FillGutterKernel, "CosineWeightedIrradianceBuffer",
                i.cosineWeightedIrradianceBuffer);
            command.DispatchCompute(computeShader, FillGutterKernel, (i.probeCounts.x + 63) / 64, i.probeCounts.y, 1);

            context.ExecuteCommandBuffer(command);
            command.Clear();
        }

        /*private void DoJfa(ScriptableRenderContext context, CommandBuffer command, IrradianceProbeManager i)
        {
            command.SetComputeIntParam(jfaShader, "pixelsPerUnit", i.pixelsPerUnit);
            command.SetComputeIntParams(jfaShader, "wallBufferSize", i.wallBuffer.width, i.wallBuffer.height);
            command.SetComputeVectorParam(jfaShader, "_ProbeAreaOrigin", i.GetProbeAreaOrigin().xyxy);
            command.SetComputeTextureParam(jfaShader, JFA_set_seedKernel.index, "WallBuffer", i.wallBuffer);
            command.SetComputeTextureParam(jfaShader, JFA_set_seedKernel.index, "ExteriorDistanceBuffer", i.sdfBuffer.Current);
            //command.DispatchCompute(jfaShader, JFA_set_seedKernel.index, JFA_set_seedKernel.numthreads, new int3(i.wallBuffer.width, i.wallBuffer.height, 1));
            command.DispatchCompute(jfaShader, JFA_set_seedKernel.index, (i.wallBuffer.width + 63) / 64, i.wallBuffer.height, 1);
            int maxSteps = 10;
            int maxOffsetPower = 10;
            //2^maxOffsetPower = how far it will jump maximum
            //ie: 2^8 means 256 pixels away is the max the jump value will ever be
            for (int jfaStepIndex = 0; jfaStepIndex < maxSteps; jfaStepIndex++)
            {
                int pow = math.max(0, maxOffsetPower - jfaStepIndex);
                int level = 1 << pow;
    
                //Grab the source and current from an index rather than executing the command buffer at each iteration of the loop
                //as long as maxSteps is even, current will remain current
                RenderTexture source = jfaStepIndex % 2 == 0 ? i.sdfBuffer.Current : i.sdfBuffer.Other;
                RenderTexture dest = jfaStepIndex % 2 == 0 ? i.sdfBuffer.Other : i.sdfBuffer.Current;
                command.SetComputeIntParam(jfaShader, "stepWidth", level);
                command.SetComputeTextureParam(jfaShader, JFA_floodKernel.index, "JFA_Source", source);
                command.SetComputeTextureParam(jfaShader, JFA_floodKernel.index, "JFA_Dest", dest);
    
                command.DispatchCompute(jfaShader, JFA_floodKernel.index, (i.wallBuffer.width + 63) / 64, i.wallBuffer.height, 1);
            }
    
            command.SetComputeTextureParam(jfaShader, JFA_distKernel.index, "JFA_Source", i.sdfBuffer.Current);
            command.SetComputeTextureParam(jfaShader, JFA_distKernel.index, "JFA_Dest", i.sdfBuffer.Other);
            command.DispatchCompute(jfaShader, JFA_distKernel.index, (i.wallBuffer.width + 63) / 64, i.wallBuffer.height, 1);
            context.ExecuteCommandBuffer(command);
            command.Clear();
            i.sdfBuffer.Swap(); //Swap after so current is other cause we did a blit for calculating sdf distance
        }*/

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

        static readonly List<ShaderTagId> lightingShaderTags = new List<ShaderTagId>()
            {new ShaderTagId("TooDLighting")};

        static SortingLayer[] sortingLayers;

        public TooDSpriteRenderPass()
        {
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
}