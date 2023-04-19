using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;   // XR support

public class VR_Classroom : MonoBehaviour
{
    
    public GameObject chair, desk;  // prefab: dynamically instantiate
    public GameObject room, ground, tv; // static obj

    new Camera camera;
    bool useVR;
    Ray ray;
    Color origColor;


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
