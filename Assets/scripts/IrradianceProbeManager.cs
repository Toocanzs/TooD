using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class IrradianceProbeManager : MonoBehaviour
{
    public static IrradianceProbeManager Instance;
    
    [SerializeField]
    private int2 probeCounts = new int2(10, 10);
    [SerializeField]
    private float probeSeparation = 1f;
    [SerializeField]
    private float2 resetAreaPercent = new float2(0.5f,0.5f);
    [SerializeField]
    private Camera lightingCamera = null;
    private float2 OriginOffset => 0.5f * probeSeparation;

    [SerializeField]
    private int pixelsPerUnit = 32;

    private int2 BufferSize => math.int2(math.float2(probeCounts) * probeSeparation * pixelsPerUnit);

    public RenderTexture buffer;
    
    void Start()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        var size = BufferSize;
        buffer = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        buffer.wrapMode = TextureWrapMode.Clamp;
        buffer.Create();
    }

    private void OnDestroy()
    {
        buffer.Release();
    }

    void Update()
    {
        float2 center = GetCenter();
        float2 scale = GetWorldScale();
        float2 resetScale = scale * resetAreaPercent;
        float4 resetBounds = new float4(center - resetScale/2, center + resetScale/2);

        float2 cameraPos = math.float3(Camera.main.transform.position).xy;
        
        if (math.any(math.bool4(cameraPos.xy < resetBounds.xy, cameraPos.xy > resetBounds.zw)))
        {

            SetCenter(transform, cameraPos.xy);
            //TODO: Transfer valid probe data to new area
        }

        var transform1 = lightingCamera.transform;
        transform1.position = new float3(center, transform1.position.z);
        lightingCamera.orthographicSize = scale.y / 2;
        lightingCamera.aspect = scale.x / scale.y;
    }

    void SetCenter(Transform trs, float2 value)
    {
        float2 scale = GetWorldScale();
        trs.position = new float3(value - scale/2, 0);
    }

    float2 GetCenter()
    {
        float2 scale = GetWorldScale();
        float2 dims = GetProbeAreaDims().xy;
        return dims + scale / 2;
    }

    private float2 GetProbePosition(int2 probePos)
    {
        return GetProbeAreaOrigin() + OriginOffset + math.float2(probePos) * probeSeparation;
    }

    private float2 GetProbeAreaOrigin()
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
                var pos = GetProbePosition(new int2(x,y));
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
        Gizmos.DrawWireCube(new float3(center, 0), new float3(scale*resetAreaPercent, 0));
    }
}
