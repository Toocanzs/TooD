using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityMathematicsExtentions;

namespace TooD2
{
    public class TooD2Renderer : ScriptableRenderer
    {
        private TooD2SpriteRenderPass tooDSpriteRenderPass;

        private static ComputeShader computeShader = (ComputeShader) Resources.Load("TooD2");
        private KernelInfo DispatchRays = new KernelInfo(computeShader, "DispatchRays");
        private KernelInfo AddGutter = new KernelInfo(computeShader, "AddGutter");
        private KernelInfo SumRays = new KernelInfo(computeShader, "SumRays");

        private static readonly int PerProbeAverageTexture = Shader.PropertyToID("PerProbeAverageTexture");
        private static readonly int OldColorId = Shader.PropertyToID("OldColor");

        //TEMP RTS:
        private static readonly int TempTextureId = Shader.PropertyToID("__TEMP__TEXTURE");

        public TooD2Renderer(TooD2RendererData data) : base(data)
        {
            tooDSpriteRenderPass = new TooD2SpriteRenderPass();
            RenderPipelineManager.endCameraRendering += OnEndRenderingCamera;
            RenderPipelineManager.beginFrameRendering += BeginFrameRendering;
        }

        private void BeginFrameRendering(ScriptableRenderContext context, Camera[] camera)
        {
            if (IrradianceManager2.Instance == null)
                return;
            var manager = IrradianceManager2.Instance;


            int2 delta = manager.DoMove();
            CommandBuffer command = CommandBufferPool.Get("TooD Setup");
            command.Clear();
            command.GenerateTempReadableCopy(TempTextureId, manager.diffuseFullScreenAverageBuffer);
            float2 worldOffset = delta;
            float2 uvOffset = (worldOffset * manager.pixelsPerUnit) /
                              manager.diffuseFullScreenAverageBuffer.Dimensions();
            command.Blit(TempTextureId, manager.diffuseFullScreenAverageBuffer, Vector2.one, -uvOffset);
            command.ReleaseTemporaryRT(TempTextureId);
            context.ExecuteCommandBuffer(command);
        }

