using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public class AnimatorStateCache
    {
        private readonly Animator _animator;
        private readonly RangeWeapon _rangeWeapon;

        public AnimatorStateCache(Animator animator, RangeWeapon  range)
        {
            _animator = animator;
            _rangeWeapon = range;
        }

        public int HashLocomotion  { get; } = Animator.StringToHash("Locomotion");
        public int HashAirborne    { get; } = Animator.StringToHash("Airborne");
        public int HashLanding     { get; } = Animator.StringToHash("Landing");
        public int HashDeath       { get; } = Animator.StringToHash("Death");
        public int HashHurt        { get; } = Animator.StringToHash("Hurt");
        public int HashAttack1     { get; } = Animator.StringToHash("Attack1");
        public int HashAttack2     { get; } = Animator.StringToHash("Attack2");
        public int Shoot           { get; } = Animator.StringToHash("Shoot");
        public int Block           { get; } = Animator.StringToHash("Block");

        private readonly int _hashAirborneVerticalSpeed = Animator.StringToHash("AirborneVerticalSpeed");
        private readonly int _hashForwardSpeed          = Animator.StringToHash("ForwardSpeed");
        private readonly int _hashAngleDeltaRad         = Animator.StringToHash("AngleDeltaRad");
        private readonly int _hashTimeoutToIdle         = Animator.StringToHash("TimeoutToIdle");
        private readonly int _hashGrounded              = Animator.StringToHash("Grounded");
        private readonly int _hashInputDetected         = Animator.StringToHash("InputDetected");
        private readonly int _hashHurtFromX             = Animator.StringToHash("HurtFromX");
        private readonly int _hashHurtFromY             = Animator.StringToHash("HurtFromY");
        private readonly int _hashStateTime             = Animator.StringToHash("StateTime");
        private readonly int _hashFootFall              = Animator.StringToHash("FootFall");
        private readonly int _hashLoadProgressCurve     = Animator.StringToHash("LoadProgressCurve");
        private readonly int _hashWeaponEquipped        = Animator.StringToHash("WeaponEquipped");
        private readonly int _hashWeaponIndex           = Animator.StringToHash("WeaponIndex");
        private readonly int _hashHasAdditionalWeapon   = Animator.StringToHash("HasAdditionalWeapon");
        private readonly int _hashRespawn               = Animator.StringToHash("Respawn");
        private readonly int _blockHitTrigger          = Animator.StringToHash("BlockHitTrigger");
        private readonly int _hashVerticalAimAngle     = Animator.StringToHash("VerticalAimAngle");
        private readonly int _hashInteract             = Animator.StringToHash("Interact");
        
        private const string BlockInput = "BlockInput";

        private AnimatorStateInfo _current;
        private AnimatorStateInfo _next;
        private AnimatorStateInfo _previousCurrent;
        private AnimatorStateInfo _previousNext;
        private bool _isTransitioning;
        private bool _wasTransitioning;
        private bool _hasAdditionalWeapon;

        public float FootFall { get; private set; }
        public float LoadProgressCurve { get; private set; }

        public void OnUpdate(int layer = 0)
        {
            _previousCurrent  = _current;
            _previousNext     = _next;
            _wasTransitioning = _isTransitioning;

            _current         = _animator.GetCurrentAnimatorStateInfo(layer);
            _next            = _animator.GetNextAnimatorStateInfo(layer);
            _isTransitioning = _animator.IsInTransition(layer);

            FootFall = _animator.GetFloat(_hashFootFall);
            LoadProgressCurve = _animator.GetFloat(_hashLoadProgressCurve);
            _animator.SetFloat(_hashVerticalAimAngle, _rangeWeapon.VerticalAimAngle);
        }

        public bool IsInState(int hash)            => _current.shortNameHash == hash || _next.shortNameHash == hash;

        public bool IsInAnyState(params int[] hashes)
        {
            foreach (var h in hashes)
            {
                if (IsInState(h)) return true;
            }
            return false;
        }

        public bool JustEntered(int hash) => _current.shortNameHash == hash && _previousCurrent.shortNameHash != hash;

        public bool WasIn(int hash) => _previousCurrent.shortNameHash == hash;

        public bool IsTransitioningInto(int hash) => _isTransitioning && _next.shortNameHash == hash;

        public bool IsTagged(int tagHash) => _current.tagHash == tagHash;

        public bool IsActiveOrEntering(int hash)
            => (!_isTransitioning && _current.shortNameHash == hash)
            || _next.shortNameHash == hash;

        public bool IsInputBlocked()
        {
            var tag = Animator.StringToHash(BlockInput);
            return (_current.tagHash == tag && !_isTransitioning)
                || _next.tagHash == tag;
        }

        public void SetForwardSpeed(float value) => _animator.SetFloat(_hashForwardSpeed, value);

        public void SetAirborneVerticalSpeed(float value) => _animator.SetFloat(_hashAirborneVerticalSpeed, value);

        public void SetAngleDeltaRad(float value) => _animator.SetFloat(_hashAngleDeltaRad, value);

        public void SetStateTime() => _animator.SetFloat(_hashStateTime,
                Mathf.Repeat(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f));

        public void SetGrounded(bool value) => _animator.SetBool(_hashGrounded, value);
        public void SetBlock(bool value) => _animator.SetBool(Block, value);
        public void SetShoot(bool value) => _animator.SetBool(Shoot, value);

        public void SetInputDetected(bool value) => _animator.SetBool(_hashInputDetected, value);

        public void SetHasAdditionalWeapon(bool value)
        { 
            _hasAdditionalWeapon = value;
            _animator.SetBool(_hashHasAdditionalWeapon, _hasAdditionalWeapon);
        }

        public void SetWeaponEquipped(bool equipped, float weaponIndex)
        {
            _animator.SetBool(_hashWeaponEquipped, equipped);
            _animator.SetFloat(_hashWeaponIndex, weaponIndex);
        }

        public void SetHurtDirection(float x, float y)
        {
            _animator.SetFloat(_hashHurtFromX, x);
            _animator.SetFloat(_hashHurtFromY, y);
        }

        public void TriggerHurt()      => _animator.SetTrigger(HashHurt);
        public void TriggerDeath(bool value)  => _animator.SetBool(HashDeath, value);
        public void TriggerRespawn()   => _animator.SetTrigger(_hashRespawn);
        public void TriggerBlock() => _animator.SetTrigger(_blockHitTrigger);

        public void TriggerAttack1()   => _animator.SetTrigger(HashAttack1);
        public void TriggerAttack2()
        {
            if (_hasAdditionalWeapon)
            {
                _animator.SetTrigger(HashAttack2);
            }
        }
        public void SetInteract(bool value)   => _animator.SetBool(_hashInteract, value);
        public void ResetAttack1()     => _animator.ResetTrigger(HashAttack1);
        public void ResetAttack2()     => _animator.ResetTrigger(HashAttack2);
        public void ResetTrigger(int hash) => _animator.ResetTrigger(hash);

        public void TriggerTimeoutToIdle() => _animator.SetTrigger(_hashTimeoutToIdle);
        public void ResetTimeoutToIdle()   => _animator.ResetTrigger(_hashTimeoutToIdle);

        public void InitialiseSceneLinkedSMB<T>(T owner) where T : MonoBehaviour
            => SceneLinkedSMB<T>.Initialise(_animator, owner);

        public Vector3 DeltaPosition => _animator.deltaPosition;

        public Quaternion DeltaRotation => _animator.deltaRotation;
        
        public async UniTask SetAnimationSpeedCurve(float duration, AnimationCurve curve, CancellationToken ct = default)
        {
            if (duration <= 0f)
            {
                _animator.speed = (curve.Evaluate(1f));
                return;
            }

            var time = 0f;
            var progress = 0f;

            while (progress < 1f)
            {
                progress = Mathf.Clamp01(time / duration);
                var speed = curve.Evaluate(progress);
                _animator.speed = speed;
                time += Time.deltaTime;
                await UniTask.Yield(ct);
            }

            _animator.speed = curve.Evaluate(1f);
        }
    }
}