using UnityEngine;

namespace Game
{
    public abstract class Projectile : MonoBehaviour, IPooled<Projectile>
    {
        public int PoolID { get; set; }
        [field: SerializeField] public int Count { get; set; }
        public ObjectPooler<Projectile> Pool { get; set; }

        public virtual void SetData(WeaponData data){}
        public abstract void Shot(Vector3 target, RangeWeapon shooter);
        public abstract LayerMask GetDamageLayerMask();
    }
}