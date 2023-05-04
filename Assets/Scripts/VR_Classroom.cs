using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;   // XR support
using UnityEngine.Assertions;

public class VR_Classroom : MonoBehaviour
{
    // *********** public:
    public GameObject CamRig;
    public GameObject chairPrefab, deskPrefab;  // prefab: dynamically instantiate
    public GameObject room, ground, tv; // static obj
    public Camera eyeCamera;
    public LineRenderer rRayRenderer;
    public GameObject chessPrefab;    // To indicate the teleport destination

    // *********** private:
    private GameObject chess;
    private GameObject chair, desk;
    private bool spawnChair = true;
    private bool useVR;
    private float step;
    // raycasting stuff
    private Ray rRay;
    private Ray rPrev;
    #nullable enable
    // the currently selected object (if any)
    private GameObject? selected;
    private float distance;

    private Vector3 accXf = new Vector3(0.0f, 0.0f, 0.0f);
    private Vector3 accRot = new Vector3(0.0f, 0.0f, 0.0f);

    // change selected (a table or chair) to this half-transparent red
    private Color colorSelected = new Color(1f, 0f, 0f, 0.5f);
    // need to remember original to restore later
    private List<Material> origMaterials = new List<Material>();
    #nullable disable

    // *********** constants
    private const float sensitivity = 1.0f;
    // max distance rRay can go
    private const float maxDistance = 10.0f;
    // where we grab a point along the rRay to update orientation
    private const float focusDistance = 3.0f;
    private const float eyeY = 1.5f;
    private const float chessY = 0.2f;

    // Start is called before the first frame update
    void Start()
    {
        // Determine VR availability
        useVR = XRSettings.isDeviceActive;
        Debug.Log(string.Format("VR device (headset + controller) is detected: {0}", useVR));
        rRayRenderer = GetComponent<LineRenderer>();

        chess = Instantiate(chessPrefab);
        chess.SetActive(false);  // invisible by default

        selected = null;
    }

    // Update is called once per frame
    void Update()
    {
        // Define step value for animation
        step = 5.0f * Time.deltaTime;

        // updateRightRay(); // we do this in FixedUpdate now
        updateLookat();
        moveAround();
        teleport();
        
        // We do controller processing and object manip here
        // Changes are accumulated, then applied in FixedUpdate()

        // First, we update the right controller ray using the debug fn defined below
        updateRightRay();

        // manip object
        if (useVR) {
            manipulateObjectVR();
        } else {
            manipulateObjectDebug();
        }
    }

    // helper function: update camera orientation (forward/lookat direction)
    void updateLookat() {
        // Oculus SDK will auto update headset camera
        // UPDATE: not allowed to use headset camera to look around
        if (useVR) {
            // update lookat to if user press down left-hand X button
            if (OVRInput.GetDown(OVRInput.Button.Three)) {
                // grab a focus point
                Vector3 focusPt = rRay.GetPoint(focusDistance);
                // update forward direction
                CamRig.transform.forward =  Vector3.Normalize(focusPt - CamRig.transform.position);
            }
            return;
        }

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
        CamRig.transform.localEulerAngles = new Vector3(rotationX, rotationY, 0f);

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

        CamRig.transform.position += deltaPos;
    }


    // helper function: shoot a Ray from right-hand (Secondary) controller
    // it updates rControllerRay
    // Only available in VR mode
    void updateRightRay() {
        rPrev = new Ray(rRay.origin, rRay.direction);

        if (useVR) {
            // Get controller position and rotation
            Vector3 controllerPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
            Quaternion controllerRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

            // Calculate ray direction
            Vector3 rayDirection = controllerRotation * Vector3.forward;

            // Update the global ray's position and direction
            // rRay.origin = eyeCamera.transform.position + new Vector3(0.25f, -0.25f, 0.25f);
            rRay.origin = eyeCamera.transform.position + controllerPosition;
            rRay.direction = rayDirection;
        } else {
            rRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        }

        // Set the line renderer's positions to match the ray
        rRayRenderer.SetPosition(0, rRay.origin);
        rRayRenderer.SetPosition(1, rRay.origin + rRay.direction * maxDistance);
    }

