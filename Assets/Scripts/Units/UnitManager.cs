using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq; // Used for convenience methods like ToList() if needed

/// <summary>
/// Singleton Manager responsible for tracking all active UnitControllers in the scene.
/// Provides efficient lookups for all units or units of a specific class.
/// Units must register/deregister themselves via the UnitController script.
/// </summary>
public class UnitManager : MonoBehaviour
{
    // --- Singleton Instance ---
    private static UnitManager _instance;
    public static UnitManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Optional: Try to find an existing instance in the scene
                _instance = FindFirstObjectByType<UnitManager>();
                if (_instance == null)
                {
                    // Optional: Create a new instance if none exists
                    // GameObject singletonObject = new GameObject("UnitManager");
                    // _instance = singletonObject.AddComponent<UnitManager>();
                    Debug.LogError("UnitManager instance not found in the scene.");
                }
            }
            return _instance;
        }
    }

    // --- Data Structures ---
    // Stores the NetworkId of all currently active units.
    private readonly HashSet<NetworkId> _allUnitIds = new HashSet<NetworkId>();

    // Maps a unit class identifier (string) to a set of NetworkIds belonging to that class.
    private readonly Dictionary<string, HashSet<NetworkId>> _unitsByClass = new Dictionary<string, HashSet<NetworkId>>();

    #region Unity Lifecycle

    private void Awake()
    {
        // --- Singleton Enforcement ---
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("Duplicate UnitManager instance detected. Destroying self.");
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Optional: Persist across scene loads if needed
        // DontDestroyOnLoad(gameObject);

        Debug.Log("UnitManager Initialized.");
    }

    private void OnDestroy()
    {
        // Clear instance if this object is destroyed
        if (_instance == this)
        {
            _instance = null;
            Debug.Log("UnitManager Destroyed.");
        }
    }

    #endregion

    #region Registration & Deregistration (Called by UnitController)

    /// <summary>
    /// Registers a unit with the manager. Called from UnitController.Spawned().
    /// </summary>
    /// <param name="unit">The UnitController instance spawning.</param>
    public void RegisterUnit(UnitController unit)
    {
        if (unit == null || !unit.Object || !unit.Object.IsValid)
        {
            Debug.LogWarning("Attempted to register an invalid unit.");
            return;
        }

        NetworkId id = unit.Object.Id;
        string unitClass = unit.ShipClass; // Assuming UnitController has ShipClass property

        // Add to the main set
        _allUnitIds.Add(id);

        // Add to the class-specific set
        if (!_unitsByClass.TryGetValue(unitClass, out HashSet<NetworkId> classSet))
        {
            // If class doesn't exist in dictionary yet, create a new set
            classSet = new HashSet<NetworkId>();
            _unitsByClass[unitClass] = classSet;
        }
        classSet.Add(id);

        // Debug.Log($"Unit Registered: ID={id}, Class={unitClass}. Total={_allUnitIds.Count}");
    }

    /// <summary>
    /// Deregisters a unit from the manager. Called from UnitController.Despawned().
    /// </summary>
    /// <param name="unit">The UnitController instance despawning.</param>
    public void DeregisterUnit(UnitController unit)
    {
         if (unit == null || !unit.Object) // Don't check IsValid here, as it might be false during despawn
        {
            // It's possible the unit or its NetworkObject is already partially destroyed during Despawned call.
            // We might need to rely solely on NetworkId if the unit reference becomes unreliable.
            // For now, proceed if unit is not null.
             if (unit == null) return;
        }


        NetworkId id = unit.Object.Id; // Get ID even if object is being destroyed
        string unitClass = unit.ShipClass;

        // Remove from the main set
        _allUnitIds.Remove(id);

        // Remove from the class-specific set
        if (_unitsByClass.TryGetValue(unitClass, out HashSet<NetworkId> classSet))
        {
            classSet.Remove(id);

            // Optional: Clean up dictionary if a class set becomes empty
            // if (classSet.Count == 0)
            // {
            //     _unitsByClass.Remove(unitClass);
            // }
        }

        // Debug.Log($"Unit Deregistered: ID={id}, Class={unitClass}. Remaining={_allUnitIds.Count}");
    }

    #endregion

    #region Public Query Methods

    /// <summary>
    /// Gets the NetworkId of all currently registered units.
    /// </summary>
    /// <returns>An enumerable collection of NetworkIds.</returns>
    public IEnumerable<NetworkId> GetAllUnitIds()
    {
        // Return a defensive copy or readonly collection if modification during iteration is a concern
        // For simplicity, returning the direct enumerator here.
        return _allUnitIds;
    }

    /// <summary>
    /// Gets the NetworkIds of all currently registered units belonging to a specific class.
    /// </summary>
    /// <param name="shipClass">The class identifier (e.g., "Fighter", "CapitalShip").</param>
    /// <returns>An enumerable collection of NetworkIds, or an empty enumerable if the class is not found.</returns>
    public IEnumerable<NetworkId> GetUnitIdsByClass(string shipClass)
    {
        if (_unitsByClass.TryGetValue(shipClass, out HashSet<NetworkId> classSet))
        {
            return classSet;
        }
        // Return an empty collection if the class key doesn't exist
        return Enumerable.Empty<NetworkId>();
    }

    /// <summary>
    /// Attempts to find the UnitController associated with a given NetworkId using the provided NetworkRunner.
    /// </summary>
    /// <param name="runner">The NetworkRunner instance to use for lookup.</param>
    /// <param name="id">The NetworkId of the unit to find.</param>
    /// <returns>The UnitController component if found and valid, otherwise null.</returns>
    public UnitController GetUnitController(NetworkRunner runner, NetworkId id)
    {
        if (runner == null || !id.IsValid) return null;

        if (runner.TryFindObject(id, out NetworkObject networkObject) && networkObject != null)
        {
            return networkObject.GetComponent<UnitController>();
        }
        return null;
    }

    /// <summary>
    /// Gets all currently active UnitController instances using the provided NetworkRunner.
    /// Note: This involves iterating and resolving IDs, potentially less performant than working with IDs directly.
    /// </summary>
    /// <param name="runner">The NetworkRunner instance to use for lookup.</param>
    /// <returns>An enumerable collection of active UnitController instances.</returns>
    public IEnumerable<UnitController> GetAllUnitControllers(NetworkRunner runner)
    {
        if (runner == null) yield break; // Return empty if runner is invalid

        // Use a temporary list to avoid issues if the collection is modified during iteration
        List<NetworkId> currentIds = new List<NetworkId>(_allUnitIds);

        foreach (NetworkId id in currentIds)
        {
            if (runner.TryFindObject(id, out NetworkObject networkObject) && networkObject != null)
            {
                UnitController unitController = networkObject.GetComponent<UnitController>();
                if (unitController != null)
                {
                    yield return unitController;
                }
            }
            // else: Unit might have been destroyed between getting the ID list and resolving it. Skip.
        }
    }

    /// <summary>
    /// Gets all currently active UnitController instances of a specific class using the provided NetworkRunner.
    /// Note: This involves iterating and resolving IDs, potentially less performant than working with IDs directly.
    /// </summary>
    /// <param name="runner">The NetworkRunner instance to use for lookup.</param>
    /// <param name="shipClass">The class identifier.</param>
    /// <returns>An enumerable collection of active UnitController instances of the specified class.</returns>
    public IEnumerable<UnitController> GetUnitControllersByClass(NetworkRunner runner, string shipClass)
    {
        if (runner == null) yield break; // Return empty if runner is invalid

        if (_unitsByClass.TryGetValue(shipClass, out HashSet<NetworkId> classSet))
        {
            // Use a temporary list to avoid issues if the collection is modified during iteration
            List<NetworkId> currentIds = new List<NetworkId>(classSet);

            foreach (NetworkId id in currentIds)
            {
                 if (runner.TryFindObject(id, out NetworkObject networkObject) && networkObject != null)
                 {
                     UnitController unitController = networkObject.GetComponent<UnitController>();
                     if (unitController != null)
                     {
                         yield return unitController;
                     }
                 }
                 // else: Unit might have been destroyed. Skip.
            }
        }
        // else: Class not found, implicitly returns empty enumerable.
    }


    #endregion
}
