using Fusion;
using UnityEngine;
using Unity.Entities; // Required if storing the Entity reference directly

// Ensure NetworkTransform is also present for basic position/rotation sync
[RequireComponent(typeof(NetworkTransform))]
public class UnitController : NetworkBehaviour
{
    [Header("Unit Configuration")]
    [SerializeField] private string shipClass = "DefaultClass"; // Set in Inspector per unit type

    [Header("Component References (Optional)")]
    [SerializeField] private Animator unitAnimator; // Assign if using animations
    [SerializeField] private AudioSource unitAudioSource; // Assign for sound effects
    [SerializeField] private ParticleSystem moveOrderEffect; // Assign particle effect prefab/instance

    // --- ECS Linking ---
    [HideInInspector] public Entity LinkedEntity { get; set; } = Entity.Null;

    // --- Networked Properties (Fusion v2 Style) ---
    // State synchronized by Fusion. SET by Host/Server based on input. READ by ECS bridge.
    [Networked] public byte NetworkedCommandState { get; set; } = NetworkInputData.COMMAND_NONE;
    [Networked] public Vector3 NetworkedTargetPosition { get; set; }
    [Networked] public NetworkId NetworkedTargetObjectId { get; set; }
    [Networked] public byte CustomNetworkedState { get; set; }
    [Networked] public int NetworkedShields { get; set; }
    [Networked] public int NetworkedHealth { get; set; }

    // --- Previous State Storage (for Change Detection) ---
    private byte _previousCommandState = NetworkInputData.COMMAND_NONE;
    private byte _previousCustomState = 0;
    private int _previousShields = -1; // Initialize to a value unlikely to be the starting shields
    private int _previousHealth = -1; // Initialize to a value unlikely to be the starting health

    public int maxShields {get; set; } = 120; // Example max shields, adjust as needed
    public int maxHealth {get; set;} = 100; // Example max health, adjust as needed

    // --- Public Accessors ---
    public string ShipClass => shipClass;

    // --- Internal State ---
    private bool _isInitialized = false;

    #region Unity & Fusion Lifecycle

    public override void Spawned()
    {
        // Runs on all clients AFTER the network object is created and initial state is received.

        // Initialize components
        if (unitAnimator == null) unitAnimator = GetComponentInChildren<Animator>();
        if (unitAudioSource == null) unitAudioSource = GetComponent<AudioSource>();

        // If this client has state authority (usually Host/Server), initialize default values
        if (Object.HasStateAuthority)
        {
            // Note: Initial values set via [Networked] property initializers are often sufficient,
            // but explicit setting here can be clearer or used for more complex defaults.
            NetworkedShields = maxShields; // Example: Set max shields
            NetworkedHealth = maxHealth;
            NetworkedCommandState = NetworkInputData.COMMAND_NONE;
            CustomNetworkedState = 0;
        }

        // --- Initialize Previous State AFTER potential host initialization ---
        // This prevents falsely detecting changes on the very first Render frame.
        _previousCommandState = NetworkedCommandState;
        _previousCustomState = CustomNetworkedState;
        _previousShields = NetworkedShields;
        _previousHealth = NetworkedHealth;

        // Apply initial visual/audio state based on the *current* networked values
        HandleCommandStateChange(_previousCommandState, NetworkedCommandState); // Pass prev/current
        HandleCustomStateChange(_previousCustomState, CustomNetworkedState);
        HandleShieldsChange(_previousShields, NetworkedShields);
        HandleHealthChange(_previousHealth, NetworkedHealth);

        UnitManager.Instance.RegisterUnit(this);
        _isInitialized = true;
        gameObject.name = $"Unit_{Object.Id.ToString()}_{shipClass}";
    }

    // Render runs every visual frame. Ideal place for detecting changes
    // for visual/audio feedback in Fusion v2.
    public override void Render()
    {
        if (!_isInitialized) return; // Ensure Spawned has run

        // --- Change Detection ---

        // Check Command State
        if (NetworkedCommandState != _previousCommandState)
        {
            HandleCommandStateChange(_previousCommandState, NetworkedCommandState);
            _previousCommandState = NetworkedCommandState; // Update previous state AFTER handling change
        }

        // Check Custom State
        if (CustomNetworkedState != _previousCustomState)
        {
            HandleCustomStateChange(_previousCustomState, CustomNetworkedState);
            _previousCustomState = CustomNetworkedState;
        }

        // Check Shields State
        if (!Mathf.Approximately(NetworkedShields, _previousShields)) // Use Approximately for float comparison
        {
            HandleShieldsChange(_previousShields, NetworkedShields);
            _previousShields = NetworkedShields;
        }

        // Check Health State
        if (!Mathf.Approximately(NetworkedHealth, _previousHealth)) // Use Approximately for float comparison
        {
            HandleHealthChange(_previousHealth, NetworkedHealth);
            _previousHealth = NetworkedHealth;
        }

        // --- Continuous Visual Updates (Optional) ---
        // Example: Update engine trail intensity based on whether unit is moving
        // UpdateEngineTrails(NetworkedCommandState == NetworkInputData.COMMAND_MOVE);
    }

    #endregion

    #region State Change Reaction Logic (Instance Methods)

    // These methods are now called from Render() when a change is detected

    private void HandleCommandStateChange(byte oldState, byte newState)
    {
        // --- Trigger Effects Based on State Change ---
        // Debug.Log($"Unit {Object.Id} Command State Changed: {oldState} -> {newState}");

        // Example: Play move order effect/sound only when changing TO move state
        if (newState == NetworkInputData.COMMAND_MOVE && oldState != NetworkInputData.COMMAND_MOVE)
        {
            PlayMoveOrderFeedback();
        }

        // Example: Update Animator Parameter
        if (unitAnimator != null)
        {
            unitAnimator.SetBool("IsMoving", newState == NetworkInputData.COMMAND_MOVE);
            unitAnimator.SetBool("IsAttacking", newState == NetworkInputData.COMMAND_ATTACK);
            // Set idle state based on COMMAND_NONE or other logic
            unitAnimator.SetBool("IsIdle", newState == NetworkInputData.COMMAND_NONE);
        }
    }

