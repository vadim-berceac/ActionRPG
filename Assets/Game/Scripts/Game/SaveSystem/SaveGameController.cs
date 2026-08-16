using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using Object = UnityEngine.Object;

public class SaveGameController
{
    [Inject] private readonly SaveService _saveService;

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
        var saveFile = new SaveFile
        {
            Version = 1,
            SlotInfo = new SaveSlotInfo { SlotName = slotName, SavedAt = DateTime.UtcNow, DisplayName = slotName },
            SceneName = currentScene.name,
            Characters = characters.Select(c => c.Capture()).ToList()
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

        if (!string.IsNullOrEmpty(saveFile.SceneName))
        {
            var currentScene = SceneManager.GetActiveScene();
            if (currentScene.name != saveFile.SceneName)
            {
                await SceneManager.LoadSceneAsync(saveFile.SceneName);

                var characters = Object.FindObjectsByType<SaveableCharacter>(FindObjectsSortMode.None);
                charactersById = characters.ToDictionary(c => c.SaveKey, c => c);
            }
        }

        foreach (var characterState in saveFile.Characters)
        {
            if (charactersById.TryGetValue(characterState.SaveKey, out var character))
            {
                character.Restore(characterState);
            }
        }
    }
}