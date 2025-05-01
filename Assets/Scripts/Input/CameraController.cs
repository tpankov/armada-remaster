using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))] // Ensure this script is on a Camera GameObject
public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputHandler playerInputHandler; // Assign in Inspector
    [SerializeField] private UnityEngine.InputSystem.PlayerInput playerInput; // Assign PlayerInput component

    [Header("Targeting & Following")]
    [SerializeField] private Vector3 homeBasePosition = Vector3.zero;
    [SerializeField] private float followLerpSpeed = 5f;

    [Header("Movement Speeds")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float panSpeed = 50f;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float zoomSpeed = 10f;

    [Header("Zoom & Pitch")]
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private float defaultDistance = 25f;
    [SerializeField] private float overviewDistance = 70f; // Distance for Shift+Z overview
    [SerializeField] private float minPitch = 10f; // Min angle from horizontal (e.g., 10 degrees)
    [SerializeField] private float maxPitch = 85f; // Max angle (close to top-down)
    [SerializeField] private float defaultPitch = 45f;
    [SerializeField] private float overviewPitch = 88f; // Pitch for Shift+Z overview
    // Curve mapping normalized distance (0=min, 1=max) to pitch (0=minPitch, 1=maxPitch)
    [SerializeField] private AnimationCurve zoomPitchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Smoothing")]
    [SerializeField] private float smoothSpeedPosition = 0.1f; // Approx time to reach target pos
    [SerializeField] private float smoothSpeedRotation = 0.1f; // Approx time to reach target rot/zoom

    // --- Runtime State ---
    private Transform _transform;
    private Vector3 _currentTargetPosition; // The point in the world the camera orbits/looks at
    private Vector3 _smoothTargetPosition; // Smoothed version of _currentTargetPosition
    private Vector3 _positionVelocity = Vector3.zero; // For SmoothDamp position

    private float _targetDistance;
    private float _currentDistance;
    private float _distanceVelocity = 0f; // For SmoothDamp distance

    private float _targetYaw; // Rotation around Y axis
    private float _currentYaw;
    private float _yawVelocity = 0f; // For SmoothDamp yaw

    private float _targetPitch; // Rotation around X axis (local)
    private float _currentPitch;
    private float _pitchVelocity = 0f; // For SmoothDamp pitch

    private bool _isCameraFollowing = false;
    private bool _isZoomedOutOverview = false;
    private bool _isPanning = false;
    private bool _isRotating = false;
    private Vector2 _lastMousePosition;

    // --- Input Action References ---
    private InputAction _cameraMoveAction;
    private InputAction _cameraZoomAction;
    private InputAction _mousePositionAction;
    private InputAction _commandAction; // Right mouse
    private InputAction _rotateToggleAction; // L+R mouse
    private InputAction _toggleOverviewAction;
    private InputAction _centerOnSelectionAction;
    private InputAction _toggleFollowAction;
    private InputAction _goHomeAction;


    #region Unity Lifecycle

    private void Awake()
    {
        _transform = transform;

        // Ensure references are set
        if (playerInputHandler == null)
            Debug.LogError("PlayerInputHandler not assigned!", this);
        if (playerInput == null)
            playerInput = GetComponentInParent<UnityEngine.InputSystem.PlayerInput>(); // Try to find it if not assigned
        if (playerInput == null)
            Debug.LogError("PlayerInput component not found!", this);

        // Get Actions
        _cameraMoveAction = playerInput.actions["CameraMove"];
        _cameraZoomAction = playerInput.actions["CameraZoomScroll"];
        _mousePositionAction = playerInput.actions["MousePosition"];
        _commandAction = playerInput.actions["Command"];
        _rotateToggleAction = playerInput.actions["CameraRotateToggle"];
        _toggleOverviewAction = playerInput.actions["ToggleOverviewZoom"];
        _centerOnSelectionAction = playerInput.actions["CenterOnSelection"];
        _toggleFollowAction = playerInput.actions["ToggleFollowSelection"];
        _goHomeAction = playerInput.actions["GoHome"];

        // Initialize state
        _currentTargetPosition = homeBasePosition;
        _smoothTargetPosition = homeBasePosition;
        _targetDistance = defaultDistance;
        _currentDistance = defaultDistance;
        _targetYaw = _transform.eulerAngles.y; // Start with current camera yaw
        _currentYaw = _targetYaw;
        _targetPitch = defaultPitch;
        _currentPitch = defaultPitch;
    }

    private void Update()
    {
        // --- Read Inputs ---
        Vector2 moveInput = _cameraMoveAction.ReadValue<Vector2>();
        float scrollInput = _cameraZoomAction.ReadValue<Vector2>().y;
        Vector2 mousePosition = _mousePositionAction.ReadValue<Vector2>();
        Vector2 mouseDelta = mousePosition - _lastMousePosition;

        bool isCommandHeld = _commandAction.IsPressed();
        bool commandStarted = _commandAction.WasPressedThisFrame();
        bool commandEnded = _commandAction.WasReleasedThisFrame();

        bool isRotateHeld = _rotateToggleAction.IsPressed(); // Assumes L+R mouse action
        bool rotateStarted = _rotateToggleAction.WasPressedThisFrame();
        bool rotateEnded = _rotateToggleAction.WasReleasedThisFrame();

        // --- Handle State Toggles & Snaps ---
        if (_toggleOverviewAction.WasPressedThisFrame())
        {
            _isZoomedOutOverview = !_isZoomedOutOverview;
            if (_isZoomedOutOverview)
            {
                _targetDistance = overviewDistance;
                // We let LateUpdate handle the pitch based on the curve/distance
            }
            else
            {
                _targetDistance = defaultDistance;
                // Pitch will adjust back via curve
            }
            _isCameraFollowing = false; // Disable follow when toggling overview
        }

        if (_centerOnSelectionAction.WasPressedThisFrame() && playerInputHandler != null)
        {
            Vector3 avgPos = playerInputHandler.GetAveragePositionOfSelection();
            if (avgPos != Vector3.zero) // Check if selection is valid
            {
                _currentTargetPosition = avgPos;
                _isCameraFollowing = false; // Center action stops following
            }
        }

        if (_toggleFollowAction.WasPressedThisFrame())
        {
            _isCameraFollowing = !_isCameraFollowing;
        }

        if (_goHomeAction.WasPressedThisFrame())
        {
            _currentTargetPosition = homeBasePosition;
            _isCameraFollowing = false; // Go home stops following
        }

        // --- Handle Zoom ---
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            // Adjust target distance based on scroll input, scaled by current distance for smoother zoom
            float zoomFactor = 1.0f - (scrollInput * zoomSpeed * Time.deltaTime);
             _targetDistance *= zoomFactor;
            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
            _isZoomedOutOverview = false; // Manual zoom cancels overview state
        }

        // --- Handle Rotation (L+R Drag) ---
        if (rotateStarted)
        {
            _isRotating = true;
            // Consider hiding cursor or locking it?
        }
        if (_isRotating)
        {
            _targetYaw += mouseDelta.x * rotationSpeed * Time.deltaTime;
        }
        if (rotateEnded)
        {
            _isRotating = false;
        }

        // --- Handle Panning (Right Mouse Drag) ---
        if (commandStarted)
        {
            // Potential start of pan
        }
        if (isCommandHeld)
        {
             // Check if mouse moved significantly since right click started
             // This simple check assumes any drag is a pan if not rotating
            if (!_isRotating && mouseDelta.magnitude > 1.0f) // Threshold to differentiate click vs drag
            {
                _isPanning = true;
            }
        }
        // Panning movement is handled in LateUpdate based on the _isPanning flag

        if (commandEnded)
        {
            _isPanning = false;
        }

        // --- Update Last Mouse Position ---
        _lastMousePosition = mousePosition;

        // --- Handle WASD/Arrow Key Panning (sets target position directly) ---
        if (moveInput.magnitude > 0.01f)
        {
             // Calculate move direction relative to camera's horizontal rotation
             Vector3 forward = Vector3.Scale(_transform.forward, new Vector3(1, 0, 1)).normalized;
             Vector3 right = Vector3.Scale(_transform.right, new Vector3(1, 0, 1)).normalized;
             Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x);

             _currentTargetPosition += moveDirection * moveSpeed * Time.deltaTime;
             _isCameraFollowing = false; // Manual movement stops following
             _isZoomedOutOverview = false; // Manual movement cancels overview
        }
    }


    private void LateUpdate()
    {
        // --- Update Target Position Based on State ---
        if (_isCameraFollowing && playerInputHandler != null)
        {
            Vector3 avgPos = playerInputHandler.GetAveragePositionOfSelection();
            if (avgPos != Vector3.zero)
            {
                // Smoothly move towards the target average position
                _currentTargetPosition = Vector3.Lerp(_currentTargetPosition, avgPos, followLerpSpeed * Time.deltaTime);
            }
            else {
                 _isCameraFollowing = false; // Stop following if selection is lost
            }
        }

        // Apply Right-Click Drag Panning
        if (_isPanning)
        {
             Vector2 mouseDelta = _mousePositionAction.ReadValue<Vector2>() - _lastMousePosition;
             // Adjust sensitivity based on distance? Optional.
             float panSensitivity = panSpeed * (_currentDistance / maxDistance); // Scale pan speed by zoom
             Vector3 right = _transform.right * -mouseDelta.x * panSensitivity * Time.deltaTime;
             Vector3 up = Vector3.Scale(_transform.up, new Vector3(1,0,1)).normalized; // Horizontal plane up
             Vector3 forwardBasedUp = Vector3.Cross(right, _transform.forward).normalized; // More robust up based on forward
             Vector3 forwardPan = Vector3.Scale(_transform.forward, new Vector3(1, 0, 1)).normalized * -mouseDelta.y * panSensitivity * Time.deltaTime;

             // Pan along camera's local horizontal plane
             _currentTargetPosition += (right + forwardPan);
             _isCameraFollowing = false; // Panning stops following
             _isZoomedOutOverview = false; // Panning cancels overview
        }

        // --- Smoothly Interpolate Camera Parameters ---
        _smoothTargetPosition = Vector3.SmoothDamp(_smoothTargetPosition, _currentTargetPosition, ref _positionVelocity, smoothSpeedPosition);
        _currentDistance = Mathf.SmoothDamp(_currentDistance, _targetDistance, ref _distanceVelocity, smoothSpeedRotation);
        _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _yawVelocity, smoothSpeedRotation);

        // Calculate target pitch based on zoom curve
        float normalizedDistance = Mathf.InverseLerp(minDistance, maxDistance, _currentDistance);
        float curveValue = zoomPitchCurve.Evaluate(normalizedDistance); // Value from 0 to 1
        _targetPitch = Mathf.Lerp(minPitch, maxPitch, curveValue); // Map curve value to pitch range

        // Override pitch if in overview state
        if (_isZoomedOutOverview) {
            _targetPitch = overviewPitch;
        }

        _currentPitch = Mathf.SmoothDampAngle(_currentPitch, _targetPitch, ref _pitchVelocity, smoothSpeedRotation);
        _currentPitch = Mathf.Clamp(_currentPitch, minPitch, maxPitch); // Ensure pitch stays within bounds

        // --- Calculate Final Position & Rotation ---
        Quaternion targetRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0);
        Vector3 positionOffset = targetRotation * Vector3.forward * -_currentDistance;

        // --- Apply Transform ---
        _transform.position = _smoothTargetPosition + positionOffset;
        _transform.rotation = targetRotation;

        // Update last mouse position for next frame's delta calculation in LateUpdate if needed
         _lastMousePosition = _mousePositionAction.ReadValue<Vector2>();
    }

    #endregion

    #region Public Accessors

    // Allow PlayerInputHandler to check if we are panning
    public bool IsPanning()
    {
        return _isPanning;
    }

    #endregion
}
