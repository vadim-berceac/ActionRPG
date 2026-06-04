using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Scriptable Objects/WeaponData")]
public class WeaponData : ScriptableObject
{
    [field: SerializeField] public GameObject ViewPrefab { get; set; }
    [field: SerializeField] public GameObject GroundPrefab { get; set; }
    [field: SerializeField] public PropBoneSettings ActiveProp { get; set; }
    [field: SerializeField] public PropBoneSettings UnActiveProp { get; set; }
    [field: SerializeField] public string[] ComboNames { get; private set; }

    public GameObject GetViewInstance(Transform parent)
    {
        if (ViewPrefab == null)
        {
            return null;
        }
        var instance = Instantiate(ViewPrefab, parent);
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        return instance;
    }
}
