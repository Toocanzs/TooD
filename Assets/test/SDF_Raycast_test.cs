using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class SDF_Raycast_test : MonoBehaviour
{
    public GameObject tile;
    void Start()
    {
        float2 dir = math.normalize(new float2(1,1));
        dir *= math.max(math.abs(dir.x), math.abs(dir.y));
        int count = 0;
        float2 xy = 0;
        uint2 pos = 0;
        while (count < 30)
        {
            xy += dir;
            if (pos.x != (uint)xy.x)
            {
                pos.x += 1;
                Instantiate(tile, transform.position + new Vector3(pos.x, pos.y, 0), Quaternion.identity);
            }
            if (pos.y != (uint)xy.y)
            {
                pos.y += 1;
                Instantiate(tile, transform.position + new Vector3(pos.x, pos.y, 0), Quaternion.identity);
            }

            count++;
        }
    }
}
