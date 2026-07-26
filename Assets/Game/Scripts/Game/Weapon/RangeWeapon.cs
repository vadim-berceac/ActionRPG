using UnityEngine;

namespace Game
{
    public class RangeWeapon : MonoBehaviour
    {
        private enum Mode
        {
            Standard,
            ScreenCenter
        }

        [SerializeField] private Mode mode = Mode.Standard;
        [SerializeField] private Vector3 muzzleOffset;
        [SerializeField] private Projectile projectile;

        private WeaponData _currentProjectileData;
        private ObjectPooler<Projectile> _projectilePool;
        
        public Damageable Owner { get; private set; }
        public Projectile LoadedProjectile { get; private set; }

        public void SetData(WeaponData data)
        {
            _currentProjectileData = null;
            _projectilePool?.ClearAll();
            _projectilePool = null;
            
            _currentProjectileData = data;
            _projectilePool = new ObjectPooler<Projectile>(projectile.Count, projectile);
        }

        private void OnDisable()
        {
            _projectilePool?.ClearAll();
            _projectilePool = null;
        }

        public void Attack(Vector3 target, Damageable owner = null)
        {
            Owner = owner;
            AttackProjectile(target);
        }

        public void LoadProjectile()
        {
            if (LoadedProjectile != null)
                return;

            LoadedProjectile = _projectilePool.GetNew();
            
            LoadedProjectile.SetData(_currentProjectileData);
            LoadedProjectile.transform.SetParent(transform, false);
            LoadedProjectile.transform.localPosition = muzzleOffset;
            LoadedProjectile.transform.localRotation = Quaternion.identity;
        }

        private void AttackProjectile(Vector3 target)
        {
            if (!LoadedProjectile) LoadProjectile();

            LoadedProjectile.transform.SetParent(null, true);

            if (mode == Mode.ScreenCenter)
            {
                var cam = Camera.main;
                if (cam)
                {
                    var ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
                    if (Physics.Raycast(ray, out var hit, 100f, LoadedProjectile.GetDamageLayerMask(), QueryTriggerInteraction.Ignore))
                    {
                        target = hit.point;
                    }
                    else
                    {
                        target = ray.origin + ray.direction * 100f;
                    }
                }
            }

            LoadedProjectile.Shot(target, this);
            LoadedProjectile = null;
        }
    }
}