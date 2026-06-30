using UnityEngine;
using System.Collections;
using Game.Message;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Inventory))]
    public class HumanoidController : MonoBehaviour, IMessageReceiver
    {
        public bool Respawning => _respawning;

        public float maxForwardSpeed = 8f;
        public float gravity = 20f;
        public float jumpSpeed = 10f;
        public float minTurnSpeed = 400f;
        public float maxTurnSpeed = 1200f;
        public float idleTimeout = 5f;
        public bool canAttack;

        public PropBones propBones;
        public RandomAudioPlayer footstepPlayer;
        public RandomAudioPlayer hurtAudioPlayer;
        public RandomAudioPlayer landingPlayer;
        public RandomAudioPlayer emoteLandingPlayer;
        public RandomAudioPlayer emoteDeathPlayer;
        public RandomAudioPlayer emoteAttackPlayer;
        public RandomAudioPlayer emoteJumpPlayer;

        private CameraSettings _cameraSettings;
        private DiContainer _diContainer;
        private bool _isWeaponEquipped;

        private AnimatorStateCache _animCache;

        private WeaponData _primaryWeaponData;
        private WeaponData _additionalWeaponData;
        private MeleeWeapon _primaryWeaponInstance;
        private MeleeWeapon _additionalWeaponInstance;
        private bool _isGrounded = true;
        private bool _previouslyGrounded = true;
        private bool _readyToJump;
        private float _desiredForwardSpeed;
        private float _forwardSpeed;
        private float _verticalSpeed;
        private CharacterInput _input;
        private CharacterController _charCtrl;
        private Material _currentWalkingSurface;
        private Quaternion _targetRotation;
        private float _angleDiff;
        private Collider[] _overlapResult = new Collider[8];
        private bool _inAttack;
        private Damageable _damageable;
        private Renderer[] _renderers;
        private Checkpoint _currentCheckpoint;
        private bool _respawning;
        private float _idleTimer;

        private const float AirborneTurnSpeedProportion = 5.4f;
        private const float GroundedRayDistance = 1f;
        private const float JumpAbortSpeed = 10f;
        private const float MinEnemyDotCoeff = 0.2f;
        private const float InverseOneEighty = 1f / 180f;
        private const float StickingGravityProportion = 0.3f;
        private const float GroundAcceleration = 20f;
        private const float GroundDeceleration = 25f;

        private int[] m_ComboHashes;

        private bool IsMoveInput => !Mathf.Approximately(_input.MoveInput.sqrMagnitude, 0f);

        public void SetCanAttack(bool canAttack)
            => this.canAttack = canAttack;

        public int PrimaryWeaponIndex => _primaryWeaponData ? _primaryWeaponData.AnimationSetIndex : 0;

        [Inject]
        private void Construct(DiContainer container, CameraSettings cameraSettings, HealthUI healthUI)
        {
            _diContainer = container;
            _cameraSettings = cameraSettings;
            _cameraSettings.SetTarget(transform, transform.Find("HeadTarget"));
        }

        private void Awake()
        {
            _input    = GetComponent<CharacterInput>();
            _charCtrl = GetComponent<CharacterController>();
            _animCache     = new AnimatorStateCache(GetComponent<Animator>());
        }

        private void OnEnable()
        {
            _animCache.InitialiseSceneLinkedSMB(this);

            _damageable = GetComponent<Damageable>();
            _damageable.onDamageMessageReceivers.Add(this);
            _damageable.isInvulnerable = true;
            _renderers = GetComponentsInChildren<Renderer>();
        }

        private void OnDisable()
        {
            _damageable.onDamageMessageReceivers.Remove(this);

            for (var i = 0; i < _renderers.Length; ++i)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].enabled = true;
            }
        }

        private void FixedUpdate()
        {
            _animCache.OnUpdate();

            UpdateInputBlocking();

            ConnectWeaponToHands(_isWeaponEquipped, _primaryWeaponData,    _primaryWeaponInstance,    _animCache.HashAttack1);
            ConnectWeaponToHands(_isWeaponEquipped, _additionalWeaponData, _additionalWeaponInstance, _animCache.HashAttack2);

            _animCache.SetStateTime();
            ProcessAttack();
            CalculateForwardMovement();
            CalculateVerticalMovement();
            SetTargetRotation();

            if (IsOrientationUpdated() && IsMoveInput)
            {
                UpdateOrientation();
            }

            PlayAudio();
            TimeoutToIdle();

            _previouslyGrounded = _isGrounded;
        }

        private void ConnectCombo(WeaponData data)
        {
            m_ComboHashes = new int[data.ComboNames.Length];
            for (var i = 0; i < data.ComboNames.Length; i++)
            {
                m_ComboHashes[i] = Animator.StringToHash(data.ComboNames[i]);
            }
        }

        private bool CheckCombo()
        {
            if (_primaryWeaponData == null || m_ComboHashes == null) return false;

            foreach (var hash in m_ComboHashes)
            {
                if (_animCache.IsInState(hash)) return true;
            }

            return false;
        }

        private void UpdateInputBlocking()
        {
            _input.InputBlocked = _animCache.IsInputBlocked();
        }

        private void CreateWeapon(WeaponData fromData, ref WeaponData prevData, ref MeleeWeapon weaponInstance, int trigger)
        {
            SetIsWeaponEquipped(false);
            
            if (weaponInstance != null)
            {
                weaponInstance.DestroyInstance();
            }
            if (fromData == null)
            {
                prevData = null;
                return;
            }

            prevData = fromData;
            var weaponObj = prevData.GetViewInstance(transform, _diContainer);
            weaponInstance = weaponObj.GetComponent<MeleeWeapon>();
            weaponInstance.SetOwner(gameObject);
            ConnectWeaponToHands(false, prevData, weaponInstance, trigger);
            ConnectCombo(prevData);
        }

        public void CreatePrimaryWeapon(WeaponData fromData)
        {
            CreateWeapon(fromData, ref _primaryWeaponData, ref _primaryWeaponInstance, _animCache.HashAttack1);
        }

        public void CreateAdditionalWeapon(WeaponData fromData)
        {
            CreateWeapon(fromData, ref _additionalWeaponData, ref _additionalWeaponInstance, _animCache.HashAttack2);
        }

        public void SetIsWeaponEquipped(bool value)
        {
            _isWeaponEquipped = value;
            var index = value && _primaryWeaponData ? _primaryWeaponData.AnimationSetIndex : 0;
            _animCache.SetWeaponEquipped(value, index);
        }

        private void ProcessAttack()
        {
            _animCache.SetHasAdditionalWeapon(_additionalWeaponData != null);
            _animCache.ResetAttack1();
            _animCache.ResetAttack2();

            if (_input.Attack1 && canAttack) _animCache.TriggerAttack1();
            if (_input.Attack2 && canAttack) _animCache.TriggerAttack2();
        }

        private void ConnectWeaponToHands(bool equip, WeaponData data, MeleeWeapon weaponInstance, int trigger)
        {
            if (!data) return;

            var bone      = equip ? data.ActiveProp : data.UnActiveProp;
            var newParent = propBones.GetPropBone(bone.PropType).Prop;

            if (weaponInstance && newParent)
                weaponInstance.SetViewParent(newParent, bone);

            _inAttack = false;

            if (!equip)
                _animCache.ResetTrigger(trigger);
        }

        private void CalculateForwardMovement()
        {
            var moveInput = _input.MoveInput;
            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            _desiredForwardSpeed = moveInput.magnitude * maxForwardSpeed;
            var acceleration    = IsMoveInput ? GroundAcceleration : GroundDeceleration;
            _forwardSpeed        = Mathf.MoveTowards(_forwardSpeed, _desiredForwardSpeed, acceleration * Time.deltaTime);

            _animCache.SetForwardSpeed(_forwardSpeed);
        }

        private void CalculateVerticalMovement()
        {
            if (!_input.JumpInput && _isGrounded)
                _readyToJump = true;

            if (_isGrounded)
            {
                _verticalSpeed = -gravity * StickingGravityProportion;

                if (_input.JumpInput && _readyToJump && !CheckCombo())
                {
                    _verticalSpeed = jumpSpeed;
                    _isGrounded    = false;
                    _readyToJump   = false;
                }
            }
            else
            {
                if (!_input.JumpInput && _verticalSpeed > 0.0f)
                    _verticalSpeed -= JumpAbortSpeed * Time.deltaTime;

                if (Mathf.Approximately(_verticalSpeed, 0f))
                    _verticalSpeed = 0f;

                _verticalSpeed -= gravity * Time.deltaTime;
            }
        }

        private void SetTargetRotation()
        {
            var moveInput              = _input.MoveInput;
            var localMovementDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

            var forward = Quaternion.Euler(0f, _cameraSettings.Current.m_XAxis.Value, 0f) * Vector3.forward;
            forward.y = 0f;
            forward.Normalize();

            Quaternion targetRotation;

            if (Mathf.Approximately(Vector3.Dot(localMovementDirection, Vector3.forward), -1.0f))
            {
                targetRotation = Quaternion.LookRotation(-forward);
            }
            else
            {
                var cameraToInputOffset = Quaternion.FromToRotation(Vector3.forward, localMovementDirection);
                targetRotation                 = Quaternion.LookRotation(cameraToInputOffset * forward);
            }

            var resultingForward = targetRotation * Vector3.forward;

            if (_inAttack)
            {
                var centre      = transform.position + transform.forward * 2.0f + transform.up;
                var halfExtents = new Vector3(3.0f, 1.0f, 2.0f);
                var layerMask       = 1 << LayerMask.NameToLayer("Enemy");
                var count           = Physics.OverlapBoxNonAlloc(centre, halfExtents, _overlapResult, targetRotation, layerMask);

                var closestDot       = 0.0f;
                var closestForward = Vector3.zero;
                var closest            = -1;

                for (var i = 0; i < count; ++i)
                {
                    var playerToEnemy = _overlapResult[i].transform.position - transform.position;
                    playerToEnemy.y = 0;
                    playerToEnemy.Normalize();

                    var d = Vector3.Dot(resultingForward, playerToEnemy);
                    if (d > MinEnemyDotCoeff && d > closestDot)
                    {
                        closestForward = playerToEnemy;
                        closestDot     = d;
                        closest        = i;
                    }
                }

                if (closest != -1)
                {
                    resultingForward   = closestForward;
                    transform.rotation = Quaternion.LookRotation(resultingForward);
                }
            }

            var angleCurrent = Mathf.Atan2(transform.forward.x, transform.forward.z) * Mathf.Rad2Deg;
            var targetAngle  = Mathf.Atan2(resultingForward.x, resultingForward.z) * Mathf.Rad2Deg;

            _angleDiff      = Mathf.DeltaAngle(angleCurrent, targetAngle);
            _targetRotation = targetRotation;
        }

        private bool IsOrientationUpdated()
        {
            return _animCache.IsActiveOrEntering(_animCache.HashLocomotion)
                || _animCache.IsActiveOrEntering(_animCache.HashAirborne)
                || _animCache.IsActiveOrEntering(_animCache.HashLanding)
                || CheckCombo() && !_inAttack;
        }

        private void UpdateOrientation()
        {
            _animCache.SetAngleDeltaRad(_angleDiff * Mathf.Deg2Rad);

            var localInput      = new Vector3(_input.MoveInput.x, 0f, _input.MoveInput.y);
            var groundedTurnSpeed = Mathf.Lerp(maxTurnSpeed, minTurnSpeed, _forwardSpeed / _desiredForwardSpeed);
            var actualTurnSpeed   = _isGrounded
                ? groundedTurnSpeed
                : Vector3.Angle(transform.forward, localInput) * InverseOneEighty * AirborneTurnSpeedProportion * groundedTurnSpeed;

            _targetRotation   = Quaternion.RotateTowards(transform.rotation, _targetRotation, actualTurnSpeed * Time.deltaTime);
            transform.rotation = _targetRotation;
        }

        private void PlayAudio()
        {
            var footfall = _animCache.FootFall;

            if (footfall > 0.01f && !footstepPlayer.playing && footstepPlayer.canPlay)
            {
                footstepPlayer.playing = true;
                footstepPlayer.canPlay = false;
                footstepPlayer.PlayRandomClip(_currentWalkingSurface, _forwardSpeed < 4 ? 0 : 1);
            }
            else if (footstepPlayer.playing)
            {
                footstepPlayer.playing = false;
            }
            else if (footfall < 0.01f && !footstepPlayer.canPlay)
            {
                footstepPlayer.canPlay = true;
            }

            if (_isGrounded && !_previouslyGrounded)
            {
                landingPlayer.PlayRandomClip(_currentWalkingSurface, bankId: _forwardSpeed < 4 ? 0 : 1);
                emoteLandingPlayer.PlayRandomClip();
            }

            if (!_isGrounded && _previouslyGrounded && _verticalSpeed > 0f)
                emoteJumpPlayer.PlayRandomClip();

            if (_animCache.JustEntered(_animCache.HashHurt))
                hurtAudioPlayer.PlayRandomClip();

            if (_animCache.JustEntered(_animCache.HashDeath))
                emoteDeathPlayer.PlayRandomClip();

            if (m_ComboHashes == null || m_ComboHashes.Length < 1) return;

            foreach (var hash in m_ComboHashes)
            {
                if (_animCache.JustEntered(hash))
                {
                    emoteAttackPlayer.PlayRandomClip();
                    break;
                }
            }
        }

        private void TimeoutToIdle()
        {
            var inputDetected = IsMoveInput || _input.Attack1 || _input.Attack2 || _input.JumpInput;

            if (_isGrounded && !inputDetected)
            {
                _idleTimer += Time.deltaTime;
                if (_idleTimer >= idleTimeout)
                {
                    _idleTimer = 0f;
                    _animCache.TriggerTimeoutToIdle();
                }
            }
            else
            {
                _idleTimer = 0f;
                _animCache.ResetTimeoutToIdle();
            }

            _animCache.SetInputDetected(inputDetected);
        }

        private void OnAnimatorMove()
        {
            Vector3 movement;

            if (_isGrounded)
            {
                RaycastHit hit;
                var ray = new Ray(transform.position + Vector3.up * GroundedRayDistance * 0.5f, -Vector3.up);
                if (Physics.Raycast(ray, out hit, GroundedRayDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    movement = Vector3.ProjectOnPlane(_animCache.DeltaPosition, hit.normal);
                    var groundRenderer = hit.collider.GetComponentInChildren<Renderer>();
                    _currentWalkingSurface = groundRenderer ? groundRenderer.sharedMaterial : null;
                }
                else
                {
                    movement                = _animCache.DeltaPosition;
                    _currentWalkingSurface = null;
                }
            }
            else
            {
                movement = _forwardSpeed * transform.forward * Time.deltaTime;
            }

            _charCtrl.transform.rotation *= _animCache.DeltaRotation;
            movement += _verticalSpeed * Vector3.up * Time.deltaTime;
            _charCtrl.Move(movement);

            _isGrounded = _charCtrl.isGrounded;

            if (!_isGrounded)
                _animCache.SetAirborneVerticalSpeed(_verticalSpeed);

            _animCache.SetGrounded(_isGrounded);
        }

        public void MeleeAttackStart(int throwing = 0)
        {
            if (_primaryWeaponInstance == null) return;
            _primaryWeaponInstance.BeginAttack(throwing != 0);
            _inAttack = true;
        }

        public void MeleeAttackEnd()
        {
            if (_primaryWeaponInstance == null) return;
            _primaryWeaponInstance.EndAttack();
            _inAttack = false;
        }

        public void AdditionalAttackStart(int throwing = 0)
        {
            if (_additionalWeaponInstance == null) return;
            _additionalWeaponInstance.BeginAttack(throwing != 0);
            _inAttack = true;
        }

        public void AdditionalAttackEnd()
        {
            if (_additionalWeaponInstance == null) return;
            _additionalWeaponInstance.EndAttack();
            _inAttack = false;
        }

        public void SetCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint != null)
                _currentCheckpoint = checkpoint;
        }

        public void Respawn()
            => StartCoroutine(RespawnRoutine());

        private IEnumerator RespawnRoutine()
        {
            while (!_animCache.IsTransitioningInto(_animCache.HashDeath)
                && !_animCache.IsInState(_animCache.HashDeath))
            {
                yield return null;
            }

            yield return StartCoroutine(ScreenFader.FadeSceneOut());
            while (ScreenFader.IsFading)
                yield return null;

            var spawn = GetComponentInChildren<EllenSpawn>();
            spawn.enabled = true;

            if (_currentCheckpoint != null)
            {
                transform.position = _currentCheckpoint.transform.position;
                transform.rotation = _currentCheckpoint.transform.rotation;
            }
            else
            {
                Debug.LogError("There is no Checkpoint set. Did you add a checkpoint at the spawn?");
            }

            _animCache.TriggerRespawn();
            spawn.StartEffect();
            yield return StartCoroutine(ScreenFader.FadeSceneIn());

            _damageable.ResetDamage();
        }

        public void RespawnFinished()
        {
            _respawning                = false;
            _damageable.isInvulnerable = false;
        }

        public void OnReceiveMessage(MessageType type, object sender, object data)
        {
            switch (type)
            {
                case MessageType.DAMAGED: Damaged((Damageable.DamageMessage)data); break;
                case MessageType.DEAD:    Die((Damageable.DamageMessage)data);     break;
            }
        }

        private void Damaged(Damageable.DamageMessage damageMessage)
        {
            _animCache.TriggerHurt();

            var forward   = damageMessage.damageSource - transform.position;
            forward.y         = 0f;
            var localHurt = transform.InverseTransformDirection(forward);
            _animCache.SetHurtDirection(localHurt.x, localHurt.z);

            CameraShake.Shake(CameraShake.k_PlayerHitShakeAmount, CameraShake.k_PlayerHitShakeTime);

            if (hurtAudioPlayer != null)
                hurtAudioPlayer.PlayRandomClip();
        }

        public void Die(Damageable.DamageMessage damageMessage)
        {
            _animCache.TriggerDeath();
            _forwardSpeed              = 0f;
            _verticalSpeed             = 0f;
            _respawning                = true;
            _damageable.isInvulnerable = true;
        }
    }
}