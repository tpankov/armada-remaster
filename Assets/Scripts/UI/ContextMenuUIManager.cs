using UnityEngine;
using UnityEngine.UI; // For UI elements like Button, Image
using TMPro; // If using TextMeshPro
using Fusion;
using System.Collections.Generic; // For HashSet
using System.Linq; // For FirstOrDefault

/// <summary>
/// Manages the context-sensitive UI menu that appears over selected units.
/// Handles positioning, displaying unit data, and routing UI actions back to the input handler.
/// </summary>
public class ContextMenuUIManager : MonoBehaviour // Or make it a Singleton if preferred
{
    [Header("References")]
    [SerializeField] private GameObject contextMenuRoot; // Assign the root Panel/GameObject of your menu prefab instance
    [SerializeField] private PlayerInputHandler playerInputHandler; // Assign or find reference
    [SerializeField] private Camera mainCamera;

    // --- UI Element References (Assign in Inspector) ---
    [Header("UI Elements")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Image shieldBarFill;
    // Add references for system status icons (Images)
    // Add references for Buttons if needed for dynamic setup (usually handled by OnClick events)
    // [SerializeField] private Button repairButton;

    // --- State ---
    private bool _isMenuVisible = false;
    private NetworkId _currentTargetUnitId;
    private HashSet<NetworkId> _currentSelectionRef; // Reference to the selection that triggered the menu
    private UnitController _currentTargetController; // Cached controller for data access
    private NetworkRunner _runnerRef; // Runner needed to find objects

    void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (playerInputHandler == null) playerInputHandler = FindFirstObjectByType<PlayerInputHandler>(); // Example: Find if not assigned

        if (contextMenuRoot != null)
            contextMenuRoot.SetActive(false); // Start hidden
        else
            Debug.LogError("ContextMenuUIManager: contextMenuRoot is not assigned!", this);
    }

    void Update()
    {
        if (!_isMenuVisible || _currentTargetController == null || _runnerRef == null)
        {
            return; // Do nothing if menu is hidden or target is invalid
        }

        // --- Update Position ---
        // Convert target unit's world position to screen position for the UI
        Vector3 screenPos = mainCamera.WorldToScreenPoint(_currentTargetController.transform.position);

        // Check if target is behind the camera
        if (screenPos.z < 0)
        {
            // Optionally hide the menu or clamp position to screen edge if target goes behind
            HideMenu(); // Simple approach: hide if target is behind camera
            return;
        }

        // Set the anchoredPosition of the UI element (assuming RectTransform)
        RectTransform menuRect = contextMenuRoot.GetComponent<RectTransform>();
        if (menuRect != null)
        {
            menuRect.position = screenPos; // Directly set screen position
            // Adjustments might be needed based on Canvas Scaler settings
        }

        // --- Update Data Display ---
        UpdateStatusBars(); // Update health, shields, etc.

        // --- Handle Context Menu Shortcuts ---
        // Only process if the menu is visible and maybe if the mouse isn't over another UI element
        // (though EventSystem usually handles blocking raycasts for world clicks)
        if (Input.GetKeyDown(KeyCode.R)) // Example: Repair shortcut
        {
            OnRepairAction();
        }
        // Add checks for other shortcuts (F1, etc.)
        // if (Input.GetKeyDown(KeyCode.F1)) { OnFireWeapon1Action(); }
    }

    private void UpdateStatusBars()
    {
        // Access data from the cached _currentTargetController
        // Make sure UnitController exposes necessary data (e.g., current/max health/shields)
        // Example: Assuming UnitController has properties like MaxHealth, CurrentShields, MaxShields
        if (healthBarFill != null)
        {
            // Assuming NetworkedHealth is current health and you have a MaxHealth property/field
            int maxHealth = _currentTargetController.maxHealth; // Need to add MaxHealth to UnitController
            healthBarFill.fillAmount = (maxHealth > 0) ? (_currentTargetController.NetworkedHealth / maxHealth) : 0;
        }
        if (shieldBarFill != null)
        {
            // Assuming UnitController has CurrentShields and MaxShields properties/fields
            int maxShields = _currentTargetController.maxShields; // Need to add MaxShields to UnitController
            shieldBarFill.fillAmount = (maxShields > 0) ? (_currentTargetController.NetworkedShields / maxShields) : 0; 
        }
        // Update system status icons based on _currentTargetController state
    }

