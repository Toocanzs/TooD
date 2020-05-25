using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class BasicMovement : MonoBehaviour
{
    public ParticleSystem testParticle;
    [SerializeField]
    private float speed = 5f;
    void Start()
    {
        
    }
    
    void Update()
    {
        float3 movement = new float3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0);
        transform.Translate(movement * speed * Time.deltaTime);
        float a = Input.GetAxis("Fire1");
        var emis = testParticle.emission;
        var main = testParticle.main;
        main.startColor = new ParticleSystem.MinMaxGradient
        {
            color = Color.HSVToRGB((Time.time*0.5f)%1, 1, 1)
        };
        if (a*a > 0.01f)
        {
            float3 diff = math.normalize(Camera.main.ScreenToWorldPoint(Input.mousePosition) - transform.position);
            float rot = math.degrees(math.atan2(diff.y, diff.x));
            testParticle.gameObject.transform.rotation = Quaternion.Euler(0f, 0f, rot - testParticle.shape.arc/2);
            emis.enabled = true;
        }
        else
        {
            emis.enabled = false;
        }
    }
}
