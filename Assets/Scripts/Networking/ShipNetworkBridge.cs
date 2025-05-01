using UnityEngine;
using Fusion;
using Unity.Entities;
using Unity.Mathematics; // For float3 comparison etc.

public class ShipNetworkBridge : NetworkBehaviour
{
    // --- State Synchronization ---
    // Define [Networked] properties for state you need clients to know
    // for visuals or non-authoritative feedback. The server will write to these
    // by reading from the linked ECS entity.

    // Syncing the high-level AI state for client-side effects
    [Networked] public TacticalGoal.GoalType NetworkedAIState { get; set; } = TacticalGoal.GoalType.None;

    // Syncing the target's NetworkId (if target is also a NetworkObject)
    [Networked] public NetworkId NetworkedTargetId { get; set; }

    // Sync the target's position (if target is a non-NetworkObject or ECS entity)
    [Networked] public Vector3 NetworkedTargetPosition { get; set; } = float3.zero;


    // --- Linking ---
    private Entity _linkedEntity = Entity.Null;
    private EntityManager _entityManager;
    private bool _isLinked = false;

    // Called by the server in InitializeShipBeforeSpawn
    public void SetLinkedEntity(Entity entity, EntityManager manager)
    {
        _linkedEntity = entity;
        _entityManager = manager;
        _isLinked = entity != Entity.Null && _entityManager != null ;//&& _entityManager.IsCreated(entity);
        // Debug.Log($"Bridge on {gameObject.name} (NO Id: {Id}) linked to Entity {_linkedEntity.Index}:{_linkedEntity.Version}");
    }

    // --- Fusion Callbacks ---

    public override void Spawned()
    {
        // Called on Server and Clients AFTER the object is spawned and networked.
        // If _linkedEntity is not set on the server here, something went wrong in initialization.
        // Clients won't have the entity linked automatically here unless you implement
        // a client-side entity creation/lookup mechanism (more complex).
        // For a server-authoritative model, clients often don't need the direct entity link.

        if (Runner.IsServer && !_isLinked)
        {
            Debug.LogError($"Server: ShipNetworkBridge on {gameObject.name} (NO Id: {Id}) failed to link to an Entity during init!", gameObject);
        }
        // Debug.Log($"Spawned: {gameObject.name} (NO Id: {Id}), IsServer: {Runner.IsServer}, IsClient: {Runner.IsClient}, IsLinked: {_isLinked}");
    }

    public override void FixedUpdateNetwork()
    {
        // --- SERVER: Read ECS state -> Write to [Networked] properties ---
        if (Runner.IsServer && _isLinked)
        {
            // Ensure the linked entity still exists in the ECS world
            if (!_entityManager.Exists(_linkedEntity))
            {
                Debug.LogWarning($"Server: Linked entity {_linkedEntity} no longer exists for NO {Id}. Despawning.", gameObject);
                // If the ECS entity is gone, the NetworkObject should likely be despawned too.
                Runner.Despawn(Object); // Despawn self
                _isLinked = false; // Mark as unlinked
                return;
            }

            // Read data from the linked ECS Entity
            try
            {
                TacticalGoal goal = _entityManager.GetComponentData<TacticalGoal>(_linkedEntity);

                // Write to [Networked] properties if changed
                if (NetworkedAIState != goal.CurrentGoal)
                {
                    NetworkedAIState = goal.CurrentGoal;
                }

                // Example: Get NetworkId from target Entity (Requires helper system/component)
                NetworkId targetNetId = GetNetworkIdFromEntity(goal.TargetEntity);
                if (NetworkedTargetId != targetNetId)
                {
                     NetworkedTargetId = targetNetId;
                }

                // Read other necessary ECS data...
            }
            catch (System.Exception e)
            {
                 Debug.LogError($"Server: Error reading ECS component for NO {Id}, Entity {_linkedEntity}: {e.Message}", gameObject);
                 // Handle error, maybe despawn if state is corrupt
            }
        }

        
        if (GetInput(out NetworkInputData input))
        {
             if (input.CommandType == 1 ) /* TODO this unit was selected */
             {
                  // Apply prediction immediately (will be corrected by server state if needed)
                  NetworkedTargetPosition = input.TargetPosition;
                  NetworkedAIState = TacticalGoal.GoalType.MoveTo; // Moving
             }
        }
        
    }

    public override void Render()
    {
        // --- CLIENT: Read [Networked] properties -> Apply Visuals ---
        // Render is called *every frame* for interpolation.
        // Use the interpolated values of [Networked] properties for visuals.
        if (Runner.IsClient)
        {
            // Example: Trigger VFX based on the interpolated AI state
            // ShipVFXManager.SetStateEffect(this.NetworkedAIState);

            // Example: Draw a UI target indicator based on the interpolated target ID
            // TargetUISystem.HighlightTarget(this.NetworkedTargetId);

            // IMPORTANT: Do NOT modify authoritative ECS state here on the client.
            // This is only for visual feedback based on the server's synced state.
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Called on Server and Clients when the object is despawned.
        // --- SERVER: Clean up the corresponding ECS entity ---
        if (runner.IsServer && _isLinked)
        {
            if (_entityManager.Exists(_linkedEntity))
            {
                Debug.Log($"Server: Despawning NetworkObject {Id}. Destroying linked Entity {_linkedEntity}.");
                _entityManager.DestroyEntity(_linkedEntity); // Destroy the ECS entity
            }
            else
            {
                 Debug.LogWarning($"Server: Despawning NetworkObject {Id}. Linked Entity {_linkedEntity} was already destroyed.");
            }
            // Unregister from any lookup systems
            // ShipLookupSystem.UnregisterShip(Id);
        }

        // Reset state
        _linkedEntity = Entity.Null;
        _isLinked = false;
    }

    // --- Helper Function (Requires Implementation) ---
    private NetworkId GetNetworkIdFromEntity(Entity entity)
    {
        // This is a placeholder! You need a robust way to map an ECS Entity
        // back to the NetworkId of its corresponding NetworkObject.
        // Options:
        // 1. Add a component to the entity storing its NetworkObject Id.
        // 2. Maintain a global Dictionary<Entity, NetworkId> in a system.
        if (entity == Entity.Null || !_entityManager.Exists(entity))
        {
            return new NetworkId(); // Invalid ID if entity is null or doesn't exist
        }

        // Example using a hypothetical lookup system:
        // if (ShipLookupSystem.TryGetNetworkId(entity, out NetworkId id)) return id;

        // Example if you added a component like `NetworkedEntityInfo { NetworkId Id; }`
        if (_entityManager.HasComponent<NetworkedEntityInfo>(entity)) {
            return _entityManager.GetComponentData<NetworkedEntityInfo>(entity).Id;
        }

        return new NetworkId(); // Return invalid if no mapping found
    }
}