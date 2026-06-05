using UnityEngine;
using UnityEngine.AI;

namespace Game
{
    [DefaultExecutionOrder(-1)]
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";
        
        public bool interpolateTurning = false;
        public bool applyAnimationRotation = false;

        public Animator animator { get { return m_Animator; } }
        public Vector3 externalForce { get { return m_ExternalForce; } }
        public NavMeshAgent navmeshAgent { get { return m_NavMeshAgent; } }
        public bool followNavmeshAgent { get { return m_FollowNavmeshAgent; } }
        public bool grounded { get { return m_Grounded; } }

        protected NavMeshAgent m_NavMeshAgent;
        protected bool m_FollowNavmeshAgent;
        protected Animator m_Animator;
        protected bool m_UnderExternalForce;
        protected bool m_ExternalForceAddGravity = true;
        protected Vector3 m_ExternalForce;
        protected bool m_Grounded;
        
        const float k_GroundedRayDistance = .8f;
        private Collider m_BlockerCollider;

        private Vector3 m_VerticalVelocity = Vector3.zero;
        private const float Gravity = -28f;

        void OnEnable()
        {
            m_NavMeshAgent = GetComponent<NavMeshAgent>();
            m_Animator = GetComponent<Animator>();
            m_Animator.updateMode = AnimatorUpdateMode.Fixed;

            m_NavMeshAgent.updatePosition = false;
            
            m_BlockerCollider = GetComponent<Collider>();
            if (m_BlockerCollider == null)
                m_BlockerCollider = GetComponentInChildren<Collider>();

            if (m_BlockerCollider != null)
                m_BlockerCollider.isTrigger = true;

            m_FollowNavmeshAgent = true;
        }

        private void FixedUpdate()
        {
            animator.speed = CharacterInput.Instance != null && CharacterInput.Instance.HaveControl() ? 1.0f : 0.0f;

            CheckGrounded();
            ApplyGravity();
        }

        private void CheckGrounded()
        {
            Vector3 origin = transform.position + k_GroundedRayDistance * 0.5f * Vector3.up;
           
            m_Grounded = Physics.Raycast(origin, Vector3.down, out var hit, k_GroundedRayDistance, 
                                         Physics.AllLayers, QueryTriggerInteraction.Collide);
        }

        private void ApplyGravity()
        {
            if (m_UnderExternalForce)
                return;

            if (m_Grounded && m_VerticalVelocity.y <= 0)
            {
                m_VerticalVelocity.y = -2f; 
            }
            else
            {
                m_VerticalVelocity.y += Gravity * Time.deltaTime;
            }

            if (!m_Grounded || m_VerticalVelocity.y > 0)
            {
                transform.position += m_VerticalVelocity * Time.deltaTime;
            }
        }

        private void OnAnimatorMove()
        {
            if (m_UnderExternalForce)
                return;

            var deltaPosition = m_Animator.deltaPosition;

            if (m_FollowNavmeshAgent)
            {
                m_NavMeshAgent.speed = deltaPosition.magnitude / Time.deltaTime;
                transform.position = m_NavMeshAgent.nextPosition;
            }
            else
            {
                if (deltaPosition.sqrMagnitude > 0.0001f)
                {
                    var dir = deltaPosition.normalized;
                    var dist = deltaPosition.magnitude;

                    if (!Physics.SphereCast(transform.position + Vector3.up * 0.8f, 
                            0.45f, dir, out var hit, dist * 1.2f) || 
                        !hit.collider.CompareTag(playerTag))
                    {
                        transform.position += deltaPosition;
                    }
                }
            }

            if (applyAnimationRotation)
            {
                transform.forward = m_Animator.deltaRotation * transform.forward;
            }
        }
        
        private void OnTriggerStay(Collider other)
        {
            if (!other.CompareTag(playerTag)) 
                return;

            var pushDir = (other.transform.position - transform.position).normalized;
            pushDir.y = 0;

            var cc = other.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.Move(pushDir * 3.5f * Time.deltaTime); 
            }
        }

        public void SetFollowNavmeshAgent(bool follow)
        {
            if (!follow && m_NavMeshAgent.enabled)
            {
                m_NavMeshAgent.ResetPath();
            }
            else if (follow && !m_NavMeshAgent.enabled)
            {
                m_NavMeshAgent.Warp(transform.position);
            }

            m_FollowNavmeshAgent = follow;
            m_NavMeshAgent.enabled = follow;
        }

        public void AddForce(Vector3 force, bool useGravity = true)
        {
            if (m_NavMeshAgent.enabled)
                m_NavMeshAgent.ResetPath();

            m_ExternalForce = force;
            m_NavMeshAgent.enabled = false;
            m_UnderExternalForce = true;
            m_ExternalForceAddGravity = useGravity;

            m_VerticalVelocity = force;
        }

        public void ClearForce()
        {
            m_UnderExternalForce = false;
            m_NavMeshAgent.enabled = true;
        }

        public void SetForward(Vector3 forward)
        {
            Quaternion targetRotation = Quaternion.LookRotation(forward);

            if (interpolateTurning)
            {
                targetRotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
                    m_NavMeshAgent.angularSpeed * Time.deltaTime);
            }

            transform.rotation = targetRotation;
        }

        public bool SetTarget(Vector3 position)
        {
            return m_NavMeshAgent.SetDestination(position);
        }
    }
}