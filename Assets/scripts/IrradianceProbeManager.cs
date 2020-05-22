using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class IrradianceProbeManager : MonoBehaviour
{
    public static IrradianceProbeManager Instance;
    
    [Range(0,10)]
    public float hysteresis = 1;

    [SerializeField]
    public int2 probeCounts = new int2(10, 10);

    [SerializeField]
    public float probeSeparation = 1f;

    [SerializeField]
    private float2 resetAreaPercent = new float2(0.5f, 0.5f);

    [SerializeField]
    private Camera lightingCamera = null;

    public float2 OriginOffset => 0.5f * probeSeparation;
    
    public int pixelsPerUnit = 32;

    [SerializeField]
    private Material dataTransferMaterial = null;

    public int2 BufferSize => math.int2(math.float2(probeCounts) * probeSeparation * pixelsPerUnit);
    

    public RenderTexture wallBuffer;

    public int directionCount = 32;

    //irradiance buffer is directionCount pixels wide, one for each direction,
    //then GutterSize pixels on each side so the side pixels can bilinearly sample across the seam
    public DoubleBuffer irradianceBuffer;
    public const int GutterSize = 1; //each side

    public DoubleBuffer averageIrradianceBuffer;

    private int SingleProbePixelWidth => (directionCount + GutterSize * 2);

    public float4x4 worldToWallBuffer;
    public float4x4 worldDirectionToBufferDirection;
    private static readonly int ProbeAreaOriginId = Shader.PropertyToID("_ProbeAreaOrigin");
    private static readonly int ProbeSeparationId = Shader.PropertyToID("_ProbeSeparation");
    private static readonly int WorldDirectionToBufferDirectionId = Shader.PropertyToID("worldDirectionToBufferDirection");
    private static readonly int IrradianceBufferId = Shader.PropertyToID("IrradianceBuffer");
    private static readonly int DirectionCountId = Shader.PropertyToID("directionCount");
    private static readonly int GutterSizeID = Shader.PropertyToID("gutterSize");
    private static readonly int AverageIrradienceBufferId = Shader.PropertyToID("_AverageIrradienceBuffer");
    private static readonly int ProbeCountsId = Shader.PropertyToID("ProbeCounts");
    private static readonly int OffsetID = Shader.PropertyToID("_Offset");

    public int MaxRayLength => 250;//TODO: When we change to sdf usage, switch this out
    
    public DoubleBuffer sdfBuffer;
    void Start()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        var size = BufferSize;
        wallBuffer = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
        wallBuffer.wrapMode = TextureWrapMode.Clamp;
        wallBuffer.Create();

        irradianceBuffer = new RenderTexture(probeCounts.x * SingleProbePixelWidth, probeCounts.y,
                0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear)
            .ToDoubleBuffer();
        irradianceBuffer.enableRandomWrite = true;
        irradianceBuffer.Create();

        averageIrradianceBuffer = new RenderTexture(probeCounts.x, probeCounts.y,
                0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear)
            .ToDoubleBuffer();
        averageIrradianceBuffer.enableRandomWrite = true;
        averageIrradianceBuffer.Create();
        
        sdfBuffer = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear).ToDoubleBuffer();
        sdfBuffer.enableRandomWrite = true;
        sdfBuffer.wrapMode = TextureWrapMode.Clamp;
        sdfBuffer.Create();

        transform.GetChild(0).GetComponent<MeshRenderer>().material.mainTexture = sdfBuffer.Current;
        //TODO: Maybe have a color buffer for diffuse walls
        //TODO: Then multiply bounce color by that diffuse color
    }

    private void OnDestroy()
    {
        wallBuffer.Release();
        irradianceBuffer.Release();
        averageIrradianceBuffer.Release();
        sdfBuffer.Release();
    }

    void Update()
    {
        float2 center = GetCenter();
        float2 scale = GetWorldScale();
        float2 resetScale = scale * resetAreaPercent;
        float4 resetBounds = new float4(center - resetScale / 2, center + resetScale / 2);

        float2 cameraPos = math.float3(Camera.main.transform.position).xy;

        float2 oldPos = GetProbeAreaOrigin();
        bool moved = false;
        if (math.any(math.bool4(cameraPos.xy < resetBounds.xy, cameraPos.xy > resetBounds.zw)))
        {
            SetCenter(transform, cameraPos.xy);
            moved = true;
        }

        var transform1 = lightingCamera.transform;
        transform1.position = new float3(center, transform1.position.z);
        lightingCamera.orthographicSize = scale.y / 2;
        lightingCamera.aspect = scale.x / scale.y;

        float4 dims = GetProbeAreaDims();
        float3 pos = new float3(dims.xy, 0);
        //Translate so origin is at probe origin
        float4x4 step1 = float4x4.Translate(-pos);
        //Scale afterwards to buffersize
        float4x4 step2 = float4x4.Scale(pixelsPerUnit);
        worldToWallBuffer = math.mul(step2, step1);

        float2 bufferSize = BufferSize;
        worldDirectionToBufferDirection = float4x4.Scale(1f / bufferSize.y, 1f / bufferSize.x, 0);

        Shader.SetGlobalVector(ProbeAreaOriginId, GetProbeAreaOrigin().xyxy);
        Shader.SetGlobalFloat(ProbeSeparationId, probeSeparation);
        Shader.SetGlobalMatrix(WorldDirectionToBufferDirectionId, worldDirectionToBufferDirection);
        Shader.SetGlobalTexture(AverageIrradienceBufferId, averageIrradianceBuffer);
        Shader.SetGlobalInt(DirectionCountId, directionCount);
        Shader.SetGlobalInt(GutterSizeID, GutterSize);

        Shader.SetGlobalVector(ProbeCountsId, (float4) probeCounts.xyxy);

        if (moved)
        {
            float2 probeOffset = (GetProbeAreaOrigin() - oldPos).xy  / probeSeparation;
            float2 pixelOffset = probeOffset * new float2(directionCount + GutterSize * 2, 1);
            float2 uvOffset = pixelOffset / irradianceBuffer.Dimensions;
            dataTransferMaterial.SetVector(OffsetID, uvOffset.xyxy);
            Graphics.Blit(irradianceBuffer.Current, irradianceBuffer.Other, dataTransferMaterial);
            irradianceBuffer.Swap();
        }
    }

    void SetCenter(Transform trs, float2 value)
    {
        float2 scale = GetWorldScale();
        trs.position = new float3(value - scale / 2, 0);
    }

    float2 GetCenter()
    {
        float2 scale = GetWorldScale();
        float2 dims = GetProbeAreaDims().xy;
        return dims + scale / 2;
    }

    private float2 GetProbeWorldPos(int2 probePos)
    {
        return GetProbeAreaOrigin() + OriginOffset + math.float2(probePos) * probeSeparation;
    }

    private int2 GetNearestProbe(float2 worldPos)
    {
        return (int2) math.floor((worldPos - GetProbeAreaOrigin() - OriginOffset) / probeSeparation);
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

    private float2 GetWorldScale()
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
        float2 dims = GetProbeAreaDims().xy;
        float2 center = dims + scale / 2;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(new float3(center, 0), new float3(scale, 0));
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new float3(center, 0), new float3(scale * resetAreaPercent, 0));
    }
}