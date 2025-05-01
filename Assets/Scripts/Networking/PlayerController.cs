using Fusion;
using UnityEngine;
using System.Linq; // For Count() if needed on arrays

/// <summary>
/// Represents a player in the network session.
/// Attached to the player prefab spawned by Fusion.
/// Responsible for retrieving the player's input each tick in FixedUpdateNetwork
/// and processing commands (primarily on the Host/Server) by updating the state
/// of the commanded UnitController instances.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerController : NetworkBehaviour
{
    // Optional: Store PlayerRef if needed for other logic
    public PlayerRef Player { get; private set; }

    public override void Spawned()
    {
        Player = Object.InputAuthority; // Store the PlayerRef if needed elsewhere

        if (Object.HasInputAuthority)
        {
            Debug.Log($"PlayerController for local player {Object.InputAuthority} Spawned.");
            // Perform any initialization specific to the local player's controller
        }
        else
        {
            Debug.Log($"PlayerController for remote player {Object.InputAuthority} Spawned.");
            // Perform initialization for proxy controllers if necessary
        }

        gameObject.name = $"PlayerController_{Object.InputAuthority}";
    }

    /// <summary>
    /// Runs every network tick. Retrieves and processes input for this player.
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        // Get the input struct for the player controlling this object.
        // This works on both Host (for its local input) and Clients (for prediction/state updates).
        if (GetInput(out NetworkInputData input))
        {
            // --- Input Processing ---
            if (input.CommandType == NetworkInputData.COMMAND_SPAWN)
            {
                ShipSpawner shipSpawner = FindFirstObjectByType<ShipSpawner>();
                shipSpawner.SpawnShip(Player, input.TargetPosition, Quaternion.identity);
            }
            // Check if a valid command was issued (not COMMAND_NONE)
            else if (input.CommandType != NetworkInputData.COMMAND_NONE && input.SelectedUnitIds.Length > 0)
            {
                // --- Authoritative Command Application (Host/Server or State Authority) ---
                // Only the Host/Server should modify the Networked properties of the units.
                if (Runner.IsServer || Object.HasStateAuthority) // Use Runner.IsServer for Host/Server check
                {
                    // Debug.Log($"Server/Host processing command {input.CommandType} for player {Object.InputAuthority} affecting {input.SelectedUnitIds.Length} units.");

                    // Iterate through the units the command applies to
                    foreach (NetworkId unitId in input.SelectedUnitIds)
                    {
                        // Find the NetworkObject for the unit
                        if (Runner.TryFindObject(unitId, out NetworkObject unitNetworkObject) && unitNetworkObject != null)
                        {
                            // Get the UnitController component
                            UnitController unitController = unitNetworkObject.GetComponent<UnitController>();
                            if (unitController != null)
                            {
                                // --- Apply the command state to the unit ---
                                unitController.NetworkedCommandState = input.CommandType; // This might need more nuance

                                // Handle specific command logic
                                switch(input.CommandType)
                                {
                                    case NetworkInputData.COMMAND_MOVE:
                                        unitController.NetworkedTargetPosition = input.TargetPosition;
                                        unitController.NetworkedTargetObjectId = default; // Clear specific target
                                        break;
                                    case NetworkInputData.COMMAND_ATTACK:
                                        unitController.NetworkedTargetPosition = input.TargetPosition; // May still need pos for attack-move
                                        unitController.NetworkedTargetObjectId = input.TargetObjectId;
                                        break;
                                    case NetworkInputData.COMMAND_REPAIR:
                                        // Set state, target might be self or specific repair target if needed
                                        unitController.NetworkedTargetObjectId = input.TargetObjectId; // Could be self or target
                                        break;
                                    case NetworkInputData.COMMAND_STOP:
                                        // Clear targets, set appropriate state in UnitController if needed
                                        unitController.NetworkedTargetPosition = unitController.transform.position; // Stop at current pos
                                        unitController.NetworkedTargetObjectId = default;// = NetworkId.Invalid;
                                        // Maybe set CommandState to Idle explicitly if Stop maps to Idle
                                        break;
                                    // Add cases for other commands...
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"UnitController component not found on NetworkObject with ID {unitId}.");
                            }
                        }
                        else
                        {
                            // Unit might have been destroyed between command issuance and processing.
                            // Debug.Log($"Could not find NetworkObject for Unit ID {unitId} while processing command. It might have been destroyed.");
                        }
                    }
                }
                // --- Client-Side Prediction (Optional) ---
                // else if (Object.HasInputAuthority) // If this is the predicting client
                // {
                //     // Apply predictive logic here based on the input.
                //     // For example, immediately start a move animation or sound effect.
                //     // This will be corrected if the server state differs.
                //     // Debug.Log($"Client applying predicted command {input.CommandType}");
                //     // foreach (NetworkId unitId in input.SelectedUnitIds) { ... find unit, trigger predictive effect ... }
                // }
            }


        }
        else
        {
             // Optional: Handle cases where input might be missing, though GetInput usually provides
             // the latest available (potentially repeated or interpolated) input.
             // Debug.LogWarning($"No input found for player {Object.InputAuthority} in tick {Runner.Tick}");
        }
        // --- Other Tick-Based Logic ---
        // Handle other player-specific logic that needs to run every tick,
        // like resource generation, cooldowns, etc.
        // if (Runner.IsServer || Object.HasStateAuthority) {
        //     // Update resources, etc.
        // }
    }
}
