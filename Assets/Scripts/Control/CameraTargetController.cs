using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraTargetController : MonoBehaviour
{
    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    public Transform cinemachineCameraTarget;
    public Transform playerTarget;
    public float yOffset = 0.3f;

    [Tooltip("How far in degrees can you move the camera up")]
    public float topClamp = 70.0f;

    [Tooltip("How far in degrees can you move the camera down")]
    public float bottomClamp = -30.0f;

    [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
    public float cameraAngleOverride = 0.0f;

    [Tooltip("For locking the camera position on all axis")]
    public bool LockCameraPosition = false;

    public float mouseSensitivity = 0.1f;
    public float gamepadSensitivity = 200f;

    // cinemachine
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    private const float _threshold = 0.01f;

#if ENABLE_INPUT_SYSTEM
    private PlayerInput _playerInput;
#endif
    private bool IsCurrentDeviceMouse
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
        }
    }

    private void Start()
    {
        _cinemachineTargetYaw = cinemachineCameraTarget.transform.rotation.eulerAngles.y;

#if ENABLE_INPUT_SYSTEM
        _playerInput = InputManager.Instance.GetComponent<PlayerInput>();
#else
		Debug.LogError("No Input System Detected");
#endif
    }


    public void LateUpdate()
    {
        CameraPosition();
        CameraRotation();
    }

    private void CameraPosition()
    {
        cinemachineCameraTarget.position = playerTarget.position + Vector3.up * yOffset;
    }

    private void CameraRotation()
    {
        var input = InputManager.Instance;

        // if there is an input and camera position is not fixed
        if (input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
        {
            //Don't multiply mouse input by Time.deltaTime;
            float deltaTimeMultiplier = IsCurrentDeviceMouse ? mouseSensitivity : Time.deltaTime * gamepadSensitivity;

            _cinemachineTargetYaw += input.look.x * deltaTimeMultiplier;
            _cinemachineTargetPitch += (IsCurrentDeviceMouse ? -1f : 1f) * input.look.y * deltaTimeMultiplier;
        }

        // clamp our rotations so our values are limited 360 degrees
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, bottomClamp, topClamp);

        // Cinemachine will follow this target
        cinemachineCameraTarget.rotation = Quaternion.Euler(_cinemachineTargetPitch + cameraAngleOverride,
            _cinemachineTargetYaw, 0.0f);
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}
