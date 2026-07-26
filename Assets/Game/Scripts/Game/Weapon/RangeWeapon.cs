using UnityEngine;

namespace Game
{
    public class RangeWeapon : MonoBehaviour
    {
        public enum Mode
        {
            Standard,
            ScreenCenter
        }

        public Mode mode = Mode.Standard;
        public Vector3 muzzleOffset;
        public Projectile projectile;

        [System.NonSerialized] public LayerMask projectileLayerMask = -1;

        public Damageable Owner { get; private set; }

        public Projectile loadedProjectile
        {
            get { return m_LoadedProjectile; }
        }

        protected Projectile m_LoadedProjectile = null;
        protected ObjectPooler<Projectile> m_ProjectilePool;

        private void Start()
        {
            m_ProjectilePool = new ObjectPooler<Projectile>();
            m_ProjectilePool.Initialize(20, projectile);
        }

        public void Attack(Vector3 target, Damageable owner = null)
        {
            Owner = owner;
            AttackProjectile(target);
        }

        public void LoadProjectile()
        {
            if (m_LoadedProjectile != null)
                return;

            m_LoadedProjectile = m_ProjectilePool.GetNew();
            m_LoadedProjectile.transform.SetParent(transform, false);
            m_LoadedProjectile.transform.localPosition = muzzleOffset;
            m_LoadedProjectile.transform.localRotation = Quaternion.identity;
        }

        private void AttackProjectile(Vector3 target)
        {
            if (m_LoadedProjectile == null) LoadProjectile();

            m_LoadedProjectile.transform.SetParent(null, true);

            if (mode == Mode.ScreenCenter)
            {
                var cam = Camera.main;
                if (cam)
                {
                    var ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
                    if (Physics.Raycast(ray, out var hit, 100f, projectileLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        target = hit.point;
                    }
                    else
                    {
                        target = ray.origin + ray.direction * 100f;
                    }
                }
            }

            m_LoadedProjectile.Shot(target, this);
            m_LoadedProjectile = null;
        }
    }
}