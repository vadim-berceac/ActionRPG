using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using Object = UnityEngine.Object;

public class SaveGameController
{
    [Inject] private readonly SaveService _saveService;
    [Inject] private readonly PickupPersistenceService _pickupPersistence;
    [Inject] private readonly IItemDatabase _itemDatabase;
    [Inject] private readonly SceneContextRegistry _sceneContextRegistry;

    public void SaveGame(string slotName)
    {
        var characters = Object.FindObjectsByType<SaveableCharacter>(FindObjectsSortMode.None);
        SaveGameAsync(slotName, characters).Forget();
    }

    public void LoadGame(string slotName)
    {
        var characters = Object.FindObjectsByType<SaveableCharacter>(FindObjectsSortMode.None);
        var charactersById = characters.ToDictionary(c => c.SaveKey, c => c);

        LoadGameAsync(slotName, charactersById).Forget();
    }

    public async UniTask<List<SaveSlotInfo>> GetAvailableSlots()
    {
        return await _saveService.GetAllSlotInfosAsync();
    }

    private async UniTask SaveGameAsync(string slotName, IEnumerable<SaveableCharacter> characters)
    {
        var currentScene = SceneManager.GetActiveScene();
        var runtimePickups = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None)
            .Where(p => p.IsRuntimeSpawned)
            .Select(p => p.CaptureRuntimeState())
            .ToList();

        var saveFile = new SaveFile
        {
            Version = 1,
            SlotInfo = new SaveSlotInfo { SlotName = slotName, SavedAt = DateTime.UtcNow, DisplayName = slotName },
            SceneName = currentScene.name,
            Characters = characters.Select(c => c.Capture()).ToList(),
            PickedPickupKeys = _pickupPersistence?.GetPickedKeys().ToList(),
            RuntimeActivePickups = runtimePickups
        };

        await _saveService.SaveAsync(saveFile, _saveService.GetPath(slotName));

        Debug.Log(Application.persistentDataPath);
    }

    private async UniTask LoadGameAsync(string slotName, IReadOnlyDictionary<string, SaveableCharacter> charactersById)
    {
        var saveFile = await _saveService.LoadAsync(_saveService.GetPath(slotName));

        if (saveFile == null || saveFile.Characters == null)
        {
            Debug.LogWarning($"Save file not found or invalid: {slotName}");
            return;
        }

        await SceneController.RunWithLoadingFade(async () =>
        {
            _pickupPersistence?.RestorePickedKeys(saveFile.PickedPickupKeys);

            if (!string.IsNullOrEmpty(saveFile.SceneName))
            {
                var currentScene = SceneManager.GetActiveScene();
                if (currentScene.name != saveFile.SceneName)
                {
                    await SceneManager.LoadSceneAsync(saveFile.SceneName).ToUniTask();

                    var scene = SceneManager.GetActiveScene();
                    await UniTask.WaitUntil(() => _sceneContextRegistry.TryGetContainerForScene(scene) != null);

                    var characters = Object.FindObjectsByType<SaveableCharacter>(FindObjectsSortMode.None);
                    charactersById = characters.ToDictionary(c => c.SaveKey, c => c);
                }
            }

            var pickups = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
            foreach (var pickup in pickups)
            {
                if ((_pickupPersistence?.IsPicked(pickup.SaveKey) ?? false) || pickup.IsRuntimeSpawned)
                {
                    pickup.DestroySelf();
                }
            }

            RestoreRuntimePickups(saveFile.RuntimeActivePickups);

            foreach (var characterState in saveFile.Characters)
            {
                if (charactersById.TryGetValue(characterState.SaveKey, out var character))
                {
                    character.Restore(characterState);
                }
            }
        });
    }

    private void RestoreRuntimePickups(List<PickupItem.PickupState> runtimePickups)
    {
        if (runtimePickups == null) return;

        var sceneContainer = _sceneContextRegistry?.TryGetContainerForScene(SceneManager.GetActiveScene());
        if (sceneContainer == null)
        {
            Debug.LogWarning("Unable to resolve scene container, runtime pickups will not be restored.");
            return;
        }

        foreach (var state in runtimePickups)
        {
            if (state.IsPicked) continue;

            var itemData = _itemDatabase?.GetByName(state.ItemName);
            if (itemData == null)
            {
                Debug.LogWarning($"Item '{state.ItemName}' not found in database, cannot restore runtime pickup.");
                continue;
            }

            var instance = itemData.GetGroundInstance(null, sceneContainer);
            if (instance == null) continue;

            instance.transform.position = state.Position;

            var pickup = instance.GetComponentInChildren<PickupItem>();
            if (pickup != null)
            {
                pickup.MarkRuntimeSpawned();
                pickup.SetSaveKey(state.SaveKey);
            }
        }
    }
}