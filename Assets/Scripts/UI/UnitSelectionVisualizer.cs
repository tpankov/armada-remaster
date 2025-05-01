using UnityEngine;
using Fusion;
using System.Collections.Generic;

/// <summary>
/// Manages the visual representation of selected units using ground circles.
/// Uses object pooling for the circle visuals.
/// Should be placed on a manager GameObject in the scene.
/// </summary>
public class UnitSelectionVisualizer : MonoBehaviour
{
    // --- Singleton Instance ---
    private static UnitSelectionVisualizer _instance;
    public static UnitSelectionVisualizer Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<UnitSelectionVisualizer>();
            if (_instance == null)
                Debug.LogError("UnitSelectionVisualizer instance not found in the scene.");
            return _instance;
        }
    }

    [Header("Configuration")]
    [SerializeField] private GameObject selectionCirclePrefab; // Assign your prepared circle prefab
    [SerializeField] private LayerMask groundLayerMask = 1;    // Set to your Ground layer
    [SerializeField] private float yOffset = 0.05f;          // Small offset to prevent Z-fighting
    [SerializeField] private int initialPoolSize = 20;       // Starting size of the object pool

    // --- Object Pool ---
    private List<GameObject> _pooledCircles;
    private int _nextAvailableIndex = 0;

    // --- Active Visuals Tracking ---
    // Maps the NetworkId of a selected unit to its active circle GameObject from the pool
    private Dictionary<NetworkId, GameObject> _activeCircles = new Dictionary<NetworkId, GameObject>();

    // Keep track of IDs processed this frame to find deselected units efficiently
    private HashSet<NetworkId> _processedIdsThisFrame = new HashSet<NetworkId>();

    #region Unity Lifecycle

    private void Awake()
    {
        // --- Singleton Enforcement ---
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        InitializePool();
    }

    private void OnDestroy()
    {
        // Cleanup pool
        if (_pooledCircles != null)
        {
            foreach (var circle in _pooledCircles)
            {
                if (circle != null) Destroy(circle);
            }
            _pooledCircles.Clear();
        }
        _activeCircles.Clear();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    #endregion

    #region Object Pooling

    private void InitializePool()
    {
        _pooledCircles = new List<GameObject>(initialPoolSize);
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateAndPoolCircle();
        }
        _nextAvailableIndex = 0; // Start from the beginning of the pool
    }

    private GameObject CreateAndPoolCircle()
    {
        if (selectionCirclePrefab == null)
        {
            Debug.LogError("SelectionCirclePrefab is not assigned!", this);
            return null;
        }
        GameObject circleInstance = Instantiate(selectionCirclePrefab, transform); // Parent to manager initially
        circleInstance.SetActive(false); // Start deactivated
        _pooledCircles.Add(circleInstance);
        return circleInstance;
    }

    private GameObject GetCircleFromPool()
    {
        // Search for an inactive object in the pool
        for (int i = 0; i < _pooledCircles.Count; i++)
        {
            // Use round-robin approach to find next available
            int index = (_nextAvailableIndex + i) % _pooledCircles.Count;
            if (!_pooledCircles[index].activeInHierarchy)
            {
                _nextAvailableIndex = (index + 1) % _pooledCircles.Count; // Update starting point for next search
                // Debug.Log($"Reusing circle from pool index {index}");
                return _pooledCircles[index];
            }
        }

        // If no inactive object found, expand the pool
        Debug.LogWarning("Expanding selection circle pool.", this);
        GameObject newCircle = CreateAndPoolCircle();
        _nextAvailableIndex = (_pooledCircles.Count) % _pooledCircles.Count; // Should point just after the new one
        return newCircle; // Return the newly created (and currently inactive) object
    }

    private void ReturnCircleToPool(GameObject circleInstance)
    {
        if (circleInstance != null)
        {
            circleInstance.SetActive(false);
            // Optionally reset position/parent if needed, though setting active(false) is usually enough.
            // circleInstance.transform.SetParent(transform); // Re-parent to manager
        }
    }

    #endregion

    #region Public Update Method

    /// <summary>
    /// Updates the selection visuals based on the currently selected units.
    /// Should be called by PlayerInputHandler when the selection changes.
    /// </summary>
    /// <param name="runner">The active NetworkRunner needed to find unit objects.</param>
    /// <param name="selectedUnits">A HashSet containing the NetworkIds of currently selected units.</param>
    public void UpdateHighlights(NetworkRunner runner, HashSet<NetworkId> selectedUnits)
    {
        if (runner == null || selectionCirclePrefab == null) return; // Safety checks

        _processedIdsThisFrame.Clear();

        // --- Activate/Update circles for currently selected units ---
        foreach (NetworkId unitId in selectedUnits)
        {
            if (!unitId.IsValid) continue;

            _processedIdsThisFrame.Add(unitId); // Mark this ID as processed

            // Try to find the unit's NetworkObject
            if (runner.TryFindObject(unitId, out NetworkObject unitNetworkObject) && unitNetworkObject != null)
            {
                // Raycast down to find ground position
                Vector3 groundPos = GetGroundPosition(unitNetworkObject.transform.position);

                if (_activeCircles.TryGetValue(unitId, out GameObject circleInstance))
                {
                    // Circle already exists for this unit, update its position and ensure active
                    if (!circleInstance.activeSelf)
                    {
                        circleInstance.SetActive(true); // Reactivate if it was somehow deactivated
                    }
                    circleInstance.transform.position = groundPos + Vector3.up * yOffset;
                    // Optional: Adjust circle scale based on unit size?
                    circleInstance.transform.localScale = Vector3.one * GetUnitRadius(unitNetworkObject);
                }
                else
                {
                    // No circle exists, get one from the pool and activate it
                    GameObject newCircle = GetCircleFromPool();
                    if (newCircle != null)
                    {
                        newCircle.transform.position = groundPos + Vector3.up * yOffset;
                        // Optional: Adjust scale
                        newCircle.SetActive(true);
                        _activeCircles.Add(unitId, newCircle); // Track the newly activated circle
                    }
                }
            }
            else
            {
                // Unit object not found (maybe destroyed?), ensure any existing circle is removed
                if (_activeCircles.TryGetValue(unitId, out GameObject circleInstance))
                {
                    ReturnCircleToPool(circleInstance);
                    _activeCircles.Remove(unitId);
                }
            }
        }

        // --- Deactivate circles for units that are no longer selected ---
        // Iterate through a copy of the keys to allow removal while iterating
        List<NetworkId> currentlyActiveIds = new List<NetworkId>(_activeCircles.Keys);
        foreach (NetworkId activeId in currentlyActiveIds)
        {
            if (!_processedIdsThisFrame.Contains(activeId)) // If this active circle's ID wasn't in the selected set this frame...
            {
                // Unit is no longer selected, return its circle to the pool
                if (_activeCircles.TryGetValue(activeId, out GameObject circleInstance))
                {
                    ReturnCircleToPool(circleInstance);
                    _activeCircles.Remove(activeId); // Stop tracking it
                }
            }
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Raycasts downwards from a unit's position to find the point on the ground.
    /// </summary>
    /// <param name="unitPosition">The unit's current world position.</param>
    /// <returns>The position on the ground, or the unit's base position if ground not hit.</returns>
    private Vector3 GetGroundPosition(Vector3 unitPosition)
    {
        // // Start raycast slightly above the unit's pivot
        // Vector3 rayStart = unitPosition + Vector3.up * 1.0f;
        // if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, groundLayerMask))
        // {
        //     return hit.point;
        // }
        // Fallback: return unit's position projected onto Y=0 plane if ground not hit directly below
        return new Vector3(unitPosition.x, 0.1f, unitPosition.z);
    }

    // Optional: Helper to get unit size for scaling circle
    private float GetUnitRadius(NetworkObject unitObject)
    {
        // Example: Get bounds or use a specific property
        Collider col = unitObject.GetComponent<Collider>();
        if (col != null) return Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
        return 1.0f; // Default radius
    }

    #endregion
}