        private void OnEndRenderingCamera(ScriptableRenderContext context, Camera camera)
        {
            if (IrradianceManager2.Instance == null)
                return;
            if (camera != IrradianceManager2.Instance.camera)
                return;
            var manager = IrradianceManager2.Instance;

            CommandBuffer command = CommandBufferPool.Get("TooD Rays");
            command.Clear();

            var bl = manager.BottomLeft;
            command.SetGlobalVector("G_BottomLeft", new Vector4(bl.x, bl.y, 0, 0));
            command.SetGlobalVector("G_ProbeCounts",
                new Vector4(manager.probeCounts.x, manager.probeCounts.y, 0, 0));
            command.SetGlobalInt("G_PixelsPerProbe", manager.pixelsPerProbe);
            manager.UpdatePhiNoise(command);

            //Send rays
            command.SetComputeTextureParam(computeShader, DispatchRays.index, "DiffuseRadialBuffer",
                manager.diffuseRadialBuffer);
            command.SetComputeTextureParam(computeShader, DispatchRays.index, "PhiNoise",
                manager.phiNoiseBuffer);
            command.GetTemporaryRT(PerProbeAverageTexture, manager.diffuseAveragePerProbeBufferDescriptor);
            command.SetComputeTextureParam(computeShader, DispatchRays.index, "WallBuffer", manager.wallBuffer);
            command.SetComputeIntParam(computeShader, "pixelsPerProbe", manager.pixelsPerProbe);
            command.SetComputeIntParam(computeShader, "pixelsPerUnit", manager.pixelsPerUnit);
            command.SetComputeIntParam(computeShader, "MaxDirectRayLength", manager.MaxDirectRayLength);
            command.SetComputeIntParams(computeShader, "probeCounts", manager.probeCounts.x, manager.probeCounts.y);
            command.SetComputeFloatParam(computeShader, "time", Time.time);
            command.SetComputeIntParams(computeShader, "bottomLeft", bl.x, bl.y);
            command.DispatchCompute(computeShader, DispatchRays.index, DispatchRays.numthreads,
                new int3(manager.probeCounts, manager.pixelsPerProbe));
            
            command.SetComputeTextureParam(computeShader, SumRays.index, "DiffuseAveragePerProbeBuffer",
                PerProbeAverageTexture);
            command.SetComputeTextureParam(computeShader, SumRays.index, "DiffuseRadialBuffer",
                manager.diffuseRadialBuffer);
            command.DispatchCompute(computeShader, SumRays.index, SumRays.numthreads, new int3(manager.probeCounts, 1));

            //Add gutter
            command.SetComputeTextureParam(computeShader, AddGutter.index, "DiffuseRadialBuffer",
                manager.diffuseRadialBuffer);
            command.DispatchCompute(computeShader, AddGutter.index, AddGutter.numthreads,
                new int3(manager.probeCounts, 1));

#if DEBUG
            command.SetGlobalTexture("G_IrradianceBand", manager.diffuseRadialBuffer);
#endif

            //Draw randomly offset grid over the fullscreen buffer
            command.GetTemporaryRT(TempTextureId, manager.diffuseFullScreenAverageBuffer.descriptor);
            command.SetRenderTarget(TempTextureId);
            command.ClearRenderTarget(false, true, Color.clear);
            command.SetViewMatrix(Matrix4x4.identity);
            command.SetProjectionMatrix(Matrix4x4.Ortho(0, manager.probeCounts.x,
                0, manager.probeCounts.y,
                0.01f, 100));
            var block = new MaterialPropertyBlock();
            block.SetFloat("_Alpha", 1f - manager.hysteresis);
            manager.quadsOffsetMaterial.SetPass(0);
            command.DrawMesh(manager.quadsMesh,
                float4x4.TRS(new float3(0, 0, -10), quaternion.identity,
                    new float3(manager.probeCounts.x, manager.probeCounts.y, 1)),
                manager.quadsOffsetMaterial, 0, 0, block);
            
            command.GenerateTempReadableCopy(OldColorId, manager.diffuseFullScreenAverageBuffer);
            manager.SmartBlendedBlitMaterial.SetFloat("Hysteresis", manager.hysteresis);
            command.Blit(TempTextureId, manager.diffuseFullScreenAverageBuffer, manager.SmartBlendedBlitMaterial);
            command.ReleaseTemporaryRT(TempTextureId);
            command.ReleaseTemporaryRT(OldColorId);

            command.SetGlobalTexture("G_FullScreenAverageBuffer", manager.diffuseFullScreenAverageBuffer);
            command.ReleaseTemporaryRT(PerProbeAverageTexture);
            context.ExecuteCommandBuffer(command);
            command.Clear();
            CommandBufferPool.Release(command);
        }

        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var colorTargetHandle = RenderTargetHandle.CameraTarget;
            ConfigureCameraTarget(colorTargetHandle.Identifier(), BuiltinRenderTextureType.CameraTarget);
            tooDSpriteRenderPass.ConfigureTarget(colorTargetHandle.Identifier());
            EnqueuePass(tooDSpriteRenderPass);
        }
    }

    public class TooD2SpriteRenderPass : ScriptableRenderPass
    {
        static readonly List<ShaderTagId> normalShaderTags = new List<ShaderTagId> {new ShaderTagId("Universal2D")};

        static readonly List<ShaderTagId> lightingShaderTags = new List<ShaderTagId> {new ShaderTagId("TooDLighting")};

        static SortingLayer[] sortingLayers;

        public TooD2SpriteRenderPass()
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
            if (camera.TryGetComponent(out TooDIrradianceCamera _) && IrradianceManager2.Instance != null)
            {
                tags = lightingShaderTags;
                if (Application.isPlaying)
                {
                    renderTargetIdentifier = IrradianceManager2.Instance.wallBuffer;
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