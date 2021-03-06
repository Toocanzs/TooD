using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace TooD
{


    public class IrradianceProbeManager : MonoBehaviour
    {
        public static IrradianceProbeManager Instance;

        [Range(0, 10)] public float hysteresis = 1;

        [SerializeField] public int2 probeCounts = new int2(10, 10);

        [SerializeField] public float probeSeparation = 1f;

        public float2 resetAreaPercent = new float2(0.5f, 0.5f);

        public Camera lightingCamera = null;

        public float2 OriginOffset => 0.5f * probeSeparation;

        public int pixelsPerUnit = 32;

        public Material dataTransferMaterial = null;
        public Material fullScreenCopyMaterial = null;

        public int2 BufferSize => math.int2(math.float2(probeCounts) * probeSeparation * pixelsPerUnit);

        public int directionCount = 32;
        public const int GutterSize = 1; //each side
        public int SingleProbePixelWidth => (directionCount + GutterSize * 2);
        public int MaxRayLength = 512;

        //irradiance buffer is directionCount pixels wide, one for each direction,
        //then GutterSize pixels on each side so the side pixels can bilinearly sample across the seam
        public RenderTexture irradianceBuffer;
        public RenderTexture cosineWeightedIrradianceBuffer;
        public RenderTexture wallBuffer;
        public RenderTexture averageIrradiancePerProbeBuffer;
        public RenderTexture fullScreenAverageIrradianceBuffer;

        //public DoubleBuffer sdfBuffer;

        void Start()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            var size = BufferSize;
            wallBuffer = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.DefaultHDR,
                RenderTextureReadWrite.Linear);
            wallBuffer.wrapMode = TextureWrapMode.Clamp;
            wallBuffer.Create();

            cosineWeightedIrradianceBuffer = new RenderTexture(probeCounts.x * SingleProbePixelWidth, probeCounts.y,
                0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
            cosineWeightedIrradianceBuffer.enableRandomWrite = true;
            cosineWeightedIrradianceBuffer.Create();

            irradianceBuffer = new RenderTexture(cosineWeightedIrradianceBuffer);
            irradianceBuffer.enableRandomWrite = true;
            irradianceBuffer.Create();

            averageIrradiancePerProbeBuffer = new RenderTexture(probeCounts.x, probeCounts.y,
                0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
            averageIrradiancePerProbeBuffer.wrapMode = TextureWrapMode.Clamp;
            averageIrradiancePerProbeBuffer.enableRandomWrite = true;
            averageIrradiancePerProbeBuffer.Create();

            fullScreenAverageIrradianceBuffer = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.DefaultHDR,
                RenderTextureReadWrite.Linear);
            fullScreenAverageIrradianceBuffer.wrapMode = TextureWrapMode.Clamp;
            fullScreenAverageIrradianceBuffer.enableRandomWrite = true;
            fullScreenAverageIrradianceBuffer.Create();
        }

        private void OnDestroy()
        {
            wallBuffer.Release();
            irradianceBuffer.Release();
            averageIrradiancePerProbeBuffer.Release();
        }

        public void SetCenter(Transform trs, float2 value)
        {
            float2 scale = GetWorldScale();
            trs.position = new float3(value - scale / 2, 0);
        }

        public float2 GetCenter()
        {
            float2 scale = GetWorldScale();
            float2 dims = GetProbeAreaDims().xy;
            return dims + scale / 2;
        }

        private float2 GetProbeWorldPos(int2 probePos)
        {
            return GetProbeAreaOrigin() + OriginOffset + math.float2(probePos) * probeSeparation;
        }

        public float2 GetProbeAreaOrigin()
        {
            return math.round(math.float3(transform.position).xy / probeSeparation) * probeSeparation;
        }

        private float4 GetProbeAreaDims()
        {
            float2 origin = GetProbeAreaOrigin();
            return new float4(origin, origin + math.float2(probeCounts) * probeSeparation);
        }

        public float2 GetWorldScale()
        {
            var dims = GetProbeAreaDims();
            return new float2(dims.z - dims.x, dims.w - dims.y);
        }

        private void OnDrawGizmosSelected()
        {
            for (int y = 0; y < probeCounts.y; y++)
            {
                for (int x = 0; x < probeCounts.x; x++)
                {
                    var pos = GetProbeWorldPos(new int2(x, y));
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(new float3(pos.xy, 0), 0.1f);
                }
            }

            float2 scale = GetWorldScale();
            float2 dims = GetProbeAreaOrigin();
            float2 center = dims + scale / 2;

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(new float3(center, 0), new float3(scale, 0));
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new float3(center, 0), new float3(scale * resetAreaPercent, 0));
        }
    }
}