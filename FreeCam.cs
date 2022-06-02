using System;
using UnityEngine;
using UnityEngine.InputSystem;

// - uses NEW input system (Requires Input Action Asset) -> download here if you don't have it:

/// original: https://gist.github.com/ashleydavis/f025c03a9221bc840a2b
// Modified by: - Remi Verlinden
// additions:
// - in & out easing
// - continuous acceleration
// - vertical angle clamping
// - simple roll + reset with [ZXC] keys

// A simple free camera to be added to a Unity game object which uses new input system.
// 
// Keys:
//	wasd / arrows	- movement
//	q/e 			- up/down (local space)
//	r/f 			- up/down (world space)
//	pageup/pagedown	- up/down (world space)
//	hold shift		- enable fast movement mode
//	right mouse  	- enable free look
//	mouse			- free look / rotation
//  x/c             - roll left / right
//  z               - reset roll to default
//     

[RequireComponent(typeof(PlayerInput))]
public class FreeCam : MonoBehaviour
{

    //<<<-------------------- Start of public editor settings ----------------------->>>
    [Header("Vertical Rotation Settings")]
    [Tooltip("Don't allow the camera to vertically rotate beyond min and max angles.")]
    [SerializeField] private bool clampVerticalRotation = false;

    [Range(0f, -90f)]
    [Tooltip("Minimum vertical angle in degrees.")]
    [SerializeField] private float minAngle = -90f;
    [Range(0f, 90f)]
    [Tooltip("Maximum vertical angle in degrees.")]
    [SerializeField] private float maxAngle = 90f;

    [Header("Movement Settings")]
    [Tooltip("Makes the camera ease in when it starts moving and ease out when it stops moving.")]
    [SerializeField] private bool cameraEasing = true;
    [Tooltip("When enabled, camera speed is evaluated as a modifier. With acceleration disabled, the camera is accelerated to the Camera speed.")]
    [SerializeField] private bool cameraAcceleration = false;
    [Tooltip("Normal speed of camera movement.")]
    [SerializeField] private float movementSpeed = 8F;
    [Tooltip("Speed multiplier of camera movement when shift is held down.")]
    [SerializeField] private float sprintSpeedMultiplier = 4F;
    [Tooltip("Amount to zoom the camera when using the mouse wheel.")]
    [SerializeField] private float scrollSensitivity = 0.3F;

    [Header("Look Settings")]
    [Tooltip("Mouse sensitivity when looking around.")]
    [SerializeField] private float freeLookSensitivity = 2F;

    [Header("[Experimental] Cinematic Look Settings ")]
    [Tooltip("Mouse smoothing will be applied when rotating the camera.")]
    [SerializeField] private bool smoothLook = false;
    [Tooltip("Strength of the mouse smoothing when it is enabled.")]
    [SerializeField] private float elasticity = 5f;
    [Tooltip("You can roll the camera by using the [X & C] keys, this parameter will dictate the speed at which you will roll.")]
    [SerializeField] private float rollSpeed = 0.15f;
    //<<<------------------ End of public editor settings ---------------------------->>>


    //<<<------ input variables -------->>>
    private InputAction move;
    private InputAction look;
    private InputAction accelerate;
    private InputAction worldUpMove;
    private InputAction scroll;
    private InputAction enableLook;
    private InputAction roll;
    private InputAction resetRoll;
    //<<<----------------------------->>>


    //<<<------ private global variables -------->>>
    private Quaternion verticalRot;
    private Quaternion horizontalRot;
    private Vector3 lastMoveSpeed;
    private float accelerationMultiplier = 0.55F;
    private float acceleratedSpeed;
    private float momentumInterpolation = 0.03f;
    private bool looking = false;
    //<<<---------------------------------------->>>

    void Awake()
    {
        InitInput();

        // initialize start rotation
        verticalRot = Quaternion.Euler(transform.localRotation.eulerAngles.x, 0f, 0f);
        horizontalRot = Quaternion.Euler(0f, transform.localRotation.eulerAngles.y, transform.localRotation.eulerAngles.z);
    }

    private void InitInput()
    {
        PlayerInput input = GetComponent<PlayerInput>();
        move = input.actions["Move"];
        look = input.actions["Look"];
        accelerate = input.actions["Accelerate"];
        worldUpMove = input.actions["WorldUpMove"];
        scroll = input.actions["Scroll"];
        enableLook = input.actions["EnableLook"];
        roll = input.actions["Roll"];
        resetRoll = input.actions["ResetRoll"];
    }

