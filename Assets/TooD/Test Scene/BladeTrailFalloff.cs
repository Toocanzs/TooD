using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BladeTrailFalloff : MonoBehaviour
{
    public AnimationCurve curve;

    private float width = 0;
    private float time = 0;

    public float trailWidth = 2f;

    public float widthFalloffSpeed = 10f;
    public float timeFalloffSpeed = 1f;
    void Start()
    {
        
    }
    
    void Update()
    {
        width = width + Time.deltaTime * widthFalloffSpeed;
        time += timeFalloffSpeed * Time.deltaTime;
        if(time > 1f)
            Destroy(gameObject);

        var lineRender = GetComponent<LineRenderer>();
        lineRender.startWidth = math.clamp(1.5f - width, 0, 1) * trailWidth;
        lineRender.endWidth = math.clamp(2.5f - width, 0, 1) * trailWidth;
    }
}
