using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;
// Include namespaces for your custom components (ShipMovementStats, TacticalGoal, etc.)

public static class ShipArchetype
{
    public static EntityArchetype Definition { get; private set; }

    public static void Create(EntityManager entityManager)
    {
        if (Definition.Valid) return; // Already created

        Definition = entityManager.CreateArchetype(
            // Core ECS Components
            typeof(LocalTransform), // Position, Rotation, Scale
            // typeof(Parent), typeof(LocalToWorld) // Often added automatically

            // Networking Identifier
            typeof(NetworkedShip), // Your marker component
            // typeof(NetworkedShipState), // If syncing custom state via ECS directly

            // Physics Components (adjust based on your physics setup)
            typeof(PhysicsVelocity),
            typeof(PhysicsMass),
            typeof(PhysicsCollider), // Assign the actual BlobAssetReference later
            // typeof(PhysicsDamping),
            // typeof(PhysicsGravityFactor),

            // Ship Definition Components
            typeof(ShipMovementStats),

            // Tactical Layer Components
            typeof(TacticalGoal),

            // Steering Layer Components
            typeof(SteeringInput),
            typeof(CalculatedSteeringForce),

            // Flocking Components
            typeof(FlockingAgent),
            typeof(FlockingForces),

            // Avoidance Components
            typeof(ObstacleAvoidanceAgent),
            typeof(AvoidanceForce)

            // Add any other components your ship needs
        );
    }
}

// Call ShipArchetype.Create(World.DefaultGameObjectInjectionWorld.EntityManager); during initialization.