    void Update()
    {
        // check if shift key has been pressed
        var fastMode = accelerate.IsPressed();
        // set fly speed
        var movementSpeed = fastMode ? this.movementSpeed * this.sprintSpeedMultiplier : this.movementSpeed;
        // get the movement direction based on input (WASD-QE keys) afterwards allign along the forward vector of the camera
        Vector3 wishDir = transform.TransformDirection(move.ReadValue<Vector3>());
        // do an extra input check for vertical movement (RF keys)
        wishDir.y += worldUpMove.ReadValue<float>();
        wishDir = wishDir.normalized;

        if (cameraAcceleration)
        {
            if (wishDir.sqrMagnitude != 0)
            {
                acceleratedSpeed *= 1f + accelerationMultiplier * Time.deltaTime;
                movementSpeed = acceleratedSpeed * (fastMode ? this.sprintSpeedMultiplier : 1f);
            }
            else
            {
                acceleratedSpeed = this.movementSpeed;
            }
        }
        // get the velocity vector with direction * speed
        Vector3 wishVelocity = wishDir * movementSpeed;

        // check if you want smooth camera movement like in the unity scene editor
        if (!cameraEasing)
        {
            //basic move update
            transform.position += wishVelocity * Time.deltaTime;
        }
        else
        {
            //use LERP between current and desired moveSpeed with already defined interpolation variable (momentumInterpolation)
            Vector3 addspeed = Vector3.Lerp(lastMoveSpeed, wishVelocity * Time.deltaTime, momentumInterpolation);
            transform.position += addspeed; // add new velocity to current
            lastMoveSpeed = addspeed; // update last move speed variable
        }

        // move camera forward with scroll wheel
        transform.position += transform.forward * scroll.ReadValue<float>() * movementSpeed * scrollSensitivity * Time.deltaTime;

        // calculate camera movement when right mouse button is down
        if (looking)
        {
            CalculateCameraRotation();
        }

        // toggle cursor when right mouse button is pressed
        if (enableLook.WasPerformedThisFrame())
        {
            StartLooking();
        }
        else if (enableLook.WasReleasedThisFrame())
        {
            StopLooking();
        }
    }

    private void CalculateCameraRotation()
    {
        // get mouse input and store it
        Vector2 mouseDelta = look.ReadValue<Vector2>();
        float yRot = mouseDelta.x * 0.05f * freeLookSensitivity;
        float xRot = mouseDelta.y * 0.05f * freeLookSensitivity;

        //------- roll input & calculation --------
            float roll = this.roll.ReadValue<float>();
        {
            roll *= rollSpeed;
            if (resetRoll.WasReleasedThisFrame())
            {
                horizontalRot.x = 0f;
                horizontalRot.z = 0f;
            }
        }
        //-----------------------------------------

        if (smoothLook)
        {
            float minMax = elasticity / 3f;
            yRot = Math.Clamp(yRot, -minMax, minMax);
            xRot = Math.Clamp(xRot, -minMax, minMax);
        }

        // calculate new horizontal and vertical rotation
        horizontalRot *= Quaternion.Euler(0, yRot, roll);
        verticalRot *= Quaternion.Euler(-xRot, 0, 0f);

        // clamp vertical rotation betwen min and max angle if you have clamping enabled
        if (clampVerticalRotation)
        {
            verticalRot = ClampRotationAroundXAxis(verticalRot);
        }

        //if smoothLook is disable then we just update the camera rotation by combining horizontal and vertical rotation
        if (!smoothLook)
        {
            transform.localRotation = horizontalRot * verticalRot;
        }
        else
        {
            //we SLERP between the current and the desired rotation with the elasticity variable
            transform.localRotation = Quaternion.Slerp(transform.localRotation, horizontalRot * verticalRot,
                elasticity * Time.deltaTime);
        }

    }
    private Quaternion ClampRotationAroundXAxis(Quaternion q)
    {
        // recalculate quaternion to become unit quaternion (might not be necessary)
        q.x /= q.w;
        q.y /= q.w;
        q.z /= q.w;
        q.w = 1.0f;

        // convert quaternion to euler angle for clamping between min & max and convert back to quaternion angle
        float angleX = 2.0f * Mathf.Rad2Deg * Mathf.Atan(q.x);
        angleX = Mathf.Clamp(angleX, minAngle, maxAngle);
        q.x = Mathf.Tan(0.5f * Mathf.Deg2Rad * angleX);

        return q;
    }

    void OnDisable()
    {
        StopLooking();
    }

    // <<<--- Enable free looking --->>>
    public void StartLooking()
    {
        looking = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // <<<--- Disable free looking --->>>
    public void StopLooking()
    {
        looking = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}