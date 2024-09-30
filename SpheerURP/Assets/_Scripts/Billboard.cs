using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform cam;

    void Awake()
    {
        //find camera
        cam = GameObject.FindWithTag("MainCamera").transform;
    }


    void FixedUpdate()
    {
        transform.LookAt(transform.position + cam.forward);
    }
}
