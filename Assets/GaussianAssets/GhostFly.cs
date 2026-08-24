using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class GhostFly : MonoBehaviour
{
    [Tooltip("The rig root that moves. Leave empty to move this object.")]
    public Transform rig;
 
    [Tooltip("Camera transform. Usually CenterEyeAnchor.")]
    public Transform head;

    public float moveSpeed = 5f;
    public float verticalSpeed = 3f;
    public float boostMultiplier = 4f;
    public float deadzone = 0.15f;

    InputDevice rightHand;

    void Start()
    {
        if (rig == null) rig = transform;
        if (head == null && Camera.main != null) head = Camera.main.transform;
    }

    void Update()
    {
        if (!rightHand.isValid) FindController();
        if (!rightHand.isValid || head == null) return;

        rightHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
        rightHand.TryGetFeatureValue(CommonUsages.primaryButton,   out bool aButton);
        rightHand.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bButton);
        rightHand.TryGetFeatureValue(CommonUsages.gripButton,      out bool grip);

        float speed = moveSpeed * (grip ? boostMultiplier : 1f);
        Vector3 move = Vector3.zero;

        // Stick: forward/back and strafe, flat to the horizon
        if (stick.magnitude > deadzone)
        {
            Vector3 fwd = head.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 rgt = head.right;   rgt.y = 0f; rgt.Normalize();
            move += (fwd * stick.y + rgt * stick.x) * speed;
        }

        // A = up, B = down
        float vSpeed = verticalSpeed * (grip ? boostMultiplier : 1f);
        if (aButton) move += Vector3.up * vSpeed;
        if (bButton) move -= Vector3.up * vSpeed;

        rig.position += move * Time.deltaTime;
    }

    void FindController()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);
        if (devices.Count > 0) rightHand = devices[0];
    }
}