    // --- Public Methods Called by PlayerInputHandler ---

    public void ShowMenu(NetworkRunner runner, NetworkId targetUnitId, HashSet<NetworkId> currentSelection)
    {
        if (contextMenuRoot == null || runner == null || !targetUnitId.IsValid) return;

        _runnerRef = runner; // Store runner for later use

        // Find the target unit controller
        if (runner.TryFindObject(targetUnitId, out NetworkObject targetNO) && targetNO != null)
        {
            _currentTargetController = targetNO.GetComponent<UnitController>();
            if (_currentTargetController != null)
            {
                _currentTargetUnitId = targetUnitId;
                _currentSelectionRef = currentSelection; // Store reference to the selection
                contextMenuRoot.SetActive(true);
                _isMenuVisible = true;
                Update(); // Force immediate position/data update
                Debug.Log($"Showing context menu for Unit {targetUnitId}");
                return; // Success
            }
        }

        // If target couldn't be found or has no controller, ensure menu is hidden
        HideMenu();
        Debug.LogWarning($"Could not show context menu for Unit {targetUnitId} - Object/Controller not found.");
    }

    public void HideMenu()
    {
        if (contextMenuRoot != null)
            contextMenuRoot.SetActive(false);
        _isMenuVisible = false;
        _currentTargetUnitId = default;
        _currentTargetController = null;
        _currentSelectionRef = null;
        _runnerRef = null;
        // Debug.Log("Hiding context menu");
    }

    // --- UI Button Action Handlers (Called by Button OnClick events) ---

    public void OnRepairAction() // Example action
    {
        if (!_isMenuVisible || playerInputHandler == null || _currentSelectionRef == null) return;
        Debug.Log("UI Repair Action Triggered");

        // Create a command specific to Repair
        // You might need a new CommandType or use existing ones with context
        var command = new PendingCommand
        {
            // Assuming a dedicated command type or specific parameters needed
            CommandType = NetworkInputData.COMMAND_REPAIR, // **NOTE: Add COMMAND_REPAIR = 3 (or similar) to NetworkInputData**
            SelectedUnitIds = _currentSelectionRef.ToArray(), // Apply to the whole selection that opened the menu
            TargetPosition = _currentTargetController.transform.position, // Target might be self for repair
            TargetObjectId = _currentTargetUnitId // Target might be self
        };

        playerInputHandler.QueueCommandFromUI(command);
        // Optionally hide menu after action?
        // HideMenu();
    }

    public void OnAttackMoveAction() // Example
    {
         if (!_isMenuVisible || playerInputHandler == null || _currentSelectionRef == null) return;
         Debug.Log("UI Attack Move Action Triggered - Requires Target Selection!");
         // Attack Move usually needs a subsequent click on the map for the target position.
         // This requires more complex state management:
         // 1. Click Attack Move button.
         // 2. Enter an "AttackMoveTargeting" state in PlayerInputHandler.
         // 3. Change mouse cursor.
         // 4. Next world click (not on UI) defines the TargetPosition.
         // 5. PlayerInputHandler creates and queues the AttackMove command then.
         // This button click might just initiate that state change.
         // playerInputHandler.EnterTargetingMode(TargetingMode.AttackMove, _currentSelectionRef);
         // HideMenu();
    }

    public void OnStopAction() // Example
    {
        if (!_isMenuVisible || playerInputHandler == null || _currentSelectionRef == null) return;
        Debug.Log("UI Stop Action Triggered");
        var command = new PendingCommand
        {
            CommandType = NetworkInputData.COMMAND_STOP, // **NOTE: Add COMMAND_STOP = 4 (or similar) to NetworkInputData**
            SelectedUnitIds = _currentSelectionRef.ToArray(),
            // TargetPosition/TargetObjectId likely irrelevant for Stop
        };
        playerInputHandler.QueueCommandFromUI(command);
    }

    // Add more methods for other buttons (Fire Weapon 1, Abilities, etc.)
    // Remember to define corresponding CommandTypes in NetworkInputData if needed.

}
