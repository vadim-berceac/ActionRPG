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

        public RangedAttackHandler(RangeWeapon rangeWeapon, Damageable owner, LayerMask targetLayer)
        {
            _rangeWeapon = rangeWeapon;
            _owner = owner;
        }

        /// <summary>
        /// Fire at the specified world-space target position.
        /// </summary>
        public void Shoot(Vector3 targetPosition)
        {
            if (_rangeWeapon == null)
                return;

            _rangeWeapon.Attack(targetPosition, _owner);
        }

        /// <summary>
        /// Whether the handler has a valid weapon assigned.
        /// </summary>
        public bool IsValid => _rangeWeapon != null;
    }
}
