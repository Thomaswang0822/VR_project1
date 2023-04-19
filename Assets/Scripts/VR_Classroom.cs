using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;   // XR support

public class VR_Classroom : MonoBehaviour
{
    // public:
    public GameObject chair, desk;  // prefab: dynamically instantiate
    public GameObject room, ground, tv; // static obj
    public Camera eyeCamera;
    
    // private:
    private bool useVR;
    private float step;


    // Start is called before the first frame update
    void Start()
    {
        // Determine VR availability
        useVR = XRSettings.isDeviceActive;
        Debug.Log(string.Format("VR device (headset + controller) is detected: {0}", useVR));
    }

    // Update is called once per frame
    void Update()
    {
        // Define step value for animation
        step = 5.0f * Time.deltaTime;

        MoveAround();
    }

    // helper function: move player (camera) around
    void MoveAround() {
        // experiment: use joystick on left controller to control movement
        if (useVR) {
            // returns a Vector2 of the primary (Left) thumbstick’s current state.
            // (X/Y range of -1.0f to 1.0f)
            Vector2 deltaPos = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

            eyeCamera.transform.position += new Vector3(deltaPos.x, 0, deltaPos.y) * step;

        } else { // use arrow keys
            // Get the horizontal and vertical axis input
            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");

            eyeCamera.transform.position += new Vector3(horizontalInput, 0, verticalInput) * step;
        }
    }
}
