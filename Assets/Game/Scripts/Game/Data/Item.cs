using UnityEngine;

public interface IItemData
{
    public string Name { get;}
    public Sprite Icon { get;}
    public string Description { get;}
    public bool CanBeEquipped { get;}
}

public abstract class ItemData : ScriptableObject, IItemData
{
    [field: SerializeField] public string Name { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
    [field: SerializeField, TextArea(3, 6)] public string Description { get; private set; }
    [field: SerializeField] public bool CanBeEquipped { get; private set; }
}