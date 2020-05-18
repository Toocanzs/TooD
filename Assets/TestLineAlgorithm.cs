using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class TestLineAlgorithm : MonoBehaviour
{
    public GameObject prefab;
    private List<GameObject> prefabs = new List<GameObject>();

    public float ang = 0;
    public float dist = 10f;
    void Update()
    {
        foreach (var prefab in prefabs)
        {
            Destroy(prefab);
        }

        float2 normalized = new float2(math.cos(ang), math.sin(ang));
        drawLine(float2.zero, normalized*dist);
    }
    
    void putPixel(float2 pos)
    {
        var go = Instantiate(prefab, new float3(pos, 0), quaternion.identity);
        prefabs.Add(go);
    }

    void drawLine(float2 start, float2 end) 
    {
        float w = end.x - start.x;
        float h = end.y - start.y;
    
        float2 d1 = math.float2(math.sign(w),math.sign(h));

        float2 d2 = math.float2(math.sign(w),0);

        float longest = math.abs(w);
        float shortest = math.abs(h);
        if (longest <= shortest) 
        {
            longest = math.abs(h);
            shortest = math.abs(w);
            d2.y = math.sign(h);
            d2.x = 0;
        }
        float numerator = longest / 2f;
        for (int i = 0; i <= (int)(math.ceil(longest)); i++) 
        {
            putPixel(start);
            numerator += shortest - (numerator >= longest ? longest : 0);
            start += numerator >= longest ? d1 : d2;
        }
    }
}
