using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;   // XR support

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
        manipulateObject();
    }

    // TODO: Maybe we move the rRay stuff to Update and accumulate movement/rotation
    //       forces that apply at FixedUpdate.
    //       Debug once we can run on a headset
    // FixedUpdate is called once per physics tick
    // We need to do two things each tick: update our controller ray, and use this info
    // for object manipulation
    void FixedUpdate() {
        // if (selected != null) {
        //     // selected.transform.position = rRay.GetPoint(focusDistance);
        //     Rigidbody rb = selected.GetComponent<Rigidbody>();
        //     if (rb != null) {
        //         // rb.MovePosition(selected.transform.position + accXf * Time.deltaTime);
        //         rb.MovePosition(rRay.GetPoint(focusDistance));
        //         // rb.MoveRotation(Quaternion.Euler(accRot.x, accRot.y, accRot.z));

        //         accXf = new Vector3(0.0f, 0.0f, 0.0f);
        //         accRot = new Vector3(0.0f, 0.0f, 0.0f);
        //     }
        // }
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
        if (!useVR) {return;}

        rPrev = new Ray(rRay.origin, rRay.direction);

        // Get controller position and rotation
        Vector3 controllerPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        Quaternion controllerRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

        // Calculate ray direction
        Vector3 rayDirection = controllerRotation * Vector3.forward;

        // Update the global ray's position and direction
        // rRay.origin = eyeCamera.transform.position + new Vector3(0.25f, -0.25f, 0.25f);
        rRay.origin = eyeCamera.transform.position + controllerPosition;
        rRay.direction = rayDirection;

        // Set the line renderer's positions to match the ray
        rRayRenderer.SetPosition(0, rRay.origin);
        rRayRenderer.SetPosition(1, rRay.origin + rRay.direction * maxDistance);
    }

    void OnGUI()
    {
        
        GUI.Label(new Rect(10, 10, 100, 20), "Hello World! " + selected);
    }

    // Helper function to handle object spawning and manipulation.
    // Left: primary; Right: secondary
    // To manipulate an object, a user points at an object and holds the left trigger;
    // a ray coming from the controller will "skewer" the object and manipulate it
    // that way
    // To spawn an object, a user holds the left trigger, then pushes the right trigger.
    // This will spawn an object and allow for manipulation of the new object so long
    // as the right trigger is being held.
    void manipulateObject() {
        if (!useVR) {return;}

        // Switch which object we spawn
        if (OVRInput.GetDown(OVRInput.Button.One)) {
            spawnChair = !spawnChair;
        }

        // Do object spawning test
        if (OVRInput.GetDown(OVRInput.RawButton.LIndexTrigger)) {
            if (spawnChair) {
                Instantiate(chairPrefab, rRay.GetPoint(focusDistance), Quaternion.identity);
            } else {
                Instantiate(tv, rRay.GetPoint(focusDistance), Quaternion.identity);
            }
        }

        // Next, do manip test
        RaycastHit hit;
        if (Physics.Raycast(rRay, out hit, maxDistance) && OVRInput.Get(OVRInput.RawButton.RIndexTrigger)) {
            selected = hit.collider.gameObject;
            distance = hit.distance;

            // Change its color and opacity
            // happens once right after you make a new selection
            if (!origMaterials.Any()) {
                Material[] ogs = selected.GetComponent<Renderer>().materials;
                Material[] news = new Material[ogs.Length];
                for (int i = 0; i < ogs.Length; i++) {
                    // push original material
                    origMaterials.Add(new Material(ogs[i]));
                    // half-transparent red
                    news[i] = ogs[i];
                    news[i].color = colorSelected;
                }

                // then, assign the whole array
                selected.GetComponent<Renderer>().materials = news;
            }

            // // We now need to move the object.
            // Rigidbody rb = selected.GetComponent<Rigidbody>();
            // if (rb != null && hit.collider.gameObject.CompareTag("Movable")) {
            //     // Move!
            //     Vector3 diff = rRay.GetPoint(distance) - rPrev.GetPoint(distance);
            //     // Velocity vector is new ray at distance - old ray at distance
            //     accXf += diff;
            //     // For angle, we first calculate the straight up angle between the two rays
            //     // (we negate since we want to rotate in opposite direction)
            //     float angle = -Vector3.Angle(rPrev.GetPoint(distance), rRay.GetPoint(distance));
            //     // The angular velocity is this angle multiplied by the normalized version of our
            //     // diff vector (the idea being if we move one unit purely in one axis, that
            //     // axis gets all the angular velocity)
            //     accRot += angle * Vector3.Normalize(diff);

            //     // FIXME: debug
            //     selected.transform.position = rRay.GetPoint(distance);
            // }

            if (hit.collider.gameObject.CompareTag("Movable")) {
                selected.transform.position = rRay.GetPoint(focusDistance);
            }
        } else {
            if (selected != null) {
                // restore original materials
                Material[] ogs = new Material[origMaterials.Count];
                for (int i = 0; i < origMaterials.Count; i++) {
                    ogs[i] = origMaterials[i];
                }
                selected.GetComponent<Renderer>().materials = ogs;
                origMaterials.Clear();

                // Rigidbody rb = selected.GetComponent<Rigidbody>();
                // if (rb != null) {
                //     rb.isKinematic = false;
                // }
            }

            selected = null;
        }

        // If the trigger is being pressed and we hit something...
        // RaycastHit hit;
        // if (OVRInput.GetDown(OVRInput.Button.One) && Physics.Raycast(rRay, out hit, maxDistance)) {
        //     // We're pointing at an object. There are two possibilities.

        //     if (selected == null) {
        //         // 1) We haven't selected an object right now
        //         // In this case, we check if the secondary trigger is being pressed
        //         // If it is, we spawn a new object at a set distance and set that as the
        //         // selected object.
        //         // If it isn't, just do the hit detection as before.
        //         if (OVRInput.GetDown(OVRInput.Button.Two)) {
        //             // TODO: allow user to choose what to spawn. We need to be able to choose b/w two objs
        //             // We spawn the object focusDistance units away from the ray with an identity rotation.
        //             selected = Object.Instantiate(chessPrefab, rRay.GetPoint(focusDistance), Quaternion.identity);
        //             distance = focusDistance;
        //         } else {
        //             // Our goal is to skewer the object. In other words, the object
        //             // should maintain the same distance and orientation relative
        //             // to the ray.
        //             selected = hit.collider.gameObject;
        //             distance = hit.distance;
        //         }

        //         // TODO: set color
        //         // (part 5 of the reqs goes here)
        //     } else {
        //         // 2) We have (i.e. currently moving an object)
        //         // We assume that the hit GameObject is the same as selected.
        //         // (If this isn't true something horrible has broken)

        //         // Change its color and opacity
        //         // happens once right after you make a new selection
        //         if (origMaterial == null) {
        //             Material selectedMaterial = selected.GetComponent<Renderer>().material;
        //             // store a copy
        //             origMaterial = new Material(selectedMaterial);
        //             // half-transparent red
        //             selectedMaterial.color = colorSelected;
        //         }
                

        //         // We now need to move the object.
        //         Rigidbody rb = selected.GetComponent<Rigidbody>();
        //         if (rb != null) {
        //             rb.isKinematic = true;

        //             // Move!
        //             Vector3 diff = rRay.GetPoint(distance) - rPrev.GetPoint(distance);
        //             // Velocity vector is new ray at distance - old ray at distance
        //             rb.MovePosition(diff);
        //             // For angle, we first calculate the straight up angle between the two rays
        //             // (we negate since we want to rotate in opposite direction)
        //             float angle = -Vector3.Angle(rPrev.GetPoint(distance), rRay.GetPoint(distance));
        //             // The angular velocity is this angle multiplied by the normalized version of our
        //             // diff vector (the idea being if we move one unit purely in one axis, that
        //             // axis gets all the angular velocity)
        //             Vector3 val = angle * Vector3.Normalize(diff);
        //             rb.MoveRotation(Quaternion.Euler(val.x, val.y, val.z));
        //         }
        //     }
        // } else {
        //     if (selected != null) {
        //         // Disable isKinematic on the selected object
        //         Rigidbody rb = selected.GetComponent<Rigidbody>();
        //         if (rb != null) {
        //             rb.isKinematic = false;
        //         }

        //         // restore original material
        //         selected.GetComponent<Renderer>().material = origMaterial;
        //         origMaterial = null;
        //     }
        //     selected = null;
        // }
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
