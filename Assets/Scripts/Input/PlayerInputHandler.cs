using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System.Linq;

// Structure to hold data for a command pending submission to OnInput
public class PendingCommand
{
    public Vector3 TargetPosition;
    public NetworkId TargetObjectId; // Optional: If you want to store the object reference
    //public NetworkId TargetObjectId;
    public byte CommandType; // Define your command types (e.g., 0=None, 1=Move, 2=Attack)
    public NetworkId[] SelectedUnitIds;
    //[Networked,Capacity(64)] // Example capacity: Adjust based on expected max selection size
    //public NetworkArray<NetworkId> SelectedUnitIds => default; // {get; set;}
}

// Requires the PlayerInput component from Unity's Input System and a NetworkObject
[RequireComponent(typeof(UnityEngine.InputSystem.PlayerInput))]
[RequireComponent(typeof(NetworkObject))]
public class PlayerInputHandler : NetworkBehaviour, INetworkRunnerCallbacks
{
    // --- Inspector References ---
    [Header("Configuration")]
    [SerializeField] private LayerMask _groundLayerMask = 1; // Set in inspector to Ground layer
    [SerializeField] private LayerMask _unitLayerMask = 1 << 8; // Set in inspector to Unit layer
    [SerializeField] private float _dragThreshold = 5f; // Min pixels mouse must move to start drag select
    [SerializeField] private RectTransform _selectionBoxVisual = null; // Optional UI element for selection box

    // --- Add near other variable declarations ---
    [Header("UI References")]
    [SerializeField] private ContextMenuUIManager contextMenuUIManager; // Assign in Inspector or find

    //[Header("Network Settings")]
    //[SerializeField] private NetworkPrefabRef _playerPrefab; // Assign your Player Prefab
    //[SerializeField] private string _gameSceneName = "NormalGameMap"; // <<< SET YOUR ACTUAL GAME SCENE NAME HERE

    //[Header("References")]
    //[SerializeField] private MainMenuController _mainMenuController; // <<< ASSIGN YOUR MainMenuController GameObject IN INSPECTOR

    private NetworkRunner _runner;
    private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    // Flag to prevent multiple StartGame calls racing
    //private bool _isStartingGame = false;

    // Hold the Unit Prefab reference
    [SerializeField] private NetworkPrefabRef _unitPrefab; // Assign your Unit Prefab here


    // --- Input Actions (Ensure these names match your Input Action Asset) ---
    private InputAction _selectAction;
    private InputAction _commandAction;
    private InputAction _modifierShiftAction;
    private InputAction _modifierControlAction;
    private InputAction _mousePositionAction;
    private InputAction[] _numberKeyActions = new InputAction[9];

    private InputAction _spawnAction;
    private InputAction _despawnAction;

    // --- Local State ---
    private Camera _mainCamera;
    private Vector2 _mousePosition; // Current screen mouse position
    private bool _isLeftMouseDown = false;
    private bool _isRightMouseDown = false;
    private bool _isBoxSelecting = false;
    private Vector2 _boxSelectStartPos;
    private Vector2 _boxSelectEndPos; // For drawing the box
    private float _rightMouseDownTime = 0f; // To differentiate click vs drag start
    private bool _wasRightDragPanning = false; // Flag to check if right-click resulted in a pan
    private bool _spawnNow = false; // Flag to indicate spawn for units

    // --- Selection & Fleets ---
    private HashSet<NetworkId> _currentSelection = new HashSet<NetworkId>();
    // Using Dictionary<int, List<NetworkId>> for easier serialization if needed later
    private Dictionary<int, List<NetworkId>> _savedFleets = new Dictionary<int, List<NetworkId>>();

    // --- Command Queue for Fusion Input ---
    private PendingCommand _pendingCommand = null;

    // --- Component References ---
    private UnityEngine.InputSystem.PlayerInput _playerInput;
    private UnitSelectionVisualizer _selectionVisualizer; // Example: Your script to handle highlights
    private CameraController _cameraController; // Optional: To check panning state

    // --- Constants ---
    private const byte COMMAND_NONE = 0;
    private const byte COMMAND_MOVE = 1;
    private const byte COMMAND_ATTACK = 2;
    private const byte SPAWN = 3; // Example command for spawning units

    #region Unity & Fusion Lifecycle

    private void Awake()
    {
        _mainCamera = Camera.main; // Ensure you have a main camera tagged
        _playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        _selectionVisualizer = FindFirstObjectByType<UnitSelectionVisualizer>(); // Or get reference another way
        _cameraController = FindFirstObjectByType<CameraController>(); // Or get reference another way

        if (_mainCamera == null)
        {
            Debug.LogError("PlayerInputHandler: Main Camera not found!", this);
        }

        // --- Initialize Input Actions ---
        _selectAction = _playerInput.actions["Select"];
        _commandAction = _playerInput.actions["Command"];
        _modifierShiftAction = _playerInput.actions["ModifierShift"];
        _modifierControlAction = _playerInput.actions["ModifierControl"];
        _mousePositionAction = _playerInput.actions["MousePosition"]; // Assumes an action tracking mouse pos

        for (int i = 0; i < 9; i++)
        {
            _numberKeyActions[i] = _playerInput.actions[$"NumberKey{i + 1}"]; // Assumes actions "NumberKey1", "NumberKey2", etc.
        }

        // Debug actions
        _spawnAction = _playerInput.actions["Spawn"];
        _despawnAction = _playerInput.actions["Despawn"];


        // Initialize selection box visual (if assigned)
        if (_selectionBoxVisual != null)
        {
            _selectionBoxVisual.gameObject.SetActive(false);
        }
    }

    public override void Spawned()
    {
        // We only want input processing for the local player controlling this object
        if (Object.HasInputAuthority)
        {
            // Add this script to the runner's callback list
            Runner.AddCallbacks(this);
            Debug.Log("PlayerInputHandler Spawned and callbacks registered for input authority.");
        }
        else
        {
            // Disable input processing for proxies, but keep script enabled for potential visual updates
            enabled = false; // Disable Update() loop for non-local players
            Debug.Log("PlayerInputHandler Spawned for proxy, disabling input processing.");
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Clean up callbacks if this object was registered
        if (runner != null && Object.HasInputAuthority) // Check HasInputAuthority for safety, though runner might handle it
        {
             runner.RemoveCallbacks(this);
             Debug.Log("PlayerInputHandler Despawned and callbacks removed.");
        }
    }


    private void Update()
    {
        // Only process input if we have input authority over this network object
        if (!Object.HasInputAuthority)
        {
            return;
        }

        // --- Read Input States ---
        _mousePosition = _mousePositionAction.ReadValue<Vector2>();
        bool shiftPressed = _modifierShiftAction.IsPressed();
        bool controlPressed = _modifierControlAction.IsPressed();

        // --- Handle Input Logic ---
        HandleSelectionInput(shiftPressed, controlPressed);
        HandleFleetInput(shiftPressed, controlPressed);
        HandleCommandInput(); // Right-click actions
        HandleSelectionBoxVisual();
        HandleSpawnInput(); // Spawn units if needed
        // Update UI based on selection (call AFTER selection logic)
        UpdateContextMenu();
    }

    #endregion

    #region Input Handling Logic

    // --- Context Menu Update ---
    // This method is called to update the context menu based on current selection
    private void UpdateContextMenu()
    {
        if (contextMenuUIManager == null) return;

        if (_currentSelection.Count > 0)
        {
            // Determine the primary unit to focus on (e.g., the first selected)
            NetworkId focusUnitId = _currentSelection.FirstOrDefault(); // Simple approach
            // TODO: More complex: Find highest rank unit, capital ship, etc.

            // Show/Update the menu only if the focus unit is valid
            if (focusUnitId.IsValid) {
                // Pass the runner, the ID to focus on, and the full current selection
                contextMenuUIManager.ShowMenu(Runner, focusUnitId, _currentSelection);
            } else {
                // If the only selected unit became invalid, hide menu
                contextMenuUIManager.HideMenu();
            }
        }
        else
        {
            // No selection, hide the menu
            contextMenuUIManager.HideMenu();
        }
    }

    private void HandleSelectionInput(bool shiftPressed, bool controlPressed)
    {
        bool selectionChanged = false;

        

        // --- Left Mouse Button Down ---
        if (_selectAction.WasPressedThisFrame())
        {
            _isLeftMouseDown = true;
            _boxSelectStartPos = _mousePosition;
            _isBoxSelecting = false; // Reset box selection state

            // --- Check for UI Interaction ---
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                // // Mouse is over a UI element, do not process world click for selection/deselection
                // if (_selectAction.WasPressedThisFrame()) {
                //     _isLeftMouseDown = true; // Still track mouse down state if needed elsewhere
                // }
                // if (_selectAction.WasReleasedThisFrame()) {
                //     _isLeftMouseDown = false;
                //     // Don't clear selection if clicking on UI
                // }
                //return; // Stop further world processing for this click
            }
            // Raycast for single unit selection
            else
            {
                NetworkObject clickedObject = RaycastUnits();

                if (clickedObject != null)
                {
                    StarshipBase unit = clickedObject.GetComponent<StarshipBase>(); // Assuming UnitController exists
                    if (unit != null)
                    {
                        NetworkId unitId = clickedObject.Id;

                        if (controlPressed) // Ctrl + Click: Select all of type
                        {
                            string targetClass = unit.shipClass; // Assuming UnitController has ShipClass property
                            if (!shiftPressed) // Clear selection if not holding shift
                                _currentSelection.Clear();
                            // Find all visible units of the same class (Requires efficient lookup, e.g., UnitManager)
                            // TODO: This is a simplified example - replace with your unit finding logic
                            if (_currentSelection.Contains(unitId))
                            {
                                foreach (var otherUnitId in _currentSelection.ToList())
                                {
                                    if (Runner.TryFindObject(otherUnitId, out NetworkObject obj) && 
                                        obj != null && 
                                        obj.GetComponent<StarshipBase>().shipClass == targetClass)
                                        _currentSelection.Remove(obj.Id); // Remove if already selected
                                }
                            }
                            else
                            {
                                //foreach (var otherUnit in FindObjectsByType<StarshipBase>(sortMode: FindObjectsSortMode.None)) // Inefficient - Use UnitManager!
                                // Use the UnitManager, providing the NetworkRunner
                                if (UnitManager.Instance != null && Runner != null) // Ensure Manager and Runner exist
                                {
                                    foreach (NetworkId id in UnitManager.Instance.GetUnitIdsByClass(targetClass))
                                    {
                                        _currentSelection.Add(id);
                                    }
                                }
                            }
                            selectionChanged = true;
                        }
                        else if (shiftPressed && !controlPressed) // Shift + Click: Add/Remove from selection
                        {
                            if (_currentSelection.Contains(unitId))
                                _currentSelection.Remove(unitId);
                            else
                                _currentSelection.Add(unitId);
                            selectionChanged = true;
                        }
                        else if (!shiftPressed && !controlPressed) // Simple Click: Select only this unit
                        {
                            _currentSelection.Clear();
                            _currentSelection.Add(unitId);
                            selectionChanged = true;
                        }
                    }
                }
                else // Clicked on empty space/ground
                {
                    if (!shiftPressed && !controlPressed) // Clear selection only if no modifiers held
                    {
                        if (_currentSelection.Count > 0)
                        {
                            _currentSelection.Clear();
                            selectionChanged = true;
                        }
                    }
                }
            }
        }

        // --- Left Mouse Button Held (Drag Detection) ---
        if (_isLeftMouseDown)
        {
            if (!_isBoxSelecting && Vector2.Distance(_mousePosition, _boxSelectStartPos) > _dragThreshold)
            {
                _isBoxSelecting = true;
            }
            if (_isBoxSelecting)
            {
                 _boxSelectEndPos = _mousePosition; // Update end position for visual
            }
        }

        // --- Left Mouse Button Up ---
        if (_selectAction.WasReleasedThisFrame())
        {
            if (_isBoxSelecting)
            {
                // Finalize box selection
                Rect selectionRect = GetScreenRect(_boxSelectStartPos, _mousePosition);
                List<NetworkId> unitsInRect = FindUnitsInRect(selectionRect);

                if (!shiftPressed) // If shift isn't held, clear previous selection
                {
                    _currentSelection.Clear();
                }

                foreach (NetworkId unitId in unitsInRect)
                {
                    _currentSelection.Add(unitId); // Add units found in the box
                }
                selectionChanged = true; // Selection potentially changed
            }

            _isLeftMouseDown = false;
            _isBoxSelecting = false;
        }

        // --- Update Visuals if Selection Changed ---
        if (selectionChanged)
        {
            UpdateSelectionHighlights();
            // Debug.Log($"Selection Changed: {_currentSelection.Count} units selected.");
        }
    }

    private void HandleFleetInput(bool shiftPressed, bool controlPressed)
    {
        for (int i = 0; i < 9; i++)
        {
            if (_numberKeyActions[i].WasPressedThisFrame())
            {
                int fleetNumber = i + 1;

                if (controlPressed && shiftPressed) // Ctrl + Shift + Num: Overwrite/Set Fleet
                {
                    _savedFleets[fleetNumber] = new List<NetworkId>(_currentSelection);
                    Debug.Log($"Fleet {fleetNumber} saved/overwritten with {_currentSelection.Count} units.");
                }
                else if (controlPressed && !shiftPressed) // Ctrl + Num: Add to Fleet
                {
                    if (!_savedFleets.ContainsKey(fleetNumber))
                    {
                        _savedFleets[fleetNumber] = new List<NetworkId>();
                    }
                    // Add units from current selection, avoiding duplicates
                    foreach (NetworkId id in _currentSelection)
                    {
                        if (!_savedFleets[fleetNumber].Contains(id))
                        {
                             _savedFleets[fleetNumber].Add(id);
                        }
                    }
                    Debug.Log($"Added {_currentSelection.Count} units to Fleet {fleetNumber}. Total: {_savedFleets[fleetNumber].Count}");
                }
                else if (!controlPressed && !shiftPressed) // Num: Recall Fleet
                {
                    if (_savedFleets.TryGetValue(fleetNumber, out List<NetworkId> fleetUnits))
                    {
                        _currentSelection.Clear();
                        // Important: Filter out units that might have been destroyed since saving
                        foreach (NetworkId id in fleetUnits)
                        {
                            if (Runner != null && Runner.TryFindObject(id, out NetworkObject obj) && obj != null)
                            {
                                _currentSelection.Add(id);
                            }
                        }
                        UpdateSelectionHighlights();
                        Debug.Log($"Fleet {fleetNumber} recalled: {_currentSelection.Count} units selected.");
                    }
                }
                break; // Process only one number key per frame
            }
        }
    }

    private void HandleSpawnInput()
    {
        // Check for spawn command (e.g., pressing 'B' key)
        if (_spawnNow) // Assuming this is set when the player wants to spawn
        {
            // Spawn units at the specified position (e.g., mouse position or predefined spawn point)
           
            if (RaycastGround(out Vector3 groundHitPoint))
                 
                _pendingCommand = new PendingCommand
                {

                    TargetPosition = groundHitPoint,
                    //TargetObjectId = targetUnitObject.Id,
                    CommandType = SPAWN,
                    //SelectedUnitIds = _currentSelection.ToArray() // Snapshot current selection
                };
            // Debug.Log($"Pending Command Prepared: Type={commandType}, Units={_pendingCommand.SelectedUnitIds.Length}");
                 }
            Vector3 spawnPosition = _mousePosition; // Replace with your desired spawn logic
            Runner.Spawn(_unitPrefab, spawnPosition, Quaternion.identity, Object.InputAuthority); // Spawn unit
            _spawnNow = false; // Reset flag after spawning
    }

    
    private void HandleCommandInput()
    {
        // --- Right Mouse Button Down ---
        if (_commandAction.WasPressedThisFrame())
        {
            _isRightMouseDown = true;
            _rightMouseDownTime = Time.time;
            _wasRightDragPanning = false; // Reset pan flag
        }

        // --- Right Mouse Button Held (Check for Pan Start) ---
        // Note: This assumes CameraController handles panning. If not,
        // you'd calculate mouse delta here and set _wasRightDragPanning if delta > threshold.
        if (_isRightMouseDown && _cameraController != null && _cameraController.IsPanning())
        {
            _wasRightDragPanning = true;
        }
        // Simple time-based heuristic if no CameraController reference:
        // if (_isRightMouseDown && Time.time > _rightMouseDownTime + 0.1f) // If held for > 0.1s, assume potential pan
        // {
        //      // Check mouse movement delta here if CameraController doesn't handle it
        //      // If delta > threshold, set _wasRightDragPanning = true
        // }


        // --- Right Mouse Button Up ---
        if (_commandAction.WasReleasedThisFrame())
        {
             _isRightMouseDown = false;

             // Only issue command if we have selected units AND we weren't panning with the right mouse button
             if (_currentSelection.Count > 0 && !_wasRightDragPanning)
             {
                 // Raycast to determine target (ground or enemy unit)
                 Vector3 targetPosition = Vector3.zero;
                 //NetworkId targetObject =  // No target by default
                 byte commandType = COMMAND_NONE;

                 // Prioritize hitting units for attack commands
                 NetworkObject targetUnitObject = RaycastUnits(_unitLayerMask); // Raycast only for units

                 if (targetUnitObject != null /* && IsEnemy(targetUnitObject) */) // Add IsEnemy check if needed
                 {
                     //NetworkId targetObjectId = targetUnitObject.Id;
                     targetPosition = targetUnitObject.transform.position; // Or a specific attack point
                     commandType = COMMAND_ATTACK;
                     // Debug.Log($"Command Target: Unit {targetObjectId}");
                 }
                 else // Didn't hit a unit, try hitting ground for move command
                 {
                      if (RaycastGround(out Vector3 groundHitPoint))
                      {
                          targetPosition = groundHitPoint;
                          commandType = COMMAND_MOVE;
                          // Debug.Log($"Command Target: Ground {targetPosition}");
                      }
                 }

                 // If a valid command target was found, prepare the command
                 if (commandType != COMMAND_NONE)
                 {
                     _pendingCommand = new PendingCommand
                     {
                         TargetPosition = targetPosition,
                         TargetObjectId = targetUnitObject.Id,
                         CommandType = commandType,
                         SelectedUnitIds = _currentSelection.ToArray() // Snapshot current selection
                     };
                     // Debug.Log($"Pending Command Prepared: Type={commandType}, Units={_pendingCommand.SelectedUnitIds.Length}");
                 }
             }
             _wasRightDragPanning = false; // Reset pan flag
        }
    }

