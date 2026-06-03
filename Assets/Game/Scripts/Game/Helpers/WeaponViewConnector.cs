using UnityEngine;

namespace Game
{
    [DefaultExecutionOrder(9999)]
    public class WeaponViewConnector : MonoBehaviour
    {
        [SerializeField] private Transform source;
        [SerializeField] private Transform toFollow;

        private void Awake()
        {
            source.parent = toFollow;
            source.localPosition = Vector3.zero;
            source.localScale = Vector3.one;
        }
    } 
}
