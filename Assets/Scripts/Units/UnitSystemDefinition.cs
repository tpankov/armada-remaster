// UnitSystemDefinition.cs
using UnityEngine;

// Base class for all system definitions (Engines, Weapons, Shields, Hull, Repairer etc.)
[CreateAssetMenu(fileName = "New Unit System", menuName = "RTS/Unit System Definition")]
public class UnitSystemDefinition : ScriptableObject
{
    [Tooltip("Identifier used internally and potentially for display")]
    public string SystemID = "BaseSystem"; // e.g., "Engine", "Hull", "ShieldEmitter"
    public string DisplayName = "Base System";
    public Texture2D Icon;
    [TextArea] public string Description;

    [Header("Base Stats (Examples)")]
    public float MaxHealth = 100f; // If the system itself has health
    public float PowerDraw = 1f;   // Example base power usage
    // Add other shared configuration relevant to all systems
}

// --- Example Specific System ---
// You might create derived classes if systems have vastly different base configs
// [CreateAssetMenu(fileName = "Weapon System", menuName = "RTS/Weapon System Definition")]
// public class WeaponSystemDefinition : UnitSystemDefinition {
//     public float BaseDamage = 10f;
//     public float BaseRange = 100f;
// }