using UnityEngine;
using Zenject;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Scriptable Objects/WeaponData")]
public class WeaponData : ItemData
{
    public enum WearType
    {
        OneHanded,
        TwoHanded,
        Additional,
        Ranged,
        Ammunition
    }

    [System.Serializable]
    public struct StaticPartSettings
    {
        [field: SerializeField] public GameObject Prefab { get; set; }
        [field: SerializeField] public PropBoneSettings BoneSettings { get; set; }
    }
    
    [field: SerializeField] public GameObject ViewPrefab { get; set; }
    [field: SerializeField] public PropBoneSettings ActiveProp { get; set; }
    [field: SerializeField] public PropBoneSettings UnActiveProp { get; set; }
    [field: SerializeField] public string[] ComboNames { get; private set; }
    [field: SerializeField, Range(0, 10)] public int AnimationSetIndex { get; private set; }
    [field: SerializeField] public WearType Wear { get; private set; }
    [field: SerializeField] public StaticPartSettings[] StaticParts { get; private set; }
    [field: SerializeField] public float knockbackForce = 5f;

    public GameObject GetViewInstance(Transform parent, DiContainer container)
    {
       return CreateInstance(ViewPrefab, parent, container);
    }

    public GameObject[] GetStaticParts(PropBones bones, DiContainer container)
    {
        if (StaticParts == null || StaticParts.Length == 0)
        {
            return null;
        }

        var results = new GameObject[StaticParts.Length];

        for (var i = 0; i < StaticParts.Length; i++)
        {
            var instance = container.InstantiatePrefab(StaticParts[i].Prefab);
            results[i] = instance;
            StaticParts[i].BoneSettings.SetPropBone(instance.transform, bones);
        }
        
        return results;
    }

    private static GameObject CreateInstance(GameObject prefab, Transform parent, DiContainer container)
    {
        if (prefab == null)
        {
            return null;
        }
        var instance = container.InstantiatePrefab(prefab, parent);
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        return instance;
    }
}
