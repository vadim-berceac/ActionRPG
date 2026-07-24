using UnityEngine;

namespace Game
{
    /// <summary>
    /// Non-MonoBehaviour handler for ranged weapon attacks.
    /// Encapsulates the logic of shooting via RangeWeapon.
    /// </summary>
    public class RangedAttackHandler
    {
        private readonly RangeWeapon _rangeWeapon;
        private readonly Damageable _owner;
        private LayerMask _targetLayer;

        public RangedAttackHandler(RangeWeapon rangeWeapon, Damageable owner, LayerMask targetLayer)
        {
            _rangeWeapon = rangeWeapon;
            _owner = owner;
            _targetLayer = targetLayer;
        }

        /// <summary>
        /// Fire at the specified world-space target position.
        /// </summary>
        public void Shoot(Vector3 targetPosition)
        {
            if (_rangeWeapon == null)
                return;

            // Set target layer so the projectile damages only objects on this layer
            _rangeWeapon.projectileLayerMask = _targetLayer;
            _rangeWeapon.Attack(targetPosition, _owner);
        }

        /// <summary>
        /// Update the target layer mask (e.g. when weapon owner changes layers).
        /// </summary>
        public void SetTargetLayer(LayerMask targetLayer)
        {
            _targetLayer = targetLayer;
        }

        /// <summary>
        /// Whether the handler has a valid weapon assigned.
        /// </summary>
        public bool IsValid => _rangeWeapon != null;
    }
}
