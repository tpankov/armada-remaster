using UnityEngine;
using UnityEngine.UIElements; // UI Toolkit namespace
using Fusion;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages the UI Toolkit context menu for selected units.
/// Handles positioning, dynamic element creation (buttons, icons), data binding,
/// and user interactions, routing commands back to PlayerInputHandler.
/// </summary>
public class ContextMenuController_UIToolkit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument; // Assign the UIDocument component for this menu
    [SerializeField] private StyleSheet styleSheet; // Assign your ContextMenuStyles.uss asset
    [SerializeField] private PlayerInputHandler playerInputHandler; // Assign or find reference
    [SerializeField] private Camera mainCamera;

    [Header("Configuration")]
    [SerializeField] private float commandButtonRadius = 100f; // Distance from center for command buttons
    [SerializeField] private float shipIconRadius = 100f;    // Distance from center for ship icons
    [SerializeField] private Vector2 commandArc = new Vector2(270, 90); // Start/End angles for command buttons (degrees, 0=right)
    [SerializeField] private Vector2 shipIconArc = new Vector2(90, 270); // Start/End angles for ship icons

    // --- Icon Mapping (Example - Use a more robust system like ScriptableObjects) ---
    [Header("Asset References (Example)")]
    [SerializeField] private Texture2D repairIcon;
    [SerializeField] private Texture2D stopIcon;
    [SerializeField] private Texture2D attackMoveIcon;
    [SerializeField] private Texture2D defaultShipIcon;
    [SerializeField] private Texture2D engineSystemIcon;
    [SerializeField] private Texture2D weaponSystemIcon;
    [SerializeField] private Texture2D shieldSystemIcon;

    [SerializeField] private Texture2D lifeSystemIcon;
    [SerializeField] private Texture2D sensordSystemIcon;
    [SerializeField] private Texture2D commSystemIcon;
    [SerializeField] private Texture2D powerSystemIcon;
    [SerializeField] private Texture2D powerDrawIcon;
    [SerializeField] private Texture2D moraleIcon;
    [SerializeField] private Texture2D suppliesIcon;



    // --- UI Element References ---
    private VisualElement _root;
    private VisualElement _commandButtonContainer;
    private VisualElement _shipIconContainer;
    private VisualElement _centerFocusElement; // Optional: If you have a distinct center element
    private VisualElement _topStatusBarsContainer;
    private VisualElement _bottomStatusBarsContainer;
    private VisualElement _leftSystemIconsContainer;
    private VisualElement _rightSystemIconsContainer;

    // Example Status Bar Elements (assuming structure defined or created)
    private VisualElement _healthBarFill;
    private VisualElement _shieldBarFill;
    private VisualElement _progressBarFill; 

    // --- State ---
    private bool _isVisible = false;
    private NetworkId _currentTargetUnitId;
    private HashSet<NetworkId> _currentSelectionRef;
    private UnitController _currentTargetController;
    private NetworkRunner _runnerRef;

    // Keep track of generated elements for clearing
    private List<Button> _generatedCommandButtons = new List<Button>();
    private List<VisualElement> _generatedShipIcons = new List<VisualElement>();
    private List<VisualElement> _generatedSystemIcons = new List<VisualElement>();
    private List<VisualElement> _generatedStatusBars = new List<VisualElement>();


    void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (playerInputHandler == null) playerInputHandler = FindFirstObjectByType<PlayerInputHandler>();
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("ContextMenuController_UIToolkit requires a UIDocument component.", this);
            enabled = false;
            return;
        }
        if (styleSheet == null)
        {
            Debug.LogWarning("ContextMenuStyles StyleSheet not assigned.", this);
        }
    }

    void OnEnable()
    {
        // It's often better to get the root element in OnEnable after the UIDocument might be ready
        InitializeUIReferences();
    }

    void InitializeUIReferences()
    {
         if (uiDocument == null) return;
         _root = uiDocument.rootVisualElement;

         if (_root == null) {
             Debug.LogError("UIDocument rootVisualElement is null.", this);
             enabled = false;
             return;
         }

         // Apply Stylesheet
         if (styleSheet != null)
            _root.styleSheets.Add(styleSheet);
         else
             Debug.LogWarning("StyleSheet is null, UI might not be styled correctly.");


        // --- Query for container elements (assuming names set in UXML or created here) ---
        // If not using UXML, you'd create these containers here dynamically.
        _commandButtonContainer = _root.Q<VisualElement>("command-button-container");
        _shipIconContainer = _root.Q<VisualElement>("ship-icon-container");
        _centerFocusElement = _root.Q<VisualElement>("center-focus"); // Optional
        _topStatusBarsContainer = _root.Q<VisualElement>("top-status-bars");
        _bottomStatusBarsContainer = _root.Q<VisualElement>("bottom-status-bars");
        _leftSystemIconsContainer = _root.Q<VisualElement>("left-system-icons");
        _rightSystemIconsContainer = _root.Q<VisualElement>("right-system-icons");


        // Example: Query for specific status bar fill elements if defined in UXML
        _healthBarFill = _root.Q<VisualElement>("health-bar-fill");
        _shieldBarFill = _root.Q<VisualElement>("shield-bar-fill");
        _progressBarFill = _root.Q<VisualElement>("progress-bar-fill");

        // Ensure containers exist if not found (basic dynamic creation)
        if (_commandButtonContainer == null) { _commandButtonContainer = new VisualElement() { name = "command-button-container" }; _root.Add(_commandButtonContainer); _commandButtonContainer.AddToClassList("command-button-container"); }
        if (_shipIconContainer == null) { _shipIconContainer = new VisualElement() { name = "ship-icon-container" }; _root.Add(_shipIconContainer); _shipIconContainer.AddToClassList("ship-icon-container"); }
        if (_topStatusBarsContainer == null) { _topStatusBarsContainer = new VisualElement() { name = "top-status-bars" }; _root.Add(_topStatusBarsContainer); _topStatusBarsContainer.AddToClassList("top-status-bars"); }
        // ... create other containers dynamically if needed ...


        // Hide initially
        _root.style.display = DisplayStyle.None;
        _isVisible = false;
    }


    void Update()
    {
        if (!_isVisible || _currentTargetController == null || _runnerRef == null || _root == null || _root.style.display == DisplayStyle.None)
        {
            return;
        }

        // --- Update Position ---
        PositionContextMenu();

        // --- Update Data Display ---
        UpdateStatusInfo(); // Health, shields, system icons

        // --- Handle Context Menu Shortcuts ---
        HandleShortcuts();
    }

    void PositionContextMenu()
    {
        if (mainCamera == null) return;

        // Convert world position to UI Toolkit panel position
        Vector2 panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(
            _root.panel, // The panel the UI is rendered in
            _currentTargetController.transform.position,
            mainCamera
        );

        // Check if target is behind camera (panel position might be unreliable)
        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(_currentTargetController.transform.position);
        if (viewportPoint.z < 0)
        {
            HideMenu(); // Hide if target goes behind camera
            return;
        }

        // Apply position - UI Toolkit uses layout relative to parent by default.
        // For world-space tracking, setting absolute position and using left/top is common.
        // The translate transform (-50%, -50%) in USS helps center it.
        _root.style.left = panelPosition.x;
        _root.style.top = panelPosition.y;
    }

    void UpdateStatusInfo()
    {
        // --- Update Bars ---
        // Example: Health Bar (Create elements dynamically if not in UXML)
        if (_healthBarFill == null && _topStatusBarsContainer != null) // Create dynamically if needed
        {
             var healthBar = new VisualElement(); healthBar.AddToClassList("status-bar");
             _healthBarFill = new VisualElement(); _healthBarFill.AddToClassList("status-bar-fill"); _healthBarFill.AddToClassList("health-bar-fill");
             healthBar.Add(_healthBarFill);
             _topStatusBarsContainer.Add(healthBar); // Add to appropriate container
             _generatedStatusBars.Add(healthBar); // Track for clearing
        }
        if (_healthBarFill != null)
        {
            float maxHealth = _currentTargetController.maxHealth; // Assumes MaxHealth exists
            float fill = (maxHealth > 0) ? (_currentTargetController.NetworkedHealth / maxHealth) : 0;
            _healthBarFill.style.width = Length.Percent(Mathf.Clamp01(fill) * 100f);
        }


        // Example: Shield Bar (similar creation/update logic)
        if (_shieldBarFill == null && _topStatusBarsContainer != null) 
        {
             var shieldBar = new VisualElement(); shieldBar.AddToClassList("status-bar");
             _shieldBarFill = new VisualElement(); _shieldBarFill.AddToClassList("status-bar-fill"); _shieldBarFill.AddToClassList("shield-bar-fill");
             shieldBar.Add(_shieldBarFill);
             _topStatusBarsContainer.Add(shieldBar); // Add to appropriate container
             _generatedStatusBars.Add(shieldBar); // Track for clearing
        }
        // Update fill amount based on current target controller state
        if (_shieldBarFill != null) 
        {
            float maxShields = _currentTargetController.maxShields; // Assumes MaxShields exists
            float fill = (maxShields > 0) ? (_currentTargetController.NetworkedShields / maxShields) : 0;
            _shieldBarFill.style.width = Length.Percent(Mathf.Clamp01(fill) * 100f);
        }

        // Example: Progress Bar (similar creation/update logic)
        if (_progressBarFill == null && _bottomStatusBarsContainer != null)
        {
             var progressBar = new VisualElement(); progressBar.AddToClassList("status-bar");
             _progressBarFill = new VisualElement(); _progressBarFill.AddToClassList("status-bar-fill"); _progressBarFill.AddToClassList("progress-bar-fill");
             progressBar.Add(_progressBarFill);
             _bottomStatusBarsContainer.Add(progressBar); // Add to appropriate container
             _generatedStatusBars.Add(progressBar); // Track for clearing
        }

        // --- Update System Icons ---
        // Clear previous dynamic icons before regenerating
        ClearSystemIcons();
        // Example: Get system status from UnitController (needs methods/properties there)
        // Add icons dynamically based on status
        AddSystemIcon(_leftSystemIconsContainer, shieldSystemIcon, _currentTargetController.GetShieldSystemStatus());
        AddSystemIcon(_leftSystemIconsContainer, engineSystemIcon, _currentTargetController.GetEngineSystemStatus());
        AddSystemIcon(_leftSystemIconsContainer, lifeSystemIcon, _currentTargetController.GetLifeSupportStatus());
        AddSystemIcon(_leftSystemIconsContainer, sensordSystemIcon, _currentTargetController.GetSensorSystemStatus());
        // AddSystemIcon(_leftSystemIconsContainer, commSystemIcon, _currentTargetController.GetCommStatus());
        // AddSystemIcon(_leftSystemIconsContainer, weaponSystemIcon, _currentTargetController.GetWeaponStatus());
        // AddSystemIcon(_leftSystemIconsContainer, powerSystemIcon, _currentTargetController.GetPowerStatus());

        AddSystemIcon(_rightSystemIconsContainer, moraleIcon, _currentTargetController.GetMoraleStatus());
        AddSystemIcon(_rightSystemIconsContainer, suppliesIcon, _currentTargetController.GetSuppliesStatus());
        AddSystemIcon(_rightSystemIconsContainer, powerDrawIcon, _currentTargetController.GetPowerDrawStatus());

    }

    void HandleShortcuts()
    {
        // Check for relevant key presses ONLY when the menu is visible
        if (Input.GetKeyDown(KeyCode.R)) { OnCommandAction(NetworkInputData.COMMAND_REPAIR); }
        if (Input.GetKeyDown(KeyCode.S)) { OnCommandAction(NetworkInputData.COMMAND_STOP); } // Example 'S' for Stop
        // Add F1 etc.
    }


    // --- Dynamic Element Generation ---

    void GenerateCommandButtons()
    {
        ClearCommandButtons(); // Clear previous buttons

        // TODO: Get available commands based on _currentTargetController type/state
        List<byte> availableCommands = new List<byte> { NetworkInputData.COMMAND_REPAIR, NetworkInputData.COMMAND_STOP /*, NetworkInputData.COMMAND_ATTACK_MOVE */ }; // Example

        int count = availableCommands.Count;
        for (int i = 0; i < count; i++)
        {
            byte commandType = availableCommands[i];
            Button button = new Button();
            button.AddToClassList("circle-button");
            button.AddToClassList("command-button");

            // Add icon
            var icon = new VisualElement();
            icon.AddToClassList("button-icon");
            icon.style.backgroundImage = GetIconForCommand(commandType); // Helper to get Texture2D
            button.Add(icon);

            // Add border color (example)
            button.AddToClassList(GetBorderClassForCommand(commandType)); // Helper

            // Calculate position
            Vector2 pos = CalculateCircularPosition(i, count, commandButtonRadius, commandArc.x, commandArc.y);
            button.style.left = Length.Percent(50 + pos.x / (commandButtonRadius*2) * 50); // Rough percentage positioning
            button.style.top = Length.Percent(50 + pos.y / (commandButtonRadius*2) * 50);
            button.style.translate = new Translate(-50, -50); // Center button on position

            // Register callback
            button.clicked += () => OnCommandAction(commandType);

            _commandButtonContainer.Add(button);
            _generatedCommandButtons.Add(button);
        }
    }

    void GenerateShipIcons()
    {
        ClearShipIcons(); // Clear previous icons

        if (_currentSelectionRef == null) return;

        List<NetworkId> otherShips = _currentSelectionRef.Where(id => id != _currentTargetUnitId && id.IsValid).ToList();
        int count = otherShips.Count;
        // Limit number of icons shown? Example limit:
        int maxIconsToShow = 8;
        count = Mathf.Min(count, maxIconsToShow);

        for (int i = 0; i < count; i++)
        {
            NetworkId shipId = otherShips[i];
            VisualElement iconElement = new VisualElement(); // Use VE, make Button if clickable
            iconElement.AddToClassList("circle-button"); // Use same base style
            iconElement.AddToClassList("ship-icon");

            var icon = new VisualElement();
            icon.AddToClassList("button-icon");
            // TODO: Get icon based on ship type
            // icon.style.backgroundImage = GetIconForShipType(shipId);
            icon.style.backgroundImage = defaultShipIcon; // Placeholder
            iconElement.Add(icon);

            // Calculate position on the bottom arc
            Vector2 pos = CalculateCircularPosition(i, count, shipIconRadius, shipIconArc.x, shipIconArc.y);
            iconElement.style.left = Length.Percent(50 + pos.x / (shipIconRadius*2) * 50);
            iconElement.style.top = Length.Percent(50 + pos.y / (shipIconRadius*2) * 50);
            iconElement.style.translate = new Translate(-50, -50);

            // Optional: Add click handler to select this specific ship
            // iconElement.RegisterCallback<ClickEvent>(evt => OnShipIconClicked(shipId));

            _shipIconContainer.Add(iconElement);
            _generatedShipIcons.Add(iconElement);
        }
    }

    void AddSystemIcon(VisualElement container, Texture2D iconTexture, StarshipBase.SystemStatus status) // Assuming SystemStatus enum
    {
         if (container == null || iconTexture == null) return;

         var iconElement = new VisualElement();
         iconElement.AddToClassList("system-icon");
         iconElement.style.backgroundImage = iconTexture;

         // Apply status class
         switch(status)
         {
             case StarshipBase.SystemStatus.Online: iconElement.AddToClassList("system-online"); break;
             case StarshipBase.SystemStatus.Damaged: iconElement.AddToClassList("system-damaged"); break;
             case StarshipBase.SystemStatus.Offline: iconElement.AddToClassList("system-offline"); break;
         }

         container.Add(iconElement);
         _generatedSystemIcons.Add(iconElement);
    }

    // --- Clearing Dynamic Elements ---
    void ClearCommandButtons() { _generatedCommandButtons.ForEach(b => b.RemoveFromHierarchy()); _generatedCommandButtons.Clear(); }
    void ClearShipIcons() { _generatedShipIcons.ForEach(i => i.RemoveFromHierarchy()); _generatedShipIcons.Clear(); }
    void ClearSystemIcons() { _generatedSystemIcons.ForEach(i => i.RemoveFromHierarchy()); _generatedSystemIcons.Clear(); }
    void ClearStatusBars() { _generatedStatusBars.ForEach(b => b.RemoveFromHierarchy()); _generatedStatusBars.Clear(); } // If bars are dynamic

    // --- Action Handling ---
    void OnCommandAction(byte commandType)
    {
        if (!_isVisible || playerInputHandler == null || _currentSelectionRef == null) return;
        Debug.Log($"UI Action Triggered: CommandType={commandType}");

        // Create command (similar to UGUI version)
        var command = new PendingCommand
        {
            CommandType = commandType,
            SelectedUnitIds = _currentSelectionRef.ToArray(),
            TargetPosition = _currentTargetController != null ? _currentTargetController.transform.position : Vector3.zero, // Adjust as needed
            TargetObjectId = _currentTargetUnitId // Adjust as needed
        };

        playerInputHandler.QueueCommandFromUI(command);
        // HideMenu(); // Optionally hide after action
    }

    // --- Public Show/Hide ---
    public void ShowMenu(NetworkRunner runner, NetworkId targetUnitId, HashSet<NetworkId> currentSelection)
    {
        if (_root == null || runner == null || !targetUnitId.IsValid) { HideMenu(); return; }

        _runnerRef = runner;

        // Try find controller
        if (runner.TryFindObject(targetUnitId, out NetworkObject targetNO) && targetNO != null)
        {
             _currentTargetController = targetNO.GetComponent<UnitController>();
        } else {
             _currentTargetController = null; // Not found
        }


        if (_currentTargetController != null)
        {
            // Only show if target is valid
            bool wasVisible = _isVisible;
            NetworkId previousTarget = _currentTargetUnitId;

            _currentTargetUnitId = targetUnitId;
            _currentSelectionRef = currentSelection;

            // Regenerate dynamic elements only if target changed or menu was hidden
            if (!wasVisible || previousTarget != targetUnitId)
            {
                 GenerateCommandButtons();
                 GenerateShipIcons();
                 // Regenerate status bars/icons if they are fully dynamic
                 ClearStatusBars();
                 ClearSystemIcons();
            }

            _root.style.display = DisplayStyle.Flex; // Show the root container
            _isVisible = true;
            Update(); // Force immediate update of position and data
        }
        else
        {
            // Target became invalid, hide
            HideMenu();
        }
    }

    public void HideMenu()
    {
        if (_root != null) _root.style.display = DisplayStyle.None;
        if (_isVisible) // Only clear if it was visible
        {
             ClearCommandButtons();
             ClearShipIcons();
             ClearSystemIcons();
             ClearStatusBars();
        }
        _isVisible = false;
        _currentTargetUnitId = default;
        _currentTargetController = null;
        _currentSelectionRef = null;
        _runnerRef = null;
    }


    // --- Helper Methods ---
    Vector2 CalculateCircularPosition(int index, int totalCount, float radius, float startAngleDeg, float endAngleDeg)
    {
        if (totalCount <= 0) return Vector2.zero;

        float totalAngleDeg = endAngleDeg - startAngleDeg;
        // Adjust spacing: if only one item, center it in the arc
        float angleStep = (totalCount > 1) ? totalAngleDeg / (totalCount -1) : 0;
        float currentAngleDeg = startAngleDeg + (totalCount > 1 ? (index * angleStep) : totalAngleDeg / 2.0f);

        // Convert angle to radians for Mathf functions
        float currentAngleRad = currentAngleDeg * Mathf.Deg2Rad;

        // Calculate position (X=radius*cos, Y=radius*sin)
        // Y is typically up in UI, but angle 0 is right. Adjust if needed.
        float x = radius * Mathf.Cos(currentAngleRad);
        float y = radius * Mathf.Sin(currentAngleRad); // NB: Y positive is DOWN in UI Toolkit coords usually

        return new Vector2(x, y);
    }

    Texture2D GetIconForCommand(byte commandType)
    {
        // Example mapping - replace with your logic
        switch (commandType)
        {
            case NetworkInputData.COMMAND_REPAIR: return repairIcon;
            case NetworkInputData.COMMAND_STOP: return stopIcon;
            // case NetworkInputData.COMMAND_ATTACK_MOVE: return attackMoveIcon;
            default: return null; // Or a default 'unknown' icon
        }
    }
     string GetBorderClassForCommand(byte commandType)
    {
        // Example mapping - replace with your logic
        switch (commandType)
        {
            case NetworkInputData.COMMAND_REPAIR: return "border-green";
            case NetworkInputData.COMMAND_STOP: return "border-yellow";
            case NetworkInputData.COMMAND_ATTACK: return "border-red";
            default: return ""; // No specific border class
        }
    }



}
