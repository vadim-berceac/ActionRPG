using UnityEngine;
using Zenject;

public class SaveLoad : MonoBehaviour
{
    [Inject] private readonly SaveGameController _saveGameController;

    public void Load(string slotName)
    {
        _saveGameController.LoadGame(slotName);
    }

    public void Save(string slotName)
    {
        _saveGameController.SaveGame(slotName);
    }
}
