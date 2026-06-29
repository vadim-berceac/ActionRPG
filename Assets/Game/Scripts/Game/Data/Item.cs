using UnityEngine;
using Zenject;

public interface IItemData
{
    public string Name { get;}
    public Sprite Icon { get;}
    public GameObject GroundPrefab { get;}
    public string Description { get;}
    public bool CanBeEquipped { get;}
    public GameObject GetGroundInstance(Transform parent, DiContainer container);
}

public abstract class ItemData : ScriptableObject, IItemData
{
    [field: SerializeField] public string Name { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
    [field: SerializeField] public GameObject GroundPrefab { get; set; }
    [field: SerializeField, TextArea(3, 6)] public string Description { get; private set; }
    [field: SerializeField] public bool CanBeEquipped { get; private set; }
    
    public GameObject GetGroundInstance(Transform parent, DiContainer container)
    {
        if (GroundPrefab == null) return null;

        var instance = container.InstantiatePrefab(GroundPrefab, parent);
        instance.transform.SetLocalPositionAndRotation(Vector3.one, Quaternion.identity);
        instance.transform.SetParent(null);
        return instance;
    }
}