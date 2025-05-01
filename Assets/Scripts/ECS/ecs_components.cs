using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Fusion; // Required for Networked attribute and INetworkStructs

// --- Core Components ---

// Basic transform components (Provided by Unity.Transforms)
// - LocalTransform (or Position, Rotation if not using hierarchy)

// Physics components (Provided by Unity.Physics or Havok)
// - PhysicsVelocity
// - PhysicsMass
// - PhysicsCollider, etc.

// --- Networking Components ---

// Component to identify a networked ship entity
public struct NetworkedShip : IComponentData { }

// Networked state essential for clients (Position/Rotation often handled by NetworkTransform)
// If using NetworkTransform (recommended for simplicity), you might not need custom position/rotation sync here.
// If doing pure ECS sync, you'd define networked position/rotation.
// Let's assume NetworkTransform handles basic sync, but we might need other state.
public struct NetworkedEntityInfo : IComponentData, INetworkStruct // Use INetworkStruct for components synced via NetworkedObject vars
{
    // Example: Sync target entity NetworkId if needed by client-side prediction/visuals
     public NetworkId Id; // NetworkId of the ship (or other entity)
    // Example: Sync current high-level state for visuals/debugging
    // public ShipAIState CurrentAIState;
}

// --- Ship Definition Components ---

// Defines the ship's movement capabilities
public struct ShipMovementStats : IComponentData
{
    public float MaxSpeed;       // Maximum linear speed
    public float MaxForce;       // Maximum steering force (acceleration capability)
    public float RotationSpeed;  // How fast the ship can turn
    // Add drag, angular drag etc. if needed
}

// --- Tactical Layer Components (Simplified Example) ---

// Represents the ship's current high-level goal
public struct TacticalGoal : IComponentData
{
    public enum GoalType { None, MoveTo, AttackTarget, FollowLeader, Evade }
    public GoalType CurrentGoal;
    public float3 TargetPosition;  // Used for MoveTo, potentially Attack/Follow
    public Entity TargetEntity;    // Used for AttackTarget, FollowLeader
    public float StoppingDistance; // Radius for Arrive behavior
}

// --- Steering Layer Components ---

// Input for the steering system, calculated by tactical/goal systems
// Multiple systems might contribute to this desired state before final calculation.
public struct SteeringInput : IComponentData
{
    public float3 DesiredVelocity; // The velocity the ship *wants* to achieve
    public float Weight;           // How strongly to pursue this velocity (can be adjusted by different goal systems)
    public bool ApplyBraking;      // Should the ship actively try to brake if overshooting?
}

// Component to store calculated steering force before applying physics
// This allows separation of calculation and application.
public struct CalculatedSteeringForce : IComponentData
{
    public float3 LinearForce;
    public float3 AngularTorque; // If controlling rotation directly
}

// --- Flocking Components ---

public struct FlockingAgent : IComponentData
{
    public float SeparationRadius;
    public float AlignmentRadius;
    public float CohesionRadius;


}

// Temporary component to store calculated flocking forces
public struct FlockingForces : IComponentData
{
    public float3 SeparationForce;
    public float3 AlignmentForce;
    public float3 CohesionForce;

    public float SeparationWeight;
    public float AlignmentWeight;
    public float CohesionWeight;
}

// --- Avoidance Components ---

public struct ObstacleAvoidanceAgent : IComponentData
{
    public float AvoidanceRadius;     // How far ahead to check
    public float AvoidanceWeight;
    public LayerMask ObstacleLayer; // Physics layer for obstacles
}

// Temporary component for avoidance force
public struct AvoidanceForce : IComponentData
{
    public float3 Force;
}

// --- Projectile Avoidance (More Complex) ---
// Would likely involve components tracking nearby threats (projectiles)
// and a dedicated high-priority system. Omitted here for brevity.

