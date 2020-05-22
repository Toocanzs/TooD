using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class SDF_Raycast_test : MonoBehaviour
{
    public GameObject tile;
    private int count = 30;
    private GameObject[] gos;
    
    [Range(0,6.28f)]
    public float angle;
    void Start()
    {
        Debug.Log((uint2)new float2(-1,2));
        gos = new GameObject[count];
    }

    float map(float2 pos)
    {
        return math.distance(pos, new float2(10, 10)) - 5;
    }

    private void Update()
    {
        foreach (var go in gos)
        {
            if(go != null)
                Destroy(go);
        }
        float2 dir = math.normalize(new float2(math.cos(angle),math.sin(angle)));
        dir /= math.max(math.abs(dir.x), math.abs(dir.y));
        float2 xy = 0;
        int2 pos = 0;
        for (int i = 0; i < count; i++)
        {
            pos = (int2) xy;
            gos[i] = Instantiate(tile, transform.position + new Vector3(pos.x, pos.y, 0), Quaternion.identity);
            xy += dir * math.max(map(xy), 1);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawSphere(transform.position + new Vector3(10, 10, 0), 5);
    }
}
