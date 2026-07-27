using System.Threading;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private float aimRayDistance = 100f;

        private WeaponData _currentProjectileData;
        private ObjectPooler<Projectile> _projectilePool;
        private CancellationTokenSource _aimUpdateCts;
        private Vector3 _lastTarget;

        public Damageable Owner { get; private set; }
        public Projectile LoadedProjectile { get; private set; }
        public Transform AimTarget { get; private set; }
        public float VerticalAimAngle { get; private set; }

        private void Awake()
        {
            var aimTargetObject = new GameObject($"{name}_AimTarget");
            AimTarget = aimTargetObject.transform;
            AimTarget.SetParent(transform, false);
            AimTarget.localPosition = Vector3.up + Vector3.forward * 10f;
            _lastTarget = AimTarget.position;

            UpdateVerticalAimAngle();
        }

        private void OnEnable()
        {
            _aimUpdateCts = new CancellationTokenSource();
            UpdateAimTargetLoop(_aimUpdateCts.Token).Forget();
        }

        private void OnDisable()
        {
            _aimUpdateCts?.Cancel();
            _aimUpdateCts?.Dispose();
            _aimUpdateCts = null;

            _projectilePool?.ClearAll();
            _projectilePool = null;
        }

        private void OnDestroy()
        {
            if (AimTarget != null)
                Destroy(AimTarget.gameObject);
        }

        public void SetData(WeaponData data)
        {
            _currentProjectileData = null;
            _projectilePool?.ClearAll();
            _projectilePool = null;
            
            _currentProjectileData = data;
            _projectilePool = new ObjectPooler<Projectile>(projectile.Count, projectile);
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

            target = ResolveTarget(target);
            _lastTarget = target;

            LoadedProjectile.Shot(target, this);
            LoadedProjectile = null;
        }

        private Vector3 ResolveTarget(Vector3 fallbackTarget)
        {
            if (mode != Mode.ScreenCenter)
                return fallbackTarget;

            var cam = Camera.main;
            if (!cam)
                return fallbackTarget;

            var layerMask = LoadedProjectile != null
                ? LoadedProjectile.GetDamageLayerMask()
                : projectile.GetDamageLayerMask();

            var ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            if (Physics.Raycast(ray, out var hit, aimRayDistance, layerMask, QueryTriggerInteraction.Ignore))
                return hit.point;

            return ray.origin + ray.direction * aimRayDistance;
        }

        private async UniTaskVoid UpdateAimTargetLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                AimTarget.position = ResolveTarget(_lastTarget);
                UpdateVerticalAimAngle();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        
        private void UpdateVerticalAimAngle()
        {
            var direction = AimTarget.position - transform.position;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            var localDir = transform.InverseTransformDirection(direction.normalized);
            VerticalAimAngle = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;
        }
    }
}