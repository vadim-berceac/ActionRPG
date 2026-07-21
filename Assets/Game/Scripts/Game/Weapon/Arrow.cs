using UnityEngine;

namespace Game
{
    public class Arrow : Projectile
    {
        [SerializeField] private float projectileSpeed = 20f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private int damageAmount = 1;
        [SerializeField] private LayerMask damageMask;
        [SerializeField] private ParticleSystem hitVFX;

        private RangeWeapon _shooter;
        private Vector3 _flightDirection;
        private float _sinceFired;

        private readonly Collider[] _hitCache = new Collider[8];
        private const float HitRadius = 0.5f;

        private void OnEnable()
        {
            _sinceFired = 0.0f;
        }

        public override void Shot(Vector3 target, RangeWeapon shooter)
        {
            _shooter = shooter;
            _flightDirection = (target - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(_flightDirection);
            _sinceFired = 0.0f;
        }

        private void FixedUpdate()
        {
            _sinceFired += Time.fixedDeltaTime;

            var step = projectileSpeed * Time.fixedDeltaTime;
            transform.position += _flightDirection * step;

            if (_sinceFired > lifetime)
            {
                pool.Free(this);
                return;
            }

            var count = Physics.OverlapSphereNonAlloc(transform.position, HitRadius, _hitCache, damageMask.value);

            if (count <= 0)
            {
                return;
            }
            
            Collider closest = null;
            var closestDist = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                var c = _hitCache[i];
                if (c.isTrigger)
                {
                    continue;
                }

                var dist = Vector3.Distance(transform.position, c.transform.position);
                if (dist >= closestDist)
                {
                    continue;
                }
                closestDist = dist;
                closest = c;
            }

            if (!closest) return;

            var d = closest.GetComponent<Damageable>();

            if (d)
            {
                var message = new Damageable.DamageMessage
                {
                    amount = damageAmount,
                    damageSource = transform.position,
                    damager = this,
                    stopCamera = false,
                    throwing = true
                };

                d.ApplyDamage(message);
            }

            if (hitVFX)
            {
                var vfx = Instantiate(hitVFX, transform.position, Quaternion.identity);
                vfx.Play();
                Destroy(vfx.gameObject, vfx.main.duration + vfx.main.startLifetimeMultiplier);
            }

            pool.Free(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
#endif
    }
}