using Tiny;
using UnityEngine;

namespace Game
{
    public class Arrow : Projectile
    {
        [SerializeField] private float projectileSpeed = 20f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private int damageAmount = 1;
        [SerializeField] private LayerMask damageMask;
        [SerializeField] private LayerMask stuckMask;
        [SerializeField] private ParticleSystem hitVFX;
        
        [Header("Sound")]
        [SerializeField] private AudioStruct hitSound;
        [SerializeField] private AudioStruct whooshSound;
        [SerializeField] private AudioSource audioSource;

        [Header("Trail")] 
        [SerializeField] private Trail trail;

        private RangeWeapon _shooter;
        private Damageable _owner;
        private Vector3 _flightDirection;
        private float _sinceFired;
        private Transform _transform;

        private readonly Collider[] _hitCache = new Collider[8];
        private const float HitRadius = 0.5f;

        private void OnEnable()
        {
            _transform = transform;
            _sinceFired = 0.0f;
            trail.enabled = false;
        }

        public override void Shot(Vector3 target, RangeWeapon shooter)
        {
            _shooter = shooter;
            _owner = shooter.Owner;
            _flightDirection = (target - _transform.position).normalized;
            _transform.rotation = Quaternion.LookRotation(_flightDirection);
            _sinceFired = 0.0f;

            if (audioSource && whooshSound.AudioClip)
            {
                audioSource.PlayOneShot(whooshSound.AudioClip, whooshSound.Volume);
            }
            
            trail.enabled = true;
        }

        private void FixedUpdate()
        {
            _sinceFired += Time.fixedDeltaTime;

            var step = projectileSpeed * Time.fixedDeltaTime;
            _transform.position += _flightDirection * step;

            if (_sinceFired > lifetime)
            {
                pool.Free(this);
                return;
            }

            var count = Physics.OverlapSphereNonAlloc(_transform.position, HitRadius, _hitCache, damageMask.value);

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

                var dist = Vector3.Distance(_transform.position, c.transform.position);
                if (dist >= closestDist)
                {
                    continue;
                }
                closestDist = dist;
                closest = c;
            }

            if (!closest) return;

            var damageable = closest.GetComponent<Damageable>();

            if (damageable)
            {
                var message = new Damageable.DamageMessage
                {
                    amount = damageAmount,
                    damageSource = _transform.position,
                    damager = _owner,
                    stopCamera = false,
                    throwing = true
                };

                damageable.ApplyDamage(message);
            }

            if (hitVFX)
            {
                var vfx = Instantiate(hitVFX, _transform.position, Quaternion.identity);
                vfx.Play();
                Destroy(vfx.gameObject, vfx.main.duration + vfx.main.startLifetimeMultiplier);
            }

            if (audioSource && hitSound.AudioClip)
            {
                audioSource.Stop();
                audioSource.PlayOneShot(hitSound.AudioClip, hitSound.Volume);
            }
            
            trail.enabled = false;

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