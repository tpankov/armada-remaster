using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // Cache the main camera for efficiency
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Billboard script cannot find main camera!", this);
        }
    }

    // Use LateUpdate to ensure camera movement has finished for the frame
    void LateUpdate()
    {
        if (mainCamera != null)
        {
            // Option 1: Make the object look directly at the camera (standard billboard)
            // transform.LookAt(mainCamera.transform);

            // Option 2: Make the object's rotation match the camera's rotation
            // This prevents slight flipping when the camera passes directly over/under
            //transform.rotation = mainCamera.transform.rotation;

            // Option 3: Look at camera but constrain rotation (e.g., only rotate on Y axis)
            Vector3 targetPos = mainCamera.transform.position;
            targetPos.y = transform.position.y; // Keep sprite upright
            transform.LookAt(targetPos);
        }
         else if (Camera.main != null)
        {
             // Attempt to find camera again if it became available
             mainCamera = Camera.main;
        }
    }
}