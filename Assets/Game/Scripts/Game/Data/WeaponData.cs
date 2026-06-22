using UnityEngine;
using Zenject;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Scriptable Objects/WeaponData")]
public class WeaponData : ScriptableObject
{
    public enum WearType
    {
        OneHanded,
        TwoHanded,
        Additional
    }
    [field: SerializeField] public GameObject ViewPrefab { get; set; }
    [field: SerializeField] public GameObject GroundPrefab { get; set; }
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

    public GameObject GetGroundInstance(Transform parent, DiContainer container)
    {
        if (GroundPrefab == null) return null;

        var instance = container.InstantiatePrefab(GroundPrefab, parent);
        instance.transform.SetLocalPositionAndRotation(Vector3.one, Quaternion.identity);
        instance.transform.SetParent(null);
        return instance;
    }
}
