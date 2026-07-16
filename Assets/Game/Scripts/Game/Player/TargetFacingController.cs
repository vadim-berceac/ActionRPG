using UnityEngine;

namespace Game
{
    public class TargetFacingController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private LayerMask _targetLayer = -1;
        [SerializeField] private float _detectionRadius = 4f;
        [SerializeField] private float _minDotProduct = 0.2f;
        [SerializeField] private float _heightOffset = 1f;

        [Header("Debug")]
        [SerializeField] private bool _showGizmos;

        private readonly Collider[] _results = new Collider[16];
        private Transform _cachedTransform;

        private void Awake()
        {
            _cachedTransform = transform;
        }
        
        public Vector3 GetDirectionToNearestTarget()
        {
            var origin = _cachedTransform.position + Vector3.up * _heightOffset;
            var count = Physics.OverlapSphereNonAlloc(origin, _detectionRadius, _results, _targetLayer);

            if (count == 0) return Vector3.zero;

            var bestDirection = Vector3.zero;
            var bestDistanceSqr = float.MaxValue;
            var currentPos = _cachedTransform.position;

            for (var i = 0; i < count; i++)
            {
                var target = _results[i];
                if (target == null || target.transform == _cachedTransform) continue;

                var direction = target.transform.position - currentPos;
                direction.y = 0f;

                var distanceSqr = direction.sqrMagnitude;
                if (distanceSqr < 0.0001f) continue;

                direction.Normalize();

                var dot = Vector3.Dot(_cachedTransform.forward, direction);
                if (dot < _minDotProduct) continue;

                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestDirection = direction;
                }
            }

            return bestDirection;
        }

        public bool HasTargetInRange()
        {
            var origin = _cachedTransform.position + Vector3.up * _heightOffset;
            var count = Physics.OverlapSphereNonAlloc(origin, _detectionRadius, _results, _targetLayer);

            for (var i = 0; i < count; i++)
            {
                if (_results[i] != null && _results[i].transform != _cachedTransform)
                    return true;
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos) return;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            var origin = Application.isPlaying
                ? transform.position + Vector3.up * _heightOffset
                : transform.position + Vector3.up * _heightOffset;
            Gizmos.DrawSphere(origin, _detectionRadius);

            if (!Application.isPlaying) return;

            var dir = GetDirectionToNearestTarget();
            if (dir != Vector3.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position + Vector3.up * _heightOffset, dir * _detectionRadius);
            }
        }
    }
}