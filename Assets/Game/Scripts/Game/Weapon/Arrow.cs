using Tiny;
using UnityEngine;

namespace Game
{
    public class Arrow : Projectile
    {
        [SerializeField] private float projectileSpeed = 20f;
        [SerializeField] private float lifetime = 5f;
        
        [SerializeField] private LayerMask damageMask;
        [SerializeField] private LayerMask stuckMask;
        [SerializeField] private Vector3 stuckOffset;

        [Header("Sound")] [SerializeField] private AudioStruct hitSound;
        [SerializeField] private AudioStruct whooshSound;
        [SerializeField] private AudioSource audioSource;

        [Header("Trail")] [SerializeField] private Trail trail;

        private int _damageAmount;
        private ParticleSystem _hitVFX;
        private RangeWeapon _shooter;
        private Damageable _owner;
        private Vector3 _flightDirection;
        private float _sinceFired;
        private Transform _transform;
        private bool _stuck;

        private readonly Collider[] _hitCache = new Collider[8];
        private const float HitRadius = 0.5f;

        private void OnEnable()
        {
            _transform = transform;
            _sinceFired = 0.0f;
            _stuck = false;
            trail.enabled = false;
        }

        public override void SetData(WeaponData data)
        {
            base.SetData(data);
            _damageAmount  = data.Damage;
            _hitVFX = data.hitParticlePrefab;
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

        public override LayerMask GetDamageLayerMask()
        {
            return damageMask;
        }

        private void FixedUpdate()
        {
            _sinceFired += Time.fixedDeltaTime;

            if (_sinceFired > lifetime)
            {
                Pool.Free(this);
                return;
            }

            if (_stuck)
            {
                return;
            }

            var previousPosition = _transform.position;
            var step = projectileSpeed * Time.fixedDeltaTime;
            _transform.position = previousPosition + _flightDirection * step;

            if (TryDamage())
            {
                return;
            }

            TryStick(previousPosition, step);
        }

        private bool TryDamage()
        {
            var count = Physics.OverlapSphereNonAlloc(_transform.position, HitRadius, _hitCache, damageMask.value);

            if (count <= 0)
            {
                return false;
            }

            Damageable closestDamageable = null;
            Vector3 vfxPos = default;
            var closestDist = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                var c = _hitCache[i];
                if (c.isTrigger)
                {
                    continue;
                }

                if (!c.TryGetComponent(out Damageable damageable))
                {
                    continue;
                }

                var dist = (_transform.position - c.transform.position).magnitude;
                if (dist >= closestDist)
                {
                    continue;
                }

                closestDist = dist;
                closestDamageable = damageable;
                vfxPos = c.bounds.center;
            }

            if (!closestDamageable)
            {
                return false;
            }

            var message = new Damageable.DamageMessage
            {
                amount = _damageAmount,
                damageSource = _transform.position,
                damager = _owner,
                stopCamera = false,
                throwing = true
            };

            closestDamageable.ApplyDamage(message);
            PlayHitEffects(vfxPos, hitSound);

            trail.enabled = false;
            Pool.Free(this);

            return true;
        }

        private void TryStick(Vector3 previousPosition, float step)
        {
            if (!Physics.SphereCast(previousPosition, HitRadius, _flightDirection,
                    out var hit, step, stuckMask.value, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            _stuck = true;
            _transform.position = hit.point + _transform.TransformDirection(stuckOffset);
            trail.enabled = false;

            PlayHitEffects(hit.point, default);
        }

        private void PlayHitEffects(Vector3 position, AudioStruct sound)
        {
            if (_hitVFX)
            {
                var vfx = Instantiate(_hitVFX, position, Quaternion.identity);
                vfx.Play();
                Destroy(vfx.gameObject, vfx.main.duration + vfx.main.startLifetimeMultiplier);
            }

            if (!audioSource || !sound.AudioClip)
            {
                return;
            }
            
            audioSource.Stop();
            audioSource.PlayOneShot(sound.AudioClip, sound.Volume);
        }
    }
}