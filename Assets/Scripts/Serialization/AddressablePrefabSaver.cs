using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.Threading.Tasks;

public static class AddressablePrefabSaver
{
    public static async Task SaveModelAsAddressable(GameObject model, string prefabName)
    {
        string addressablePath = $"Assets/AddressablePrefabs/{prefabName}.prefab";
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(model, addressablePath);

        if (savedPrefab == null)
        {
            Debug.LogError("Failed to save prefab: " + prefabName);
            return;
        }

        Debug.Log("Prefab saved at: " + addressablePath);

        // Add prefab to Addressables
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        AddressableAssetGroup group = settings.FindGroup("ProceduralModels") ??
            settings.CreateGroup("ProceduralModels", false, false, false, null);

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(addressablePath), group);
        entry.address = prefabName;

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        await Task.Yield();
        Debug.Log("Prefab added to Addressables: " + prefabName);
    }
}
