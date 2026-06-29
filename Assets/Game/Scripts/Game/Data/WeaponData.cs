using UnityEngine;
using Zenject;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Scriptable Objects/WeaponData")]
public class WeaponData : ItemData
{
    public enum WearType
    {
        OneHanded,
        TwoHanded,
        Additional
    }
    [field: SerializeField] public GameObject ViewPrefab { get; set; }
    [field: SerializeField] public PropBoneSettings ActiveProp { get; set; }
    [field: SerializeField] public PropBoneSettings UnActiveProp { get; set; }
    [field: SerializeField] public string[] ComboNames { get; private set; }
    [field: SerializeField, Range(0, 10)] public int AnimationSetIndex { get; private set; }
    [field: SerializeField] public WearType Wear { get; private set; }

    public GameObject GetViewInstance(Transform parent, DiContainer container)
    {
        if (ViewPrefab == null)
        {
            return null;
        }
        var instance = container.InstantiatePrefab(ViewPrefab, parent);
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        return instance;
    }
}
