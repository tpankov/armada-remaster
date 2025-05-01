http://googleusercontent.com/immersive_entry_chip/0


**2. C# Configuration Script (`ContextMenuController_UIToolkit.cs`)**

This script brings the UI to life. Attach it to the same GameObject that has the `UIDocument` component for your context menu.


**Key Changes and Considerations for UI Toolkit:**

1.  **UXML (Optional):** While you *can* create the entire structure in C#, using a simple UXML file (`ContextMenu.uxml`) to define the main container and potentially the named sub-containers (`command-button-container`, `ship-icon-container`, etc.) makes querying elements in C# (`_root.Q<VisualElement>("...")`) cleaner.
2.  **Stylesheet:** The USS file (`ContextMenuStyles.uss`) defines all the visuals. You link this in the C# script or directly in the `UIDocument` component Inspector.
3.  **`UIDocument` Component:** You need this component on a GameObject in your scene to host the UI Toolkit interface. Assign your UXML asset (if using one) to its `Source Asset` field.
4.  **C# Controller Script:** Attach the `ContextMenuController_UIToolkit.cs` script (likely to the same GameObject as the `UIDocument`). Assign the required references (UIDocument, StyleSheet, PlayerInputHandler, Camera, Icon Textures) in the Inspector.
5.  **Dynamic Creation:** The script now dynamically creates `Button` and `VisualElement` objects in C# for commands and ship icons, applying USS classes and positioning them using calculated coordinates. It also clears/removes these elements when the menu is hidden or updated. Status bars/icons can also be created dynamically or queried if defined in UXML.
6.  **Positioning:** Uses `RuntimePanelUtils.CameraTransformWorldToPanel` to map the world position of the target unit onto the UI panel's coordinate system. Absolute positioning with `style.left`, `style.top`, and `style.translate` is used to place the menu and its circular elements.
7.  **Event Handling:** Button clicks are handled using the `.clicked += () => { ... }` syntax.
8.  **Data Updates:** Status bars (`_healthBarFill.style.width`), icon tints (`iconElement.AddToClassList(...)`), etc., are updated directly via C# styles in the `UpdateStatusInfo` method.
9.  **Input Blocking:** UI Toolkit's event system generally handles blocking input to the game world when interacting with UI elements that have `pickingMode` set to `Position` (which is default for Buttons, etc.). You might not need the explicit `EventSystem.current.IsPointerOverGameObject()` check in `PlayerInputHandler`, but thorough testing is recommended.
10. **Integration:** Remember to update `PlayerInputHandler` to call `ContextMenuController_UIToolkit.Instance.ShowMenu(...)` and `HideMenu()` instead of the UGUI version's methods.

This provides a complete setup using UI Toolkit, offering more flexibility and modern features compared to UGUI for complex UI like this. Remember to create the icon textures and link everything correctly in the Unity Editor.