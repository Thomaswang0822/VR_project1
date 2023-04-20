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
    public LineRenderer rRayRenderer;
    
    // private:
    private bool useVR;
    private float step;
    private float sensitivity = 1.0f;
    private float maxDistance = 5.0f;
    private Ray rControllerRay;


    // Start is called before the first frame update
    void Start()
    {
        // Determine VR availability
        useVR = XRSettings.isDeviceActive;
        Debug.Log(string.Format("VR device (headset + controller) is detected: {0}", useVR));
        rRayRenderer = GetComponent<LineRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        // Define step value for animation
        step = 5.0f * Time.deltaTime;

        updateLookat();
        moveAround();
    }


    // helper function: update camera orientation (forward/lookat direction)
    void updateLookat() {
        // Oculus SDK will auto update headset camera
        if (useVR) {return;}

        // Get the horizontal and vertical axis input from the mouse cursor
        float horizontalInput = Input.GetAxis("Mouse X");
        float verticalInput = Input.GetAxis("Mouse Y");

        // Calculate the rotation angles based on the input
        float rotationY = eyeCamera.transform.localEulerAngles.y + horizontalInput * sensitivity;
        float rotationX = eyeCamera.transform.localEulerAngles.x - verticalInput * sensitivity;
        // limit rotationX from going over 360 to avoid wrap-around when clamp
        if (rotationX > 180f) {
            rotationX -= 360f;
        }
        // limit look-up and look-down to be more realistic
        rotationX = Mathf.Clamp(rotationX, -85f, 85f);

        // Rotate the camera based on the new angles
        eyeCamera.transform.localEulerAngles = new Vector3(rotationX, rotationY, 0f);

        // rRayRenderer.SetPosition(1, eyeCamera.transform.position + eyeCamera.transform.forward * maxDistance);
    }

    // helper function: move player (camera) around
    void moveAround() {
        Vector3 forward = eyeCamera.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = Quaternion.Euler(new Vector3(0, 90, 0)) * forward;
        Vector3 deltaPos;

        // Grab user input, which is position change in local space
        if (useVR) {
            // returns a Vector2 of the primary (Left) thumbstick’s current state.
            // (X/Y range of -1.0f to 1.0f)
            Vector2 deltaXY = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

            deltaPos = (forward * deltaXY.y + right * deltaXY.x) * step;

        } else { // use arrow keys
            // Get the horizontal and vertical axis input
            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");

            deltaPos = (forward * verticalInput + right * horizontalInput) * step;
        }

        eyeCamera.transform.position += deltaPos;
    }


    // helper function: shoot a Ray from right-hand (Secondary) controller
    // it updates rControllerRay
    // Only available in VR mode
    void updateRightRay() {
        if (!useVR) {return;}

        // Get controller position and rotation
        Vector3 controllerPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        Quaternion controllerRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

        // Calculate ray direction
        Vector3 rayDirection = controllerRotation * Vector3.forward;

        // Update the global ray's position and direction
        rControllerRay.origin = controllerPosition;
        rControllerRay.direction = rayDirection;

        // Set the line renderer's positions to match the ray
        rRayRenderer.SetPosition(0, rControllerRay.origin);
        rRayRenderer.SetPosition(1, rControllerRay.origin + rControllerRay.direction * maxDistance);
    }
    
}
