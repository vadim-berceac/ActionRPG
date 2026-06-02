using UnityEngine;

namespace Game
{
    [DefaultExecutionOrder(9999)]
    public class FixedUpdateFollow : MonoBehaviour
    {
        [SerializeField] private Transform source;
        [SerializeField] private Transform toFollow;

        private void FixedUpdate()
        {
            source.position = toFollow.position;
            source.rotation = toFollow.rotation;
        }
    } 
}
