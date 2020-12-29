using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Unity.Mathematics.math;
namespace TooD2
{
    public class TooD2Renderer : ScriptableRenderer
    {
        private TooD2SpriteRenderPass tooDSpriteRenderPass;
        private double goldenRatio = (1d + sqrt(5d)) / 2d;
        
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
            
            //Send rays
            command.SetComputeTextureParam(computeShader, DispatchRays.index, "IrradianceBands", manager.irradianceBandBuffer.Current);
            command.SetComputeTextureParam(computeShader, DispatchRays.index, "PhiNoise", manager.phiNoiseBuffer.Current);
            command.SetComputeIntParam(computeShader, "pixelsPerProbe", manager.pixelsPerProbe);
            command.SetComputeIntParam(computeShader, "pixelsPerUnit", manager.pixelsPerUnit);
            command.SetComputeIntParams(computeShader, "probeCounts", manager.probeCounts.x, manager.probeCounts.y);
            command.SetComputeFloatParam(computeShader, "time", Time.time);
            var bl = manager.BottomLeft;
            command.SetComputeIntParams(computeShader, "bottomLeft", bl.x, bl.y);
            command.DispatchCompute(computeShader, DispatchRays.index, DispatchRays.numthreads, new int3(manager.probeCounts, manager.pixelsPerProbe));
            
            //Add gutter
            command.SetComputeTextureParam(computeShader, AddGutter.index, "IrradianceBands", manager.irradianceBandBuffer.Current);
            command.DispatchCompute(computeShader, AddGutter.index, AddGutter.numthreads, new int3(manager.probeCounts, 1));

            //Set result as a global
            command.SetGlobalTexture("G_IrradianceBand", manager.irradianceBandBuffer.Current);
            
            context.ExecuteCommandBuffer(command);
            command.Clear();
            CommandBufferPool.Release(command);
            
            //Make sure everything has run up to this point
            GL.Flush();
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