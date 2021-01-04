using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class RaycastTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    struct Box
    {
        public float2 min;
        public float2 max;
    }

    struct Ray
    {
        public float2 origin;
        public float2 direction;
        public float2 n_inv => 1f / direction;
    }
    
    (float, float) intersection(Box b, Ray r) {
        float tx1 = (b.min.x - r.origin.x)*r.n_inv.x;
        float tx2 = (b.max.x - r.origin.x)*r.n_inv.x;

        float tmin = math.min(tx1, tx2);
        float tmax = math.max(tx1, tx2);

        float ty1 = (b.min.y - r.origin.y)*r.n_inv.y;
        float ty2 = (b.max.y - r.origin.y)*r.n_inv.y;

        tmin = math.max(tmin, math.min(ty1, ty2));
        tmax = math.min(tmax, math.max(ty1, ty2));

        return (tmin, tmax);
    }

    private void OnDrawGizmos()
    {
        Box box = new Box
        {
            min = float2.zero, max = new float2(4,5)
        };
        Ray ray = new Ray{origin = new float2(math.cos(Time.time*1.3423567f) + 2,math.sin(Time.time*1.23456f) + 2), direction = new float2(math.cos(Time.time), math.sin(Time.time))};
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new float3((box.min + box.max)/2f, 0), new float3(box.max - box.min, 1));
        Gizmos.color = Color.blue;

        (float tmin, float tmax) = intersection(box, ray);
        Gizmos.DrawLine(new float3(ray.origin, 0), new float3(ray.origin + ray.direction * tmax, 0));
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new float3(ray.origin, 0), new float3(ray.origin + ray.direction * tmin, 0));
        //Gizmos.DrawRay(new float3(ray.origin, 0), new float3(ray.direction, 0));
    }
}
