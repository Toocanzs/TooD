using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class BasicMovement : MonoBehaviour
{
    public ParticleSystem testParticle;
    public GameObject sword;
    private bool rightSide = false;

    [SerializeField]
    private float speed = 5f;

    private float swordTime = 0f;

    public float swordHoldAngle = 25f;
    public float swordSwingSpeed = 15f;

    public AnimationCurve swordCurve;
    public float dashLength = 5f;

    public GameObject dashTrail;

    void Start()
    {
    }

    void Update()
    {
        float3 movement = new float3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0);
        transform.Translate(movement * speed * Time.deltaTime);
        float a = Input.GetAxis("Fire2");
        var emis = testParticle.emission;
        var main = testParticle.main;
        main.startColor = new ParticleSystem.MinMaxGradient
        {
            color = Color.HSVToRGB((Time.time * 0.5f) % 1, 1, 1)
        };
        float2 forward = ((float3) Camera.main.ScreenToWorldPoint(Input.mousePosition)).xy - ((float3) transform.position).xy;
        forward = math.normalizesafe(forward);
        float2 right = new float2(-forward.y, forward.x);


        if (Input.GetButtonDown("Fire3"))
        {
            float3 newPos = (float3) transform.position + math.float3(forward, 0) * dashLength;
            var trailGo = Instantiate(dashTrail, transform.position, quaternion.identity);
            var lineRenderer = trailGo.GetComponent<LineRenderer>();
            int numPositions = 20;
            lineRenderer.positionCount = numPositions;
            float radius = 1.5f;
            float3 basePos = math.float3((rightSide ? -1 : 1) * right * radius, 0);
            Vector3[] positions = new Vector3[numPositions];
            positions[0] = (float3)transform.position + basePos;
            positions[1] = newPos + basePos;
            for (int i = 2; i < numPositions; i++)
            {
                positions[i] = math.mul(Quaternion.Euler(0, 0, (rightSide ? 1 : -1) * ((float)(i-2)/(numPositions - 3)) * 160), basePos) + newPos;
            }
            lineRenderer.SetPositions(positions);
            transform.position = newPos;
            rightSide = !rightSide;
            swordTime = math.clamp(swordTime + 999 * (rightSide ? -1 : 1), 0, 1);//switch sides 
        }

        


        bool notSwinging = math.abs(-1f + 2f * swordTime) > 0.99f;
        if (Input.GetButtonDown("Fire1") && notSwinging)
        {
            rightSide = !rightSide;
        }

        float ang = math.degrees(math.atan2(forward.y, forward.x));
        quaternion rot = Quaternion.Euler(0f, 0f, ang - testParticle.shape.arc / 2);
        testParticle.gameObject.transform.rotation = rot;

        swordTime = math.clamp(swordTime + Time.deltaTime * swordSwingSpeed * (rightSide ? -1 : 1), 0, 1);

        sword.transform.rotation = Quaternion.Euler(0f, 0f, ang - 90f + swordCurve.Evaluate(swordTime) * swordHoldAngle);

        if (a * a > 0.01f)
        {
            emis.enabled = true;
        }
        else
        {
            emis.enabled = false;
        }
    }
}