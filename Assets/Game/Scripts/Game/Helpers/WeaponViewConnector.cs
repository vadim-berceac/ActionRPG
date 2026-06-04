using UnityEngine;

namespace Game
{
    [DefaultExecutionOrder(9999)]
    public class WeaponViewConnector : MonoBehaviour
    {
        [SerializeField] private Transform source;
        [SerializeField] private Transform toFollow;

        public void SetPropBone(Transform bone)
        {
            toFollow = bone;
            source.parent = toFollow;
            source.localPosition = Vector3.zero;
            source.localScale = Vector3.one;
        }
    } 
}
