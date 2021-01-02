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
        private double goldenRatio = (1d + math.sqrt(5d)) / 2d;
        
        private static ComputeShader computeShader = (ComputeShader) Resources.Load("TooD2");
        private KernelInfo DispatchRays = new KernelInfo(computeShader, "DispatchRays");
        private KernelInfo AddGutter = new KernelInfo(computeShader, "AddGutter");

        public TooD2Renderer(TooD2RendererData data) : base(data)
        {
            tooDSpriteRenderPass = new TooD2SpriteRenderPass();
            RenderPipelineManager.endCameraRendering += OnEndRenderingCamera;
            RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
        }

        private void OnBeginFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            if (IrradianceManager2.Instance == null)
                return;
            if (!cameras.Contains(IrradianceManager2.Instance.camera))
                return;

            var manager = IrradianceManager2.Instance;
            int2 delta = manager.DoMove();
            CommandBuffer command = CommandBufferPool.Get("TooD Offset Old");
            command.Clear();
            float2 worldOffset = delta;
            float2 uvOffset = (worldOffset * manager.pixelsPerUnit) / new float2(manager.diffuseFullScreenAverageBuffer.Dimensions);
            command.Blit(manager.diffuseFullScreenAverageBuffer.Current, manager.diffuseFullScreenAverageBuffer.Other, Vector2.one, -uvOffset);
            
            var bl = manager.BottomLeft;
            command.SetGlobalVector("G_BottomLeft", new Vector4(bl.x, bl.y, 0, 0));
            command.SetGlobalVector("G_ProbeCounts", new Vector4(manager.probeCounts.x, manager.probeCounts.y, 0, 0));

            context.ExecuteCommandBuffer(command);
            command.Clear();
            CommandBufferPool.Release(command);
            GL.Flush();
            manager.diffuseFullScreenAverageBuffer.Swap();
            manager.UpdatePhiNoise();
        }

        private void OnEndRenderingCamera(ScriptableRenderContext context, Camera camera)
        {
            if (IrradianceManager2.Instance == null)
                return;
            if (camera != IrradianceManager2.Instance.camera)
                return;
            
            var manager = IrradianceManager2.Instance;

            //After rendering our irradiance camera:
            //Run our lighting code
            //Setup global textures
            //
            
            CommandBuffer command = CommandBufferPool.Get("TooD Rays");
            command.Clear();
            command.SetGlobalFloat("hysteresis", manager.hysteresis);
            
            //Send rays
            command.SetComputeTextureParam(computeShader, DispatchRays.index, "DiffuseRadialBuffer", manager.diffuseRadialBuffer);
            command.SetComputeTextureParam(computeShader, DispatchRays.index, "PhiNoise", manager.phiNoiseBuffer.Current);
            command.SetComputeTextureParam(computeShader, DispatchRays.index, "DiffuseAveragePerProbeBuffer", manager.diffuseAveragePerProbeBuffer);
            command.SetComputeTextureParam(computeShader, DispatchRays.index, "WallBuffer", manager.wallBuffer);
            command.SetComputeIntParam(computeShader, "pixelsPerProbe", manager.pixelsPerProbe);
            command.SetComputeIntParam(computeShader, "pixelsPerUnit", manager.pixelsPerUnit);
            command.SetComputeIntParam(computeShader, "MaxDirectRayLength", manager.MaxDirectRayLength);
            command.SetComputeIntParams(computeShader, "probeCounts", manager.probeCounts.x, manager.probeCounts.y);
            command.SetComputeFloatParam(computeShader, "time", Time.time);
            var bl = manager.BottomLeft;
            command.SetComputeIntParams(computeShader, "bottomLeft", bl.x, bl.y);
            command.DispatchCompute(computeShader, DispatchRays.index, DispatchRays.numthreads, new int3(manager.probeCounts, manager.pixelsPerProbe));
            
            //Add gutter
            command.SetComputeTextureParam(computeShader, AddGutter.index, "DiffuseRadialBuffer", manager.diffuseRadialBuffer);
            command.DispatchCompute(computeShader, AddGutter.index, AddGutter.numthreads, new int3(manager.probeCounts, 1));
            
            #if DEBUG
            command.SetGlobalTexture("G_IrradianceBand", manager.diffuseRadialBuffer);
            #endif
            
            //Draw randomly offset grid over the fullscreen buffer
            DrawOffsetGrid(command, manager, 0, 1f, 1f-manager.hysteresis);

            command.SetGlobalTexture("G_FullScreenAverageBuffer", manager.diffuseFullScreenAverageBuffer.Current);

            context.ExecuteCommandBuffer(command);
            command.Clear();
            CommandBufferPool.Release(command);
            //Make sure everything has run up to this point
            GL.Flush();
        }

        private static void DrawOffsetGrid(CommandBuffer command, IrradianceManager2 manager, int2 offset, float noiseScale, float alpha)
        {
            command.SetRenderTarget(manager.diffuseFullScreenAverageBuffer.Current);
            command.SetViewMatrix(Matrix4x4.identity);
            command.SetProjectionMatrix(Matrix4x4.Ortho(0, manager.probeCounts.x,
                0, manager.probeCounts.y,
                0.01f, 100));
            var block = new MaterialPropertyBlock();
            block.SetFloat("_Alpha", alpha);
            block.SetFloat("_NoiseScale", noiseScale);
            manager.gridOffsetMat.SetTexture("PerProbeAverageTexture", manager.diffuseAveragePerProbeBuffer);
            manager.gridOffsetMat.SetPass(0);
            command.DrawMesh(manager.gridMesh,
                float4x4.TRS(new float3(offset, -10), quaternion.identity,
                    new float3(manager.probeCounts.x, manager.probeCounts.y, 1)),
                manager.gridOffsetMat, 0, 0,  block);
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