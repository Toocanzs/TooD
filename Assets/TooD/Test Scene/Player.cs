using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance;
    void Start()
    {
        if (Instance != null)
        {
            Destroy(this);
            Debug.LogError("2 Players Exist in the scene");
            return;
        }

        Instance = this;
        
    }
}
