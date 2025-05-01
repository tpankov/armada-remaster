using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Fusion;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms; // For LocalTransform
using Unity.Physics;
//using Unity.VisualScripting;   // For Physics components

public class ShipSpawner : NetworkBehaviour // Or any class with access to the Runner
{
    // Assign the Addressable key for your ship prefab in the Inspector or via script
    public string shipAddressableKey = "MyShipPrefabAddress";

    private Unity.Entities.EntityManager _entityManager;

    public override void Spawned() // Or Awake/Start if not a NetworkBehaviour itself
    {
        // Get the EntityManager. Typically, you use the default world unless
        // you have a specific multi-world setup.
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Ensure the archetype is created (safe to call multiple times)
        ShipArchetype.Create(_entityManager);
    }

    // Call this function on the SERVER to spawn a ship
    public async void SpawnShip(PlayerRef playerAuthority, float3 spawnPosition, quaternion spawnRotation)
    {
        if (!HasStateAuthority) return; // Only server can spawn

        // Ensure EntityManager is valid before proceeding
        if (_entityManager == default)
        {
             Debug.LogError("EntityManager is not initialized in ShipSpawner!");
             return;
        }


        // 1. Load the Addressable Prefab
        AsyncOperationHandle<GameObject> loadHandle = Addressables.LoadAssetAsync<GameObject>(shipAddressableKey);
        await loadHandle.Task; // Wait for loading to complete

        if (loadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject prefab = loadHandle.Result;

            // 2. Spawn via Fusion, providing the initialization delegate
            Runner.Spawn(prefab,
                         spawnPosition,
                         spawnRotation,
                         playerAuthority, // Assign player authority if needed, otherwise default (server)
                         InitializeShipBeforeSpawn // Pass the delegate function
            );
        }
        else
        {
            Debug.LogError($"Failed to load ship prefab: {shipAddressableKey}");
        }

        // Release the handle when done (optional, depends on management strategy)
        // Addressables.Release(loadHandle);
    }

    // 3. Initialization Delegate (Executed by Fusion BEFORE the object is networked)
    private void InitializeShipBeforeSpawn(NetworkRunner runner, NetworkObject networkObject)
    {
        // --- This code runs ONLY on the SERVER ---

        // Ensure EntityManager is valid (might be called before Spawned sometimes)
        if (_entityManager == default)
        {
            Debug.LogWarning("InitializeShipBeforeSpawn: EntityManager not ready, attempting to get default.");
             _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
             if(_entityManager == default)
             {
                 Debug.LogError("InitializeShipBeforeSpawn: Failed to get EntityManager!");
                 return; // Cannot proceed without EntityManager
             }
             // Ensure archetype exists if we just got the manager here
             ShipArchetype.Create(_entityManager);
        }


        Debug.Log($"InitializeShipBeforeSpawn for NetworkObject ID: {networkObject.Id}");

        // a. Get the NetworkBehaviour bridge
        ShipNetworkBridge bridge = networkObject.GetComponent<ShipNetworkBridge>();
        if (bridge == null)
        {
            Debug.LogError("Ship prefab is missing ShipNetworkBridge component!", networkObject.gameObject);
            // Consider destroying the object if setup fails critically
            // runner.Despawn(networkObject); // Be careful with despawning here
            return;
        }

        // b. Create the corresponding ECS Entity
        Entity shipEntity = _entityManager.CreateEntity(ShipArchetype.Definition);

        // c. Set Initial ECS Component Data
        //    - Transform: Match the NetworkObject's spawn transform
        _entityManager.SetComponentData(shipEntity, new LocalTransform
        {
            Position = networkObject.transform.position, // Use the NO's initial position
            Rotation = networkObject.transform.rotation, // Use the NO's initial rotation
            Scale = 1.0f // Or prefab scale
        });

        //    - Physics (Example: Set initial velocity, mass properties)
        //      Mass/Collider usually come from baked data or are set based on ship type
        //      You might need to load/assign a PhysicsCollider BlobAssetReference here.
        // _entityManager.SetComponentData(shipEntity, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
        // _entityManager.SetComponentData(shipEntity, PhysicsMass.CreateDynamic(massProperties, 100f)); // Example mass

        //    - Ship Specific Stats (Load from config or set default)
        _entityManager.SetComponentData(shipEntity, new ShipMovementStats
        {
            MaxSpeed = 50f, // Example values
            MaxForce = 100f,
            RotationSpeed = 5f
            // ... other stats
        });

        //    - Initial Tactical Goal (e.g., Idle or MoveTo spawn area)
        _entityManager.SetComponentData(shipEntity, new TacticalGoal
        {
            CurrentGoal = TacticalGoal.GoalType.None // Or MoveTo nearby point
            // ... other goal params
        });

        //    - Initialize other components as needed (FlockingAgent, AvoidanceAgent, etc.)
        _entityManager.SetComponentData(shipEntity, new FlockingAgent { /* ... default values ... */ });
        _entityManager.SetComponentData(shipEntity, new ObstacleAvoidanceAgent { /* ... default values ... */ });

        // --- d. Link GameObject <-> Entity ---
        // Store the Entity reference in the bridge component.
        // This is crucial for the bridge to find its ECS counterpart later.
        bridge.SetLinkedEntity(shipEntity, _entityManager);

        Debug.Log($"Linked NetworkObject {networkObject.Id} to Entity {shipEntity.Index}:{shipEntity.Version}");

        // Add the entity to any global lookup systems if needed (e.g., Dictionary<NetworkId, Entity>)
        // ShipLookupSystem.RegisterShip(networkObject.Id, shipEntity);
    }
}
