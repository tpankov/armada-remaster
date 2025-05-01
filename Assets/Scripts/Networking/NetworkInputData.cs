using Fusion;
using UnityEngine;

/// <summary>
/// Defines the network input structure for RTS commands.
/// This struct is collected locally by PlayerInputHandler's OnInput callback
/// and sent to the Fusion host/server for processing in FixedUpdateNetwork.
/// </summary>
public struct NetworkInputData : INetworkInput
{
    // --- Command Types ---
    // Using bytes for efficiency, but could be an enum cast to byte.
    public const byte COMMAND_NONE = 0;
    public const byte COMMAND_MOVE = 1;
    public const byte COMMAND_ATTACK = 2;
    public const byte COMMAND_SPAWN = 3;
    public const byte COMMAND_REPAIR = 4; 
    public const byte COMMAND_STOP = 5;  
    // Add other command types here if needed (e.g., Build, Ability1, etc.)

    // --- Input Data Fields ---

    /// <summary>
    /// The type of command issued by the player this tick.
    /// Uses constants defined above (COMMAND_NONE, COMMAND_MOVE, etc.).
    /// </summary>
    public byte CommandType;

    /// <summary>
    /// The world-space position target for the command (e.g., move destination, attack location).
    /// </summary>
    public Vector3 TargetPosition;

    /// <summary>
    /// The NetworkId of a specific target unit (e.g., the unit to attack).
    /// Will be NetworkId.Invalid if the command targets a position or has no specific unit target.
    /// </summary>
    public NetworkId TargetObjectId;

    /// <summary>
    /// An array containing the NetworkIds of all units that were selected
    /// by the player *at the moment this command was issued*.
    /// This ensures the command applies to the correct group, even if local selection changes rapidly.
    /// Marked as [Capacity] to tell Fusion how much space to potentially reserve. Adjust capacity as needed.
    /// </summary>
    [Networked,Capacity(64)] // Example capacity: Adjust based on expected max selection size
    public NetworkArray<NetworkId> SelectedUnitIds => default;// {get; set;}

    // Optional: Include NetworkButtons if you need to sync raw button states.
    // For this specific command structure, it might be redundant as the CommandType
    // and SelectedUnitIds capture the intent, but it's common in INetworkInput.
    public NetworkButtons Buttons;
}
