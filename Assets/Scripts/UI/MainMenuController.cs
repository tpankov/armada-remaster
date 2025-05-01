using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Fusion; // Add Fusion namespace
//using Unity.VisualScripting; // Add Fusion Sockets namespace

public class MainMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManager networkManager; // <<< ASSIGN NetworkManager GameObject IN INSPECTOR

    // Panel references (Make public if needed by NetworkManager, e.g., for error messages or hiding)
    [HideInInspector] public VisualElement mainMenuPanel;
    [HideInInspector] public VisualElement singlePlayerSetupPanel;
    [HideInInspector] public VisualElement multiplayerLobbyPanel;
    [HideInInspector] public VisualElement multiplayerRoomConfigPanel;
    [HideInInspector] public VisualElement settingsPanel;
    [HideInInspector] public VisualElement mapEditorPanel;

    private VisualElement root;

    // ... (Keep your other UI Element References: Buttons, Sliders, Dropdowns etc.) ...
    // ... (Single Player Setup) ...
    private DropdownField factionDropdownSP;
    private TextField resourcesInputSP;
    private DropdownField mapDropdownSP;
    private DropdownField difficultyDropdownSP;
    private Button startSPButton;
    private Button backButtonSP;
    // ... (MP Lobby) ...
    private Button refreshRoomsButton;
    private Button createRoomButton;
    private ScrollView roomListScrollView;
    private Button backButtonMPLobby;
    // ... (MP Config) ...
    private TextField roomNameInput;
    private DropdownField factionDropdownMP;
    private TextField resourcesInputMP;
    private DropdownField mapDropdownMP;
    private DropdownField maxPlayersDropdownMP;
    private Button confirmCreateRoomButton;
    private Button backButtonMPConfig;
    // ... (Settings) ...
    private DropdownField graphicsDropdown;
    private DropdownField resolutionDropdown;
    private Toggle fullscreenToggle;
    private Slider masterVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private Button applySettingsButton;
    private Button backButtonSettings;
    // ... (Map Editor) ...
    private Button backButtonMapEditor;
    // ... (Main Menu) ...
     private Button singlePlayerButton;
    private Button multiplayerButton;
    private Button settingsButton;
    private Button mapEditorButton;
    private Button quitButton;

    private void Awake()
    {
        // Ensure this GameObject persists across scene loads
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) { Debug.LogError("MainMenuController: No UIDocument found!"); return; }
        root = uiDocument.rootVisualElement;
        if (root == null) { Debug.LogError("MainMenuController: UIDocument has no rootVisualElement!"); return; }

        // --- Check NetworkManager Reference ---
        if (networkManager == null) { Debug.LogError("MainMenuController: NetworkManager reference not set in Inspector!"); }


        // --- Query Panels (Assign to public fields) ---
        mainMenuPanel = root.Q<VisualElement>("MainMenuPanel");
        singlePlayerSetupPanel = root.Q<VisualElement>("SinglePlayerSetupPanel");
        multiplayerLobbyPanel = root.Q<VisualElement>("MultiplayerLobbyPanel");
        multiplayerRoomConfigPanel = root.Q<VisualElement>("MultiplayerRoomConfigPanel");
        settingsPanel = root.Q<VisualElement>("SettingsPanel");
        mapEditorPanel = root.Q<VisualElement>("MapEditorPanel");


        // --- Query All UI Elements (as before) ---
        singlePlayerButton = root.Q<Button>("singleplayer-button");
        multiplayerButton = root.Q<Button>("multiplayer-button");
        settingsButton = root.Q<Button>("settings-button");
        mapEditorButton = root.Q<Button>("mapeditor-button");
        quitButton = root.Q<Button>("quit-button");
        factionDropdownSP = singlePlayerSetupPanel?.Q<DropdownField>("faction-dropdown-sp");
        resourcesInputSP = singlePlayerSetupPanel?.Q<TextField>("resources-input-sp");
        mapDropdownSP = singlePlayerSetupPanel?.Q<DropdownField>("map-dropdown-sp");
        difficultyDropdownSP = singlePlayerSetupPanel?.Q<DropdownField>("difficulty-dropdown-sp");
        startSPButton = singlePlayerSetupPanel?.Q<Button>("start-sp-button");
        backButtonSP = singlePlayerSetupPanel?.Q<Button>("back-button-sp");
        refreshRoomsButton = multiplayerLobbyPanel?.Q<Button>("refresh-rooms-button");
        createRoomButton = multiplayerLobbyPanel?.Q<Button>("create-room-button");
        roomListScrollView = multiplayerLobbyPanel?.Q<ScrollView>("room-list-scrollview");
        backButtonMPLobby = multiplayerLobbyPanel?.Q<Button>("back-button-mplobby");
        roomNameInput = multiplayerRoomConfigPanel?.Q<TextField>("room-name-input");
        factionDropdownMP = multiplayerRoomConfigPanel?.Q<DropdownField>("faction-dropdown-mp");
        resourcesInputMP = multiplayerRoomConfigPanel?.Q<TextField>("resources-input-mp");
        mapDropdownMP = multiplayerRoomConfigPanel?.Q<DropdownField>("map-dropdown-mp");
        maxPlayersDropdownMP = multiplayerRoomConfigPanel?.Q<DropdownField>("max-players-dropdown-mp");
        confirmCreateRoomButton = multiplayerRoomConfigPanel?.Q<Button>("confirm-create-room-button");
        backButtonMPConfig = multiplayerRoomConfigPanel?.Q<Button>("back-button-mpconfig");
        graphicsDropdown = settingsPanel?.Q<DropdownField>("graphics-dropdown");
        resolutionDropdown = settingsPanel?.Q<DropdownField>("resolution-dropdown");
        fullscreenToggle = settingsPanel?.Q<Toggle>("fullscreen-toggle");
        masterVolumeSlider = settingsPanel?.Q<Slider>("master-volume-slider");
        musicVolumeSlider = settingsPanel?.Q<Slider>("music-volume-slider");
        sfxVolumeSlider = settingsPanel?.Q<Slider>("sfx-volume-slider");
        applySettingsButton = settingsPanel?.Q<Button>("apply-settings-button");
        backButtonSettings = settingsPanel?.Q<Button>("back-button-settings");
        backButtonMapEditor = mapEditorPanel?.Q<Button>("back-button-mapeditor");


        // --- Register Callbac ks (UPDATED for NetworkManager) ---
        // Main Menu
        singlePlayerButton?.RegisterCallback<ClickEvent>(ev => ShowPanel(singlePlayerSetupPanel));
        multiplayerButton?.RegisterCallback<ClickEvent>(ev => ShowMultiplayerLobby()); // Calls NM.ConnectToLobby
        settingsButton?.RegisterCallback<ClickEvent>(ev => ShowSettings());
        mapEditorButton?.RegisterCallback<ClickEvent>(ev => LoadMapEditor());
        quitButton?.RegisterCallback<ClickEvent>(ev => QuitGame());

        // SP Setup
        startSPButton?.RegisterCallback<ClickEvent>(ev => CreateSinglePlayerRoom()); // No change here
        backButtonSP?.RegisterCallback<ClickEvent>(ev => ShowPanel(mainMenuPanel));

        // MP Lobby
        refreshRoomsButton?.RegisterCallback<ClickEvent>(ev => networkManager?.ConnectToLobby()); // Re-connect/refresh
        createRoomButton?.RegisterCallback<ClickEvent>(ev => ShowPanel(multiplayerRoomConfigPanel)); // Show config panel
        backButtonMPLobby?.RegisterCallback<ClickEvent>(ev => HandleBackFromLobby()); // Disconnect/Shutdown runner

        // MP Config
        confirmCreateRoomButton?.RegisterCallback<ClickEvent>(ev => CreateMultiplayerRoom()); // Calls NM.StartConfiguredGame(Host)
        backButtonMPConfig?.RegisterCallback<ClickEvent>(ev => ShowPanel(multiplayerLobbyPanel)); // Back to lobby view

        // Settings
        applySettingsButton?.RegisterCallback<ClickEvent>(ev => ApplySettings());
        backButtonSettings?.RegisterCallback<ClickEvent>(ev => ShowPanel(mainMenuPanel));

        // Map Editor
        backButtonMapEditor?.RegisterCallback<ClickEvent>(ev => ShowPanel(mainMenuPanel));


        // --- Initial State ---
        HideAllPanels(); // Hide all panels at start
        PopulateStaticDropdowns();
        ShowPanel(mainMenuPanel); // Show only main menu
    }

    // --- Panel Navigation ---
    public void ShowPanel(VisualElement panelToShow) // Make public if NM needs to call it
    {
        if (root == null || panelToShow == null) return;
        HideAllPanels(); // Hide all first
        panelToShow.RemoveFromClassList("hidden"); // Show target
    }

    // --- Hides all menu panels (e.g., when game starts) ---
    public void HideAllPanels()
    {
        mainMenuPanel?.AddToClassList("hidden");
        singlePlayerSetupPanel?.AddToClassList("hidden");
        multiplayerLobbyPanel?.AddToClassList("hidden");
        multiplayerRoomConfigPanel?.AddToClassList("hidden");
        settingsPanel?.AddToClassList("hidden");
        mapEditorPanel?.AddToClassList("hidden");
    }

    // --- Data Population (No changes needed here) ---
    // ... Keep PopulateStaticDropdowns, PopulateGraphicsDropdown, PopulateResolutionDropdown ...
     private void PopulateStaticDropdowns()
    {
        List<string> factions = new List<string> { "Federation", "Klingon", "Romulan", "Cardassian" };
        List<string> maps = new List<string> { "Map Alpha", "Sector 001", "Desert Planet", "Ice World" };
        List<string> difficulties = new List<string> { "Easy", "Normal", "Hard", "Brutal" };
        List<string> playerCounts = new List<string> { "2", "3", "4", "5", "6", "7", "8" };

        factionDropdownSP?.choices.AddRange(factions); mapDropdownSP?.choices.AddRange(maps); difficultyDropdownSP?.choices.AddRange(difficulties);
        factionDropdownMP?.choices.AddRange(factions); mapDropdownMP?.choices.AddRange(maps); maxPlayersDropdownMP?.choices.AddRange(playerCounts);
        PopulateGraphicsDropdown(); PopulateResolutionDropdown();
    }
     private void PopulateGraphicsDropdown() 
     {
        if (graphicsDropdown == null) return; 
        graphicsDropdown.choices = new List<string>(QualitySettings.names); 
        graphicsDropdown.index = QualitySettings.GetQualityLevel(); 
    }
     private void PopulateResolutionDropdown() 
     {
         if (resolutionDropdown == null) return;
        var resolutions = Screen.resolutions; var options = new List<string>(); int currentResIndex = 0;
        for(int i=0; i < resolutions.Length; i++){ string option = resolutions[i].width + " x " + resolutions[i].height + " @ " + resolutions[i].refreshRateRatio + "Hz"; options.Add(option); if (resolutions[i].width == Screen.currentResolution.width && resolutions[i].height == Screen.currentResolution.height) { currentResIndex = i; }}
        resolutionDropdown.choices = options; resolutionDropdown.index = currentResIndex; 
    }

     private void ShowMultiplayerLobby()
    {
        // Show the panel FIRST, then attempt connection. NM handles feedback/updates.
        ShowPanel(multiplayerLobbyPanel);
        networkManager?.ConnectToLobby(); // Ask NetworkManager to connect
    }

    // Called when pressing Back from the Multiplayer Lobby
    private void HandleBackFromLobby()
    {
        networkManager?.ShutdownRunner(); // Disconnect / shut down runner when leaving lobby
        ShowPanel(mainMenuPanel); // Go back to main menu
    }


    // --- Public method called by NetworkManager ---
    public void UpdateRoomListView(List<SessionInfo> sessionList)
    {
        if (roomListScrollView == null) return;
        var contentContainer = roomListScrollView.contentContainer;
        contentContainer.Clear();

        if (sessionList == null || sessionList.Count == 0)
        {
            contentContainer.Add(new Label("No open rooms found."));
            return;
        }

        Debug.Log($"UI: Updating room list with {sessionList.Count} sessions.");

        foreach (var session in sessionList)
        {
            var roomEntry = new VisualElement();
            roomEntry.AddToClassList("room-entry");

            // Try to get custom properties (example: map name) - Use default if not found
            string mapName = "Default Map";
            if(session.Properties.TryGetValue("map", out var mapProp)) {
                 mapName = mapProp.ToString(); // Assuming map name stored as string property
            }

            var infoLabel = new Label($"{session.Name} [{mapName}] ({session.PlayerCount}/{session.MaxPlayers})");
            var joinButton = new Button(() => JoinMultiplayerRoom(session.Name))
            {
                text = "Join", name = session.Name
            };
            joinButton.AddToClassList("join-button");
            joinButton.AddToClassList("pill-button");

            roomEntry.Add(infoLabel);
            roomEntry.Add(joinButton);
            contentContainer.Add(roomEntry);
        }
    }

    private async void CreateSinglePlayerRoom()
    {
        if (networkManager == null) return;
        string roomName = "SinglePlayerRoom"; // Use a default name or generate one
        string faction = factionDropdownSP?.value; // Host's chosen faction (store in Player Prefs or Networked Var later?)
        string map = mapDropdownSP?.value;
        string difficulty = difficultyDropdownSP?.value; // Example: Store difficulty level
        int maxPlayers = 1; // Single player, so max players is 1
        int resources = 0; int.TryParse(resourcesInputSP?.value, out resources);

        Debug.Log($"UI: Requesting Create Room: Name={roomName}, Faction={faction}, Map={map}, Players={maxPlayers}, Resources={resources}");

        // --- Prepare Session Properties ---
        var sessionProps = new Dictionary<string, SessionProperty> {
            {"faction", faction}, // Example: Store faction name
            { "difficulty", difficulty }, // Example: Store difficulty level
            { "map", map },             // Example: Store map name
            { "res", resources },       // Example: Store starting resources
            // Add other game rules/settings as needed
        };

        // Show Loading indicator?
        // loadingIndicator.style.display = DisplayStyle.Flex;

        // Call Network Manager to start hosting
        await networkManager.StartConfiguredGame(SimulationModes.Host, roomName, maxPlayers, sessionProps);

        // Hide loading indicator? (Might happen automatically on scene change or NM callback)
        // loadingIndicator.style.display = DisplayStyle.None;
    }

    private async void CreateMultiplayerRoom() // Make async? StartConfiguredGame is async
    {
        if (networkManager == null) return;

        string roomName = roomNameInput?.value;
        string faction = factionDropdownMP?.value; // Host's chosen faction (store in Player Prefs or Networked Var later?)
        string difficulty = "Normal";
        string map = mapDropdownMP?.value;
        int maxPlayers = int.Parse(maxPlayersDropdownMP?.value ?? "4");
        int resources = 0; int.TryParse(resourcesInputMP?.value, out resources);

        if (string.IsNullOrWhiteSpace(roomName)) { Debug.LogError("Room name cannot be empty!"); return; }

        Debug.Log($"UI: Requesting Create Room: Name={roomName}, Faction={faction}, Map={map}, Players={maxPlayers}, Resources={resources}");

        // --- Prepare Session Properties ---
        var sessionProps = new Dictionary<string, SessionProperty> {
            {"faction", faction}, // Example: Store faction name
            { "difficulty", difficulty }, // Example: Store difficulty level
            { "map", map },             // Example: Store map name
            { "res", resources },       // Example: Store starting resources
            // Add other game rules/settings as needed
        };

        // Show Loading indicator?
        // loadingIndicator.style.display = DisplayStyle.Flex;

        // Call Network Manager to start hosting
        await networkManager.StartConfiguredGame(SimulationModes.Host, roomName, maxPlayers, sessionProps);

        // Hide loading indicator? (Might happen automatically on scene change or NM callback)
        // loadingIndicator.style.display = DisplayStyle.None;
    }

     private async void JoinMultiplayerRoom(string sessionName) // Make async?
    {
         if (networkManager == null) return;
         Debug.Log($"UI: Requesting Join Room: {sessionName}");
         // Show Loading indicator?
         await networkManager.StartConfiguredGame(SimulationModes.Client, sessionName, 0, null); // MaxPlayers & Props irrelevant for client join
         // Hide loading indicator?
    }


    private void LoadMapEditor()
    {
        SceneManager.LoadScene("MapEditorScene"); // <<< REPLACE with your map editor scene if different
    }

    // --- Settings (No changes needed here) ---
    // ... Keep LoadSettings, ShowSettings, ApplySettings ...
     private void LoadSettings() { /* ...as before... */
          graphicsDropdown.index = PlayerPrefs.GetInt("GraphicsQuality", QualitySettings.GetQualityLevel());
          fullscreenToggle.value = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
          masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
          musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
          sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.9f); }
     private void ShowSettings() { LoadSettings(); ShowPanel(settingsPanel); }
     private void ApplySettings()
     {
        Debug.Log("Applying Settings...");
        // Apply Graphics
        QualitySettings.SetQualityLevel(graphicsDropdown.index, true);
        // Apply Resolution and Fullscreen
        string selectedRes = resolutionDropdown.value; // "Width x Height @ RefreshHz"
        // Parse the resolution string... (more robust parsing needed)
        // Screen.SetResolution(width, height, fullscreenToggle.value);
        Screen.fullScreen = fullscreenToggle.value;

        // Apply Audio (Using AudioMixer is recommended)
        // Example: Assuming you have an AudioMixer with exposed parameters
        // audioMixer.SetFloat("MasterVolume", Mathf.Log10(masterVolumeSlider.value) * 20); // Convert linear to dB
        // audioMixer.SetFloat("MusicVolume", Mathf.Log10(musicVolumeSlider.value) * 20);
        // audioMixer.SetFloat("SFXVolume", Mathf.Log10(sfxVolumeSlider.value) * 20);
        Debug.Log($"Applying Volume - Master: {masterVolumeSlider.value}"); // Replace with actual application

        // Save Settings
        PlayerPrefs.SetInt("GraphicsQuality", graphicsDropdown.index);
        // Save Resolution (store index or dimensions string)
        PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.value ? 1 : 0);
        PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolumeSlider.value);
        PlayerPrefs.Save();

        Debug.Log("Settings Applied and Saved.");
        ShowPanel(mainMenuPanel); // Go back to main menu after applying
    }


    private void QuitGame()
    {
        Debug.Log("Quitting Application...");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Exit play mode in editor
        #endif
    }
}