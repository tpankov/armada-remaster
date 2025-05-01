using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting; // Required for async Task

// Make sure this component is placed on a GameObject that persists across scenes
// (e.g., create an empty GameObject like "_NetworkManager" and add this script)

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network Settings")]
    [SerializeField] private NetworkPrefabRef _playerPrefab; // Assign your Player Prefab
    [SerializeField] private string _gameSceneName = "NormalGameMap"; // <<< SET YOUR ACTUAL GAME SCENE NAME HERE

    [Header("References")]
    [SerializeField] private MainMenuController _mainMenuController; // <<< ASSIGN YOUR MainMenuController GameObject IN INSPECTOR

    private NetworkRunner _runner;
    private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    // Flag to prevent multiple StartGame calls racing
    private bool _isStartingGame = false;

    // Hold the Unit Prefab reference
    [SerializeField] private NetworkPrefabRef _unitPrefab; // Assign your Unit Prefab here

    // --- Implement OnInput ---
    public void OnInput(NetworkRunner runner, NetworkInput input){}
    // public void OnInput(NetworkRunner runner, NetworkInput input)
    // {
    //     var data = new NetworkInputData(); // Create an instance of our input struct
        
    //     // --- Sample Input Gathering (Replace with your input system) ---
    //     // This is just an example; use Input System or your preferred method.

    //     // Check for quit command (e.g., pressing 'Escape' key)
    //     if (Keyboard.current.escapeKey.wasPressedThisFrame) // Requires Input System
    //     {
    //         data.CommandType = NetworkCommands.QuitGame; // Set quit command flag
    //         Debug.Log("Input: Quit Command Requested");
    //     }

    //     // Get mouse position in world space (assuming a ground plane at Y=0)
    //     Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()); // Requires Input System package
    //     if (Physics.Raycast(ray, out RaycastHit hit, 100f)) // Check against a layer mask if needed
    //     {
    //         data.MousePosition = hit.point;
    //         //Debug.Log($"Input: Mouse Position {data.MousePosition.x}, {data.MousePosition.y}, {data.MousePosition.z}");

    //         // Check for Spawn command (e.g., pressing 'B' key)
    //         if (Keyboard.current.bKey.wasPressedThisFrame) // Requires Input System
    //         {
    //             data.CommandType = NetworkCommands.SpawnUnit; // Set spawn command flag
    //             Debug.Log("Input: Spawn Command Requested");
    //         }

    //         // Check for Move command (e.g., Right Mouse Button click)
    //         if (Mouse.current.rightButton.wasPressedThisFrame) // Requires Input System
    //         {
    //             data.CommandType = NetworkCommands.MoveUnit; // Set move command flag
    //             Debug.Log($"Input: Move Command Requested at {data.MousePosition}");

    //             // --- Basic Unit Selection (Very Simple Example) ---
    //             // Find the closest controllable unit belonging to this player
    //             // NOTE: This selection logic is CLIENT-SIDE for IMMEDIATE feedback.
    //             // The actual command is processed authoritatively on the server.
    //             float closestDist = float.MaxValue;
    //             NetworkObject selectedUnit = null;
    //             PlayerRef localPlayer = runner.LocalPlayer; // Get the local player reference

    //             // Iterate through all NetworkObjects that are Units
    //             foreach (var unitNO in runner.GetAllNetworkObjects()) // Iterate all active NOs
    //             {
    //                 Unit unit = unitNO.GetComponent<Unit>();
    //                 if (unit != null && unit.Object.InputAuthority == localPlayer) // Check if it's a unit AND belongs to us
    //                 {
    //                     float dist = Vector3.Distance(hit.point, unit.transform.position);
    //                     if (dist < closestDist)
    //                     {
    //                         closestDist = dist;
    //                         selectedUnit = unit.Object;
    //                     }
    //                 }
    //             }

    //             if (selectedUnit != null)
    //             {
    //                 data.TargetUnitId = selectedUnit.Id; // Set the ID of the unit to move
    //                 Debug.Log($"Input: Targeting Unit ID {selectedUnit.Id} for move command.");
    //             } else {
    //                 Debug.Log("Input: Move command requested, but no nearby unit found for this player.");
    //                 data.CommandType = NetworkCommands.None; // Cancel move command if no unit selected
    //             }
    //         }
    //     }
    //     // --- Set the collected input data ---
    //     input.Set(data);
    // }


    private void Awake()
    {
        // Ensure this GameObject persists across scene loads
        DontDestroyOnLoad(gameObject);
    }

    // --- Public Methods Called By UI (MainMenuController) ---

    /// <summary>
    /// Connects to the Photon Cloud region to fetch the session list (lobby).
    /// Doesn't join a specific game session yet.
    /// </summary>
    public async void ConnectToLobby()
    {
        if (_isStartingGame || (_runner != null && _runner.IsRunning))
        {
            Debug.LogWarning("Already connecting or connected.");
             // If already connected, maybe just force a session list update? Check Fusion docs/API.
             if(_runner != null && _runner.IsConnectedToServer) {
                 // Attempt to join lobby again might refresh or use specific API if available
                 await _runner.JoinSessionLobby(SessionLobby.ClientServer);
             }
            return;
        }

        ShutdownRunner(); // Ensure previous runner is cleaned up if exists
        _isStartingGame = true;
        Debug.Log("Connecting to Lobby...");

        // Create a new runner instance for lobby connection
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true; // We'll handle input later

        try
        {
            // Start game in a mode that connects to the cloud but doesn't load a scene
            // or join a specific session immediately. Joining the ClientServer lobby is typical.
            var result = await _runner.JoinSessionLobby(SessionLobby.ClientServer);

            if (result.Ok)
            {
                Debug.Log("Successfully joined Lobby.");
                // OnSessionListUpdated will be called automatically by Fusion after joining lobby
            }
            else
            {
                Debug.LogError($"Failed to join Lobby: {result.ShutdownReason}");
                ShutdownRunner(); // Clean up failed runner
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception during Lobby Connection: {e.Message}");
            ShutdownRunner();
        }
        finally
        {
             _isStartingGame = false;
        }
    }


    /// <summary>
    /// Starts a Fusion game session (Host or Client) with specific configuration.
    /// Handles scene loading via NetworkSceneManagerDefault.
    /// </summary>
    /// <param name="mode">Host or Client</param>
    /// <param name="sessionName">The specific room name to create or join</param>
    /// <param name="maxPlayers">Maximum players for this session</param>
    /// <param name="sessionProperties">Custom data for the session (map, resources, etc.)</param>
    public async Task StartConfiguredGame(SimulationModes mode, string sessionName, int maxPlayers, Dictionary<string, SessionProperty> sessionProperties = null)
    {
        if (_isStartingGame || (_runner != null && _runner.Mode == mode && _runner.SessionInfo.Name == sessionName))
        {
            Debug.LogWarning($"Already starting or running game '{sessionName}' in mode {mode}.");
            return;
        }

        ShutdownRunner(); // Ensure previous runner is cleaned up
        _isStartingGame = true;
        Debug.Log($"Starting configured game. Mode: {mode}, Session: {sessionName}, Max Players: {maxPlayers}");

        // --- Create the Fusion Runner ---
        _runner = gameObject.GetOrAddComponent<NetworkRunner>();
        //_runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        // --- Prepare Scene Info (Use the designated Game Scene Name) ---
        int sceneIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + _gameSceneName + ".unity"); // Assumes scene is in Assets/Scenes
        if (sceneIndex < 0)
        {
            Debug.LogError($"Game scene '{_gameSceneName}' not found or not in build settings! Add it via File > Build Settings.");
            ShutdownRunner();
             _isStartingGame = false;
            return; // Stop if scene is invalid
        }
        var scene = SceneRef.FromIndex(sceneIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (scene.IsValid)
        {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Single); // Load game scene exclusively
        }

        // --- Start the Game ---
        try
        {
            _runner.AddCallbacks(this); // Register this script for callbacks
            var result = await _runner.StartGame(new StartGameArgs()
            {
                GameMode = (Fusion.GameMode) mode,
                SessionName = sessionName,
                Scene = scene, // Scene to load
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(), // Handles loading the scene above
                PlayerCount = maxPlayers,
                SessionProperties = sessionProperties, // Pass custom data
            });

            if (result.Ok)
            {
                Debug.Log($"StartGame successful for session '{sessionName}'. Runner is running: {_runner.IsRunning}");
                _mainMenuController.gameObject.GetComponent<UIDocument>().enabled = false; // Disable UI Document to prevent interaction during loading
                // Scene loading is handled by NetworkSceneManagerDefault & OnSceneLoadDone/Start callbacks
            }
            else
            {
                Debug.LogError($"Failed to Start Game '{sessionName}': {result.ShutdownReason}");
                ShutdownRunner(); // Clean up failed runner
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception during StartGame: {e.Message}");
            Debug.LogError($"Runner Shutdown Trace: {e.StackTrace}");
            ShutdownRunner();
        }
        finally
        {
            _isStartingGame = false;
        }
    }

    /// <summary>
    /// Shuts down the current runner if it exists.
    /// </summary>
    public async void ShutdownRunner()
    {
        if (_runner != null && _runner.IsRunning)
        {
            Debug.Log("Shutting down existing runner...");
            await _runner.Shutdown(); // Graceful shutdown
        }
        // Clean up components even if shutdown wasn't graceful or runner wasn't running
        if (_runner != null)
        {
             Destroy(_runner.GetComponent<NetworkSceneManagerDefault>());
             Destroy(_runner);
             _runner = null;
        }
         _spawnedPlayers.Clear();
         _isStartingGame = false; // Reset flag
        
         Debug.Log("Runner shutdown complete. Returning to main menu.");
    }


    // --- INetworkRunnerCallbacks Implementation ---

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"Session List Updated. Found {sessionList.Count} sessions.");
        // --- Pass the list to the Main Menu Controller ---
        if (_mainMenuController != null)
        {
            // Filter list if needed (e.g., only show visible, open sessions)
            var filteredList = new List<SessionInfo>();
            foreach(var session in sessionList)
            {
                if(session.IsVisible && session.IsOpen)
                {
                    filteredList.Add(session);
                }
            }
            _mainMenuController.UpdateRoomListView(filteredList);
        }
        else
        {
            Debug.LogWarning("MainMenuController reference not set on NetworkManager. Cannot update UI room list.");
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player.PlayerId} Joined session {runner.SessionInfo.Name}");
        // This runs ONLY on the server/host (or Shared Mode master)
        if (runner.IsServer || runner.IsSharedModeMasterClient)
        {
            // Check if player prefab is assigned
            if (_playerPrefab == null)
            {
                Debug.LogError("Player Prefab is not assigned in NetworkManager!");
                return;
            }

            // Basic position calculation (replace with actual spawn logic)
            Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1, 0);

            // Spawn the player prefab
            NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
            Debug.Log($"Spawned Player Object for Player {player.PlayerId}");

            if (networkPlayerObject != null)
            {
                _spawnedPlayers.Add(player, networkPlayerObject);
            }
            else {
                 Debug.LogError($"Failed to spawn player object for Player {player.PlayerId}");
            }
            // --- Server also spawns a starting unit FOR the player ---
            // The player object doesn't move, the units do.
            // Give the UNIT Input Authority so the owning client can send commands for it.
            // NOTE: This is one way; alternatively, the PlayerController object could receive
            // all input and then issue RPCs to the server to control specific units.
            // Giving units Input Authority directly simplifies this basic example for commands.

            //Vector3 unitSpawnPos = spawnPosition + Vector3.forward * 2; // Offset slightly
            //NetworkObject unitNO = runner.Spawn(_unitPrefab, unitSpawnPos, Quaternion.identity, player); // Assign player authority to the unit
            //Debug.Log($"Spawned starting Unit {unitNO.Id} for player {player.PlayerId} with Input Authority");

            // You might want the PlayerController script to keep track of its units
            // playerControllerScript.AddUnit(unitNO); // If you add such a method
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player.PlayerId} Left");
        // Find and despawn the player object
        if (_spawnedPlayers.TryGetValue(player, out NetworkObject networkObject))
        {
            if (networkObject != null)
            {
                runner.Despawn(networkObject);
                 Debug.Log($"Despawned Player Object for Player {player.PlayerId}");
            }
            _spawnedPlayers.Remove(player);
        } else {
             Debug.LogWarning($"Player {player.PlayerId} left, but no spawned object found in dictionary.");
        }
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Runner Shutdown: {shutdownReason}");
        // Clear spawned players list
        _spawnedPlayers.Clear();
        _runner = null; // Clear runner reference
        _isStartingGame = false; // Reset flag

        // Return to main menu scene
        // Ensure we are not already in the menu scene to avoid loop
        if (SceneManager.GetActiveScene().name != "MainMenu") // <<< USE YOUR ACTUAL MENU SCENE NAME
        {
             SceneManager.LoadScene("MainMenu"); // <<< USE YOUR ACTUAL MENU SCENE NAME
        } else {
             // If already in menu, maybe refresh UI state?
             if(_mainMenuController != null) 
             {
                _mainMenuController.gameObject.GetComponent<UIDocument>().enabled = true;
                _mainMenuController.ShowPanel(_mainMenuController.mainMenuPanel); // Show main menu panel again
             }
        }
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log($"Connected to Server. Session: {runner.SessionInfo.Name}");
        // If you have logic that needs to run *after* connecting but *before* player spawning/scene load, it goes here.
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from Server: {reason}");
        // Usually followed by OnShutdown
        ShutdownRunner(); // Explicitly call shutdown cleanup just in case
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {
         Debug.Log($"Incoming connection request...");
         // You can add logic here to accept/reject connections based on the token or other criteria
         // request.Accept(); // Accept the connection
         // request.Refuse(); // Refuse the connection
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"Connect Failed: {reason}");
        // Inform the UI?
        // if(_mainMenuController != null) _mainMenuController.ShowConnectionError($"Connection Failed: {reason}");
        ShutdownRunner(); // Clean up failed runner attempt
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"Game Scene '{_gameSceneName}' Load Done.");
        // Scene is loaded, game is ready to proceed. Player spawning happens around/after this.
        // If the Main Menu UI is still visible somehow, hide it now.
         if(_mainMenuController != null) _mainMenuController.HideAllPanels(); // Hide UI completely
    }
    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log($"Game Scene '{_gameSceneName}' Load Start...");
         // Optionally show a loading screen here
    }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnPlayerMaxCountExceeded(NetworkRunner runner, PlayerRef player, int maxCount){ Debug.LogWarning($"Player {player.PlayerId} rejected: Max player count {maxCount} exceeded."); }

    // Helper in NetworkManager.cs:
    // public bool TryGetUnitPrefab(out NetworkPrefabRef prefabRef)
    // {
    //     // Ensure _unitPrefab is assigned in the inspector!
    //     if (_unitPrefab != NetworkPrefabRef.Empty) {
    //         prefabRef = _unitPrefab;
    //         return true;
    //     }
    //     prefabRef = default;
    //     return false;
    // }
}