    private void HandleCustomStateChange(byte oldState, byte newState)
    {
        // Debug.Log($"Unit {Object.Id} Custom State Changed: {oldState} -> {newState}");
        // Add logic to react to your custom state changes (e.g., apply stun effect, shield visuals)
    }

    private void HandleShieldsChange(float oldShields, float newShields)
    {
        // Debug.Log($"Unit {Object.Id} Shields Changed: {oldShields} -> {newShields}");

        // Example: Play shield hit effect if shields decreased
        if (newShields < oldShields)
        {
            PlayDamageShieldsEffect(); // Placeholder for shield hit effect
        }

        // Example: Update a shield bar UI element associated with this unit
        // UpdateShieldBar(newShields / 120f); // Assuming max shields is 120
    }

    private void HandleHealthChange(float oldHealth, float newHealth)
    {
        // Debug.Log($"Unit {Object.Id} Health Changed: {oldHealth} -> {newHealth}");

        // Example: Play damage effect if health decreased
        if (newHealth < oldHealth)
        {
            PlayDamageEffect();
        }

        // Example: Trigger death sequence if health <= 0
        if (newHealth <= 0 && oldHealth > 0)
        {
            HandleDeath();
        }

        // Example: Update a health bar UI element associated with this unit
        // UpdateHealthBar(newHealth / 100f); // Assuming max health is 100
    }

    #endregion

    #region Visual/Audio Feedback Methods (Placeholders - Same as before)

    private void PlayMoveOrderFeedback()
    {
        // Debug.Log($"Unit {Object.Id} Move Order Acknowledged");
        if (moveOrderEffect != null) moveOrderEffect.Play();
        // Play sound if AudioSource and clip assigned
    }

    private void PlayDamageShieldsEffect()
    {
        // Debug.Log($"Unit {Object.Id} Took Shield Damage");
        // Play shield hit sound, flash material, trigger particle effect, etc.
    }
    private void PlayDamageEffect()
    {
         // Debug.Log($"Unit {Object.Id} Took Damage");
         // Play hit sound, flash material, trigger particle effect, etc.
    }

    private void HandleDeath()
    {
        Debug.Log($"Unit {Object.Id} Died");
        // Play death animation, explosion effect, disable collider, etc.
        // Actual despawning still typically handled by Host/Server logic.
        if (Object.HasStateAuthority)
        {
             // Example: Despawn after a delay
             // Invoke(nameof(RequestDespawn), 2.0f);
        }
        // Disable components immediately on clients for responsiveness
        // GetComponent<Collider>().enabled = false; // Example
    }

    private void RequestDespawn() { // Renamed for clarity
        if(Object != null && Object.IsValid) {
             UnitManager.Instance.DeregisterUnit(this);
             Runner.Despawn(Object);
        }
    }

    public StarshipBase.SystemStatus GetShieldSystemStatus()
    {
        // Placeholder for actual shield status logic
        if (NetworkedShields > 0)
        {
            return StarshipBase.SystemStatus.Online;
        }
        else if (NetworkedShields <= 0 && NetworkedHealth > 0)
        {
            return StarshipBase.SystemStatus.Offline;
        }
        else
        {
            return StarshipBase.SystemStatus.Damaged;
        }
    }
    public StarshipBase.SystemStatus GetEngineSystemStatus()
    {
        // Placeholder for actual health status logic
        if (NetworkedHealth > 0)
        {
            return StarshipBase.SystemStatus.Online;
        }
        else if (NetworkedHealth <= 0 && NetworkedShields > 0)
        {
                return StarshipBase.SystemStatus.Offline;
        }
        else
        {
            return StarshipBase.SystemStatus.Damaged;
        }
    }
    public StarshipBase.SystemStatus GetLifeSupportStatus()
    {
        // Placeholder for actual life support status logic
        return StarshipBase.SystemStatus.Online; // Example: Always online for now
    }
    public StarshipBase.SystemStatus GetWeaponSystemStatus()
    {
        // Placeholder for actual weapon system status logic
        return StarshipBase.SystemStatus.Online; // Example: Always online for now
    }
    public StarshipBase.SystemStatus GetSensorSystemStatus()
    {
        // Placeholder for actual sensor system status logic
        return StarshipBase.SystemStatus.Online; // Example: Always online for now
    }
    public StarshipBase.SystemStatus GetMoraleStatus()
    {
        // Placeholder for actual shield generator status logic
        return StarshipBase.SystemStatus.Online; // Example: Always online for now
    }
    public StarshipBase.SystemStatus GetSuppliesStatus()
    {
        // Placeholder for actual supplies status logic
        return StarshipBase.SystemStatus.Online; // Example: Always online for now
    }
    public StarshipBase.SystemStatus GetPowerDrawStatus()
    {
        // Placeholder for actual hull integrity status logic
        return StarshipBase.SystemStatus.Online; // Example: Always online for now
    }
    // public override void Despawned(NetworkRunner runner, NetworkObject networkObject) {
    //     //Cleanup logic if needed when the object is despawned
    //     //Example: Deregister from UnitManager
    //     UnitManager.Instance.DeregisterUnit(this);
    // }
    // Placeholder for updating a potential health bar
    // private void UpdateHealthBar(float fillAmount) {
    //    // Find and update health bar UI component
    // }

    #endregion
}