    void manipulateObjectDebug() {
        if (useVR) {return;}

        RaycastHit hit;
        if (Physics.Raycast(rRay, out hit, maxDistance)) {
            selected = hit.collider.gameObject;
            distance = hit.distance;

            selected.GetComponent<Highlight>()?.ToggleHighlight(true);
        } 
    }

    // Helper function to handle object spawning and manipulation.
    // Left: primary; Right: secondary
    // To manipulate an object, a user points at an object and holds the left trigger;
    // a ray coming from the controller will "skewer" the object and manipulate it
    // that way
    // To spawn an object, a user holds the left trigger, then pushes the right trigger.
    // This will spawn an object and allow for manipulation of the new object so long
    // as the right trigger is being held.
    void manipulateObjectVR() {
        if (!useVR) {return;}

        // Switch which object we spawn
        if (OVRInput.GetDown(OVRInput.Button.Two)) {
            spawnChair = !spawnChair;
        }

        // Do object spawning test
        if (OVRInput.GetDown(OVRInput.Button.One)) {
            if (spawnChair) {
                Instantiate(chairPrefab, rRay.GetPoint(focusDistance), Quaternion.Euler(-90.0f, 0.0f, 0.0f));
            } else {
                Instantiate(deskPrefab, rRay.GetPoint(focusDistance), Quaternion.Euler(-90.0f, 0.0f, 90.0f));
            }
        }

        // Next, do manip test
        RaycastHit hit;
        if (Physics.Raycast(rRay, out hit, maxDistance) && OVRInput.Get(OVRInput.RawButton.RIndexTrigger)) {
            selected = hit.collider.gameObject;
            distance = hit.distance;

            selected.transform.position = rRay.GetPoint(focusDistance);
            selected.GetComponent<Highlight>()?.ToggleHighlight(true);

            // Rotation check
            if (OVRInput.Get(OVRInput.RawButton.LIndexTrigger)) {
                hit.transform.Rotate(0, 0, 90.0f * Time.deltaTime);
            }

            if (OVRInput.Get(OVRInput.RawButton.RHandTrigger)) {
                // Scale up check
                float scaleFactor = 1.0f + 0.5f * Time.deltaTime;
                hit.transform.localScale = new Vector3(hit.transform.localScale.x * scaleFactor, hit.transform.localScale.y * scaleFactor, hit.transform.localScale.z * scaleFactor);
            } else if (OVRInput.Get(OVRInput.RawButton.LHandTrigger)) {
                // Scale down check
                float scaleFactor = 1.0f - 0.5f * Time.deltaTime;
                hit.transform.localScale = new Vector3(hit.transform.localScale.x * scaleFactor, hit.transform.localScale.y * scaleFactor, hit.transform.localScale.z * scaleFactor);
            }
        } else {
            if (selected != null) {
                // restore original materials
                selected.GetComponent<Highlight>()?.ToggleHighlight(false);
            }

            selected = null;
        }
    }

    // movement is limited to button; no joystick allowed
    void teleport() {
        if (!useVR) {return;}

        // Perform raycast
        RaycastHit hit;
        if (Physics.Raycast(rRay, out hit, maxDistance))
        {
            // Debug log information about the hit object
            Debug.Log("Hit object: " + hit.collider.gameObject.name);
            Debug.Log("Hit point: " + hit.point);
            Debug.Log("Hit normal: " + hit.normal);

            if (true/* Mathf.Abs(hit.point.y) < 1e-2 */) { // close to ground, can teleport
                // draw a chess to indicate teleport destination
                if (OVRInput.GetDown(OVRInput.Button.Four)) {
                    chess.transform.position = new Vector3(hit.point.x, chessY, hit.point.z);
                    chess.transform.rotation = Quaternion.identity;
                    chess.SetActive(true);
                }
                // teleport if user release left-hand Y button
                if (OVRInput.GetUp(OVRInput.Button.Four)) {
                    CamRig.transform.position = new Vector3(hit.point.x, eyeY, hit.point.z);
                    // hide chess indicator after transform
                    chess.SetActive(false);
                }
            }
        }
        return;
    }
    
}
