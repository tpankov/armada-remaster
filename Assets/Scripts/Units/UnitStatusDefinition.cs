// UnitStatusDefinition.cs
using UnityEngine;

// Defines a type of status effect (Repairing, Stunned, Shield Boost, etc.)
[CreateAssetMenu(fileName = "New Unit Status", menuName = "RTS/Unit Status Definition")]
public class UnitStatusDefinition : ScriptableObject
{
    [Tooltip("Identifier used internally")]
    public string StatusID = "BaseStatus"; // e.g., "Repairing", "Stunned", "EngineBoost"
    public string DisplayName = "Base Status";
    public Texture2D Icon;
    [TextArea] public string Description;
    public bool IsBuff = false; // Example flag
    public float DefaultDuration = 5f; // If applicable, 0 or -1 for permanent until removed
    // Add other shared configuration for status types
}