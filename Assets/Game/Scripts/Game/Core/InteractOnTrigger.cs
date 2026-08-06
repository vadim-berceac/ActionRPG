using UnityEngine;
using UnityEngine.Events;

namespace Game
{
    [RequireComponent(typeof(Collider))]
    public class InteractOnTrigger : MonoBehaviour
    {
        public UnityEvent OnEnter, OnExit;

        private void OnTriggerEnter(Collider other)
        {
            ExecuteOnEnter(other);
        }
        
        private void OnTriggerExit(Collider other)
        {
            ExecuteOnExit(other);
        }

        protected virtual void ExecuteOnEnter(Collider other)
        {
            OnEnter.Invoke();
        }

        protected virtual void ExecuteOnExit(Collider other)
        {
            OnExit.Invoke();
        }

        protected virtual void OnDestroy()
        {
            OnExit?.Invoke();
        }
    } 
}
