using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PlayerFollower : MonoBehaviour
{
    public float followSpeed = 4f;

    private float Z;

    private void Start()
    {
        Z = transform.position.z;
    }

    void LateUpdate()
    {
        float3 currentPos = transform.position;
        float3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float3 playerPos = Player.Instance.transform.position;
        mousePos.z = 0;
        playerPos.z = 0;
        float3 targetPos = math.lerp(playerPos, mousePos, new float3(0.5f, 0.35f, 0));
        targetPos.z = Z;
        transform.position = math.lerp(currentPos, targetPos, Time.deltaTime * followSpeed);
    }
}