    private void HandleSelectionBoxVisual()
    {
        if (_selectionBoxVisual == null) return;

        if (_isBoxSelecting)
        {
            _selectionBoxVisual.gameObject.SetActive(true);

            Vector2 start = _boxSelectStartPos;
            Vector2 end = _boxSelectEndPos;

            Vector2 size = end - start;
            Vector2 absSize = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));

            _selectionBoxVisual.anchoredPosition = start + size / 2f;
            _selectionBoxVisual.sizeDelta = absSize;

            // Adjust pivot based on drag direction (optional, for consistent anchor)
            _selectionBoxVisual.pivot = new Vector2(size.x < 0 ? 1 : 0, size.y < 0 ? 1 : 0);
            _selectionBoxVisual.anchoredPosition = start; // Re-apply position after pivot change
             _selectionBoxVisual.sizeDelta = absSize;


        }
        else
        {
            _selectionBoxVisual.gameObject.SetActive(false);
        }
    }


    #endregion

    #region Helper Methods

    private NetworkObject RaycastUnits(LayerMask? layerMask = null)
    {
        if (_mainCamera == null) return null;
        Ray ray = _mainCamera.ScreenPointToRay(_mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask ?? _unitLayerMask)) // Use provided mask or default unit mask
        {
            // Check if the hit object has a NetworkObject component (and potentially UnitController)
            return hit.collider.GetComponentInParent<NetworkObject>(); // Use GetComponentInParent for complex prefabs
        }
        return null;
    }

    private bool RaycastGround(out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        if (_mainCamera == null) return false;
        Ray ray = _mainCamera.ScreenPointToRay(_mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 5000f, _groundLayerMask)) // Use ground layer mask
        {
            hitPoint = hit.point;
            return true;
        }
        return false;
    }

    private List<NetworkId> FindUnitsInRect(Rect screenRect)
    {
        List<NetworkId> unitsFound = new List<NetworkId>();
        // This requires iterating through potentially all selectable units
        // Optimize this with spatial partitioning or by querying a UnitManager
        foreach (var unitController in FindObjectsByType<StarshipBase>(FindObjectsSortMode.None)) // Inefficient - Replace with UnitManager lookup!
        {
            NetworkObject netObj = unitController.GetComponent<NetworkObject>();
            if (netObj != null && unitController.gameObject.activeInHierarchy) // Check if unit is valid
            {
                Vector3 screenPoint = _mainCamera.WorldToScreenPoint(unitController.transform.position);
                // Check if unit is within screen bounds and the selection rectangle
                if (screenPoint.z > 0 && // Check if in front of camera
                    screenRect.Contains(new Vector2(screenPoint.x, screenPoint.y)))
                {
                    unitsFound.Add(netObj.Id);
                }
            }
        }
        return unitsFound;
    }

    private Rect GetScreenRect(Vector2 screenPos1, Vector2 screenPos2)
    {
        // Ensure rect has positive width/height
        float xMin = Mathf.Min(screenPos1.x, screenPos2.x);
        float yMin = Mathf.Min(screenPos1.y, screenPos2.y);
        float xMax = Mathf.Max(screenPos1.x, screenPos2.x);
        float yMax = Mathf.Max(screenPos1.y, screenPos2.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }


    private void UpdateSelectionHighlights()
    {
        // TODO: Implement your logic to visually highlight units in _currentSelection
        // This might involve:
        // 1. Iterating through all selectable units.
        // 2. Checking if their NetworkId is in _currentSelection.
        // 3. Enabling/disabling a highlight component or changing material properties.
        // Example using a hypothetical UnitSelectionVisualizer:
        // if (_selectionVisualizer != null)
        // {
        //     _selectionVisualizer.UpdateHighlights(_currentSelection);
        // }
        // Debug.Log($"Updating highlights for {_currentSelection.Count} units.");
    }

    // Optional: Helper for CameraController
    public Vector3 GetAveragePositionOfSelection()
    {
        if (_currentSelection.Count == 0 || Runner == null)
        {
            return Vector3.zero; // Or camera's current target
        }

        Vector3 averagePos = Vector3.zero;
        int validUnits = 0;
        foreach (NetworkId id in _currentSelection)
        {
             if (Runner.TryFindObject(id, out NetworkObject obj) && obj != null)
             {
                 averagePos += obj.transform.position;
                 validUnits++;
             }
        }

        return validUnits > 0 ? averagePos / validUnits : Vector3.zero;
    }


    #endregion

    #region INetworkRunnerCallbacks

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var rtsInputData = new NetworkInputData(); // Create instance of your input struct

        // TODO: debug section, remove later
        if (Keyboard.current.escapeKey.wasPressedThisFrame) // Requires Input System
        {
            //data.CommandType = NetworkCommands.QuitGame; // Set quit command flag
            Debug.Log("Input: Quit Command Requested");
        }

        // // Get mouse position in world space (assuming a ground plane at Y=0)
        // Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()); // Requires Input System package
        // if (Physics.Raycast(ray, out RaycastHit hit, 100f)) // Check against a layer mask if needed
        // {
        //     data.MousePosition = hit.point;
        //     //Debug.Log($"Input: Mouse Position {data.MousePosition.x}, {data.MousePosition.y}, {data.MousePosition.z}");

        //     // Check for Spawn command (e.g., pressing 'B' key)
        //     if (Keyboard.current.bKey.wasPressedThisFrame) // Requires Input System
        //     {
        //         data.CommandType = NetworkCommands.SpawnUnit; // Set spawn command flag
        //         Debug.Log("Input: Spawn Command Requested");
        //     }
        // }
        // --- Set the collected input data ---
        //input.Set(data);

        if (_pendingCommand != null)
        {
            rtsInputData.TargetPosition = _pendingCommand.TargetPosition;
            rtsInputData.TargetObjectId = _pendingCommand.TargetObjectId;
            rtsInputData.CommandType = _pendingCommand.CommandType;
            rtsInputData.SelectedUnitIds.Clear(); // Clear previous selection
            //rtsInputData.SelectedUnitIds = _pendingCommand.SelectedUnitIds;

            // Snapshot current selection
            if (_pendingCommand.CommandType == COMMAND_MOVE || _pendingCommand.CommandType == COMMAND_ATTACK)
            {
                for (int idx = 0; _pendingCommand.SelectedUnitIds.Count() > idx; idx++)
                {
                    // Ensure the array is large enough to hold the selection
                    if (rtsInputData.SelectedUnitIds.Length <= idx)
                    {
                        rtsInputData.SelectedUnitIds.Set(idx,_currentSelection.ToArray()[idx]); // Convert HashSet to array
                    }
                }
            }
            _pendingCommand = null; // Consume the command
        }
        else
        {
            // Set default values if no command is pending
            rtsInputData.CommandType = COMMAND_NONE;
            // = System.Array.Empty<NetworkId>(); // Use empty array for no selection
        }

        // Set the collected input struct into Fusion's input collection
        input.Set<NetworkInputData>(rtsInputData);
    }

    /// <summary>
    /// Allows the UI Manager to queue a command generated from UI interaction.
    /// </summary>
    public void QueueCommandFromUI(PendingCommand command)
    {
        // Basic queuing: Overwrite any pending world command.
        // TODO: More complex: Could have separate queues or logic if needed.
        if (command != null)
        {
            _pendingCommand = command;
            Debug.Log($"Command queued from UI: Type={command.CommandType}");
        }
    }

    // --- Provide empty implementations for unused INetworkRunnerCallbacks methods ---
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { Debug.Log($"Runner Shutdown: {shutdownReason}"); }
    public void OnConnectedToServer(NetworkRunner runner) { Debug.Log("Connected to Server"); }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { Debug.Log($"Disconnected from Server: {reason}"); }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { Debug.Log($"Connect Failed: {reason}"); }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress){ }

    #endregion
}
