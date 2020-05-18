using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BasicMovement : MonoBehaviour
{
    [SerializeField]
    private float speed = 5f;
    void Start()
    {
        
    }
    
    void Update()
    {
        float3 movement = new float3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0);
        transform.Translate(movement * speed * Time.deltaTime);
    }
}
