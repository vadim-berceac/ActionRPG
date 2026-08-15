using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Scriptable Objects/ItemDatabase")]
public class ItemDatabase : ScriptableObject, IItemDatabase
{
    [SerializeField] private ItemData[] itemData;
    
    public ItemData GetByName(string itemName)
    {
        return itemData.FirstOrDefault(item => item.name == itemName);
    }
}

public interface IItemDatabase
{
    ItemData GetByName(string itemName);
}
