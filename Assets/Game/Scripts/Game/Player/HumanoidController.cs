using UnityEngine;
using System.Collections;
using Game.Message;
using Unity.Cinemachine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Inventory))]
    public class HumanoidController : MonoBehaviour, IMessageReceiver
    {
        [field: SerializeField] public bool IsPlayer { get; private set; }
        [field: SerializeField] public Transform ModelTransform { get; private set; }
        [field: SerializeField] public LayerMask TargetLayer { get; private set; }
        [field: SerializeField] public RangeWeapon RangeWeaponRoot { get; private set; }
        public bool Respawning => _respawning;

        public float maxForwardSpeed = 8f;
        public float gravity = 20f;
        public float jumpSpeed = 10f;
        public float minTurnSpeed = 400f;
        public float maxTurnSpeed = 1200f;
        public float idleTimeout = 5f;
        public bool canAttack;

        public bool IsGrounded => _charCtrl != null && _charCtrl.isGrounded;
        public bool HasAdditionalWeapon => _additionalWeaponInstance != null;

        public PropBones propBones;
        public RandomAudioPlayer footstepPlayer;
        public RandomAudioPlayer hurtAudioPlayer;
        public RandomAudioPlayer landingPlayer;
        public RandomAudioPlayer emoteLandingPlayer;
        public RandomAudioPlayer emoteDeathPlayer;
        public RandomAudioPlayer emoteAttackPlayer;
        public RandomAudioPlayer emoteJumpPlayer;
        public AudioSource blockAudioSource;

        private CameraSettings _cameraSettings;
        private DiContainer _diContainer;
        private bool _isMeleeWeaponEquipped;
        private bool _isRangeWeaponEquipped;

        private AnimatorStateCache _animCache;

        private WeaponData _primaryWeaponData;
        private WeaponData _additionalWeaponData;
        private WeaponData _rangedWeaponData;
        private WeaponData _ammunitionWeaponData;
        private WeaponInstance _primaryWeaponInstance;
        private WeaponInstance _additionalWeaponInstance;
        private WeaponInstance _rangedWeaponInstance;
        private WeaponInstance _ammunitionWeaponInstance;
        private bool _isGrounded = true;
        private bool _previouslyGrounded = true;
        private bool _readyToJump;
        private float _desiredForwardSpeed;
        private float _forwardSpeed;
        private float _verticalSpeed;
        
        private RangedAttackHandler _rangedAttackHandler;
        private bool _shootPressed;
        private bool _bowCameraOn;
        private Quaternion _modelOriginalLocalRotation;

        private IInput _input;
        private CharacterController _charCtrl;
        private Material _currentWalkingSurface;
        private Quaternion _targetRotation;
        private float _angleDiff;
        private bool _inAttack;
        private bool _isBlocking;
        private bool _isShoot;
        private bool _blockTriggeredThisFixedUpdate;
        private bool _damageTriggeredThisFixedUpdate;
        private Damageable _damageable;
        private Renderer[] _renderers;
        private Checkpoint _currentCheckpoint;
        private bool _respawning;
        private float _idleTimer;
        private Vector3 _knockbackVelocity;
        private float _knockbackDeceleration = 15f;
        private GameObject _projectileView;

        private const float AirborneTurnSpeedProportion = 5.4f;
        private const float GroundedRayDistance = 1f;
        private const float JumpAbortSpeed = 10f;
        private const float InverseOneEighty = 1f / 180f;
        private const float StickingGravityProportion = 0.3f;
        private const float GroundAcceleration = 20f;
        private const float GroundDeceleration = 25f;

        private int[] m_ComboHashes;

        private bool IsMoveInput => !Mathf.Approximately(_input.MoveInput.sqrMagnitude, 0f);

        public void SetCanAttack(bool canAttack)
            => this.canAttack = canAttack;

        public int PrimaryWeaponIndex => _primaryWeaponData ? _primaryWeaponData.AnimationSetIndex : 0;
        public int RangeWeaponIndex => _rangedWeaponData ? _rangedWeaponData.AnimationSetIndex : 0;
        public WeaponData PrimaryWeaponData => _primaryWeaponData;
        public WeaponData AdditionalWeaponData => _additionalWeaponData;
        public WeaponData RangedWeaponData => _rangedWeaponData;
        public float LoadProgressCurve => _animCache.LoadProgressCurve;

        [Inject]
        private void Construct(DiContainer container, CameraSettings cameraSettings, HealthUI healthUI, PlayerInputHandlerService playerInputHandlerService)
        {
            _diContainer = container;
            _cameraSettings = cameraSettings;
            
            if(IsPlayer)
            {
                _input = playerInputHandlerService;
                _cameraSettings.SetTarget(transform, transform.Find("HeadTarget"));
            }
            else
            {
                _input = GetComponent<IInput>();
            }
        }

        private void Awake()
        {
            _charCtrl = GetComponent<CharacterController>();
            _animCache     = new AnimatorStateCache(GetComponent<Animator>());

            if (ModelTransform != null)
            {
                _modelOriginalLocalRotation = ModelTransform.localRotation;
            }
        }

        private void OnEnable()
        {
            _animCache.InitialiseSceneLinkedSMB(this);

            _damageable = GetComponent<Damageable>();
            _damageable.onDamageMessageReceivers.Add(this);
            _damageable.isInvulnerable = true;
            _damageable.onDamageBlocked = OnDamageBlocked;
            _renderers = GetComponentsInChildren<Renderer>();

            if (RangeWeaponRoot != null)
            {
                _rangedAttackHandler = new RangedAttackHandler(RangeWeaponRoot, _damageable, TargetLayer);
            }
        }

        private void OnDisable()
        {
            _damageable.onDamageMessageReceivers.Remove(this);
            _damageable.onDamageBlocked = null;

            for (var i = 0; i < _renderers.Length; ++i)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].enabled = true;
            }
        }

        private void FixedUpdate()
        {
            _animCache.OnUpdate();

            _blockTriggeredThisFixedUpdate = false;
            _damageTriggeredThisFixedUpdate = false;

            UpdateInputBlocking();

            ConnectWeaponToHands(_isMeleeWeaponEquipped, _primaryWeaponData,    _primaryWeaponInstance,    _animCache.HashAttack1);
            ConnectWeaponToHands(_isMeleeWeaponEquipped, _additionalWeaponData, _additionalWeaponInstance, _animCache.HashAttack2);
            ConnectWeaponToHands(_isRangeWeaponEquipped, _rangedWeaponData, _rangedWeaponInstance, _animCache.Shoot);

            _animCache.SetStateTime();
            ProcessAttack();
            UpdateBlocking();
            UpdateShoot();
            CalculateForwardMovement();
            CalculateVerticalMovement();
            SetTargetRotation();

            if (IsOrientationUpdated() && (IsMoveInput || _isBlocking || _shootPressed || _inAttack))
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

        private void CreateWeapon(WeaponData fromData, ref WeaponData prevData, ref WeaponInstance weaponInstance, int trigger)
        {
            SetIsMeleeWeaponEquipped(false);
            
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
            weaponInstance = weaponObj.GetComponent<WeaponInstance>();
            weaponInstance.Initialize(gameObject, TargetLayer);
            weaponInstance.SetWeaponData(prevData);
            weaponInstance.SetKnockbackForce(prevData.knockbackForce);
            weaponInstance.SetStaticParts(prevData.GetStaticParts(propBones, _diContainer));
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

        public void CreateRangedWeapon(WeaponData fromData)
        {
            CreateWeapon(fromData, ref _rangedWeaponData, ref _rangedWeaponInstance, _animCache.Shoot);
        }
        
        public void CreateAmmunition(WeaponData fromData)
        {
            RangeWeaponRoot.SetData(fromData);
            CreateWeapon(fromData, ref _ammunitionWeaponData, ref _ammunitionWeaponInstance, _animCache.Shoot);
        }

        public void SetIsMeleeWeaponEquipped(bool value)
        {
            _isMeleeWeaponEquipped = value;
            var index = value && _primaryWeaponData ? _primaryWeaponData.AnimationSetIndex : 0;
            _animCache.SetWeaponEquipped(value, index);
            
            if(value) _isRangeWeaponEquipped = false;
        }

        public void SetRangeWeaponEquipped(bool value)
        {
            _isRangeWeaponEquipped = value;
            var index = value && _rangedWeaponData ? _rangedWeaponData.AnimationSetIndex : 0;
            _animCache.SetWeaponEquipped(value, index);
            
            if(value) _isMeleeWeaponEquipped = false;
        }

        private void ProcessAttack()
        {
            _animCache.SetHasAdditionalWeapon(_additionalWeaponData != null);
            _animCache.ResetAttack1();
            _animCache.ResetAttack2();

            if (!_isBlocking)
            {
                if (_input.Attack1 && canAttack) _animCache.TriggerAttack1();
                if (_input.Attack2 && canAttack) _animCache.TriggerAttack2();
            }
        }

        private void ConnectWeaponToHands(bool equip, WeaponData data, WeaponInstance weaponInstanceInstance, int trigger)
        {
            if (!data) return;

            var settings = equip ? data.ActiveProp : data.UnActiveProp;

            if (weaponInstanceInstance)
            {
                weaponInstanceInstance.SetViewParent(propBones, settings);
            }

            if (!equip)
                _animCache.ResetTrigger(trigger);
        }

        private void CalculateForwardMovement()
        {
            // Для AI движение при стрельбе разрешено (ShootState управляет позиционированием),
            // для игрока — заблокировано (прицеливание требует неподвижности)
            var moveInput = _isBlocking || (_shootPressed && IsPlayer) || _inAttack ? Vector2.zero : _input.MoveInput;
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

                if (_input.JumpInput && _readyToJump && !_inAttack && !_isBlocking)
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
            var cameraForward = Quaternion.Euler(0f, _input.RotationYaw, 0f) * Vector3.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            if (_inAttack || _isBlocking || _shootPressed)
            {
                _targetRotation = Quaternion.LookRotation(cameraForward);
                _angleDiff = Mathf.DeltaAngle(
                    Mathf.Atan2(transform.forward.x, transform.forward.z) * Mathf.Rad2Deg,
                    Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg
                );
                return;
            }

            var moveInput = _input.MoveInput;
            var localMovementDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

            Quaternion targetRotation;

            if (Mathf.Approximately(Vector3.Dot(localMovementDirection, Vector3.forward), -1.0f))
            {
                targetRotation = Quaternion.LookRotation(-cameraForward);
            }
            else
            {
                var cameraToInputOffset = Quaternion.FromToRotation(Vector3.forward, localMovementDirection);
                targetRotation = Quaternion.LookRotation(cameraToInputOffset * cameraForward);
            }

            var resultingForward = targetRotation * Vector3.forward;

            var angleCurrent = Mathf.Atan2(transform.forward.x, transform.forward.z) * Mathf.Rad2Deg;
            var targetAngle  = Mathf.Atan2(resultingForward.x, resultingForward.z) * Mathf.Rad2Deg;

            _angleDiff = Mathf.DeltaAngle(angleCurrent, targetAngle);
            _targetRotation = targetRotation;
        }

        private bool IsOrientationUpdated()
        {
            return _animCache.IsActiveOrEntering(_animCache.HashLocomotion)
                || _animCache.IsActiveOrEntering(_animCache.HashAirborne)
                || _animCache.IsActiveOrEntering(_animCache.HashLanding)
                || _inAttack
                || _isBlocking
                || _shootPressed;
        }

        private void UpdateOrientation()
        {
            _animCache.SetAngleDeltaRad(_angleDiff * Mathf.Deg2Rad);
            
            if (_isBlocking || _inAttack || _shootPressed)
            {
                transform.rotation = _targetRotation;
                return;
            }

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
                landingPlayer.PlayRandomClipOneShot(_currentWalkingSurface, bankId: _forwardSpeed < 4 ? 0 : 1);
                emoteLandingPlayer.PlayRandomClipOneShot();
            }

            if (!_isGrounded && _previouslyGrounded && _verticalSpeed > 0f)
                emoteJumpPlayer.PlayRandomClipOneShot();

            if (_animCache.JustEntered(_animCache.HashHurt))
                hurtAudioPlayer.PlayRandomClipOneShot();

            if (_animCache.JustEntered(_animCache.HashDeath))
                emoteDeathPlayer.PlayRandomClip();

            if (m_ComboHashes == null || m_ComboHashes.Length < 1) return;

            foreach (var hash in m_ComboHashes)
            {
                if (_animCache.JustEntered(hash))
                {
                    emoteAttackPlayer.PlayRandomClipOneShot();
                    break;
                }
            }
        }

        private void UpdateBlocking()
        {
            if (!_primaryWeaponInstance && !_additionalWeaponInstance)
            {
                _isBlocking = false;
                return;
            }
            _isBlocking = _input.Block;
            _animCache.SetBlock(_input.Block);
        }

        private void UpdateShoot()
        {
            if (!_rangedWeaponInstance || !_ammunitionWeaponInstance)
            {
                _shootPressed = false;
                _bowCameraOn = false;
                return;
            }
            _shootPressed = _input.Shoot;
            _isShoot = _shootPressed;
            _animCache.SetShoot(_shootPressed);
            
            if (!IsPlayer) return;
            
            if (_shootPressed && !_bowCameraOn)
            {
                _bowCameraOn = true;
                _cameraSettings.SwitchCamera(CameraSettings.CameraType.Bow);
            }
            else if (!_shootPressed && _bowCameraOn)
            {
                _bowCameraOn = false;
                _cameraSettings.SwitchCamera(CameraSettings.CameraType.Exploration);
            }
               
            if (!ModelTransform) return;
            
            var targetYaw = _bowCameraOn ? 30f : 0f;
            var currentYaw = ModelTransform.localEulerAngles.y;
            if (currentYaw > 180f) currentYaw -= 360f;
            var newYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * 10f);
            ModelTransform.localRotation = Quaternion.Euler(
                _modelOriginalLocalRotation.eulerAngles.x,
                newYaw,
                _modelOriginalLocalRotation.eulerAngles.z
            );
        }

        public void CreateProjectile()
        {
            if(_projectileView) return;
            
            _projectileView = Instantiate(_ammunitionWeaponData.ViewPrefab);
            _ammunitionWeaponData.ActiveProp.SetPropBone(_projectileView.transform, propBones);
        }

        public void DestroyProjectile()
        {
            if (!_projectileView) return;
            
            Destroy(_projectileView);
            _projectileView = null;
        }

        public void Shoot()
        {
            if (_rangedAttackHandler == null || !_rangedAttackHandler.IsValid)
                return;

            var targetPosition = transform.position + transform.forward * 20f;
            _rangedAttackHandler.Shoot(targetPosition);
        }

        public bool IsBlocking => _isBlocking;

        private bool IsFacingDamageSource(Vector3 damageSource)
        {
            var toSource = (damageSource - transform.position).normalized;
            toSource.y = 0f;
            return Vector3.Dot(transform.forward, toSource) > 0f;
        }

        private void PlayBlockSound()
        {
            AudioClip clip = null;

            if (_additionalWeaponData)
                clip = _additionalWeaponData.blockSound;

            if (!clip && _primaryWeaponData)
                clip = _primaryWeaponData.blockSound;

            if (!clip) return;

            blockAudioSource.clip = clip;
            blockAudioSource.Play();
        }

        private void TimeoutToIdle()
        {
            var inputDetected = IsMoveInput || _isBlocking || _shootPressed || _inAttack || _input.Attack1 || _input.Attack2 || _input.JumpInput;

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
                    movement = _animCache.DeltaPosition;
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

            if (_knockbackVelocity.sqrMagnitude > 0.01f)
            {
                var knockbackMovement = _knockbackVelocity * Time.deltaTime;
                _charCtrl.Move(knockbackMovement);
                _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, _knockbackDeceleration * Time.deltaTime);
            }
            else
            {
                _knockbackVelocity = Vector3.zero;
            }

            _isGrounded = _charCtrl.isGrounded;

            if (!_isGrounded)
                _animCache.SetAirborneVerticalSpeed(_verticalSpeed);

            _animCache.SetGrounded(_isGrounded);
        }

        /// <summary>
        /// Прямой вызов TriggerAttack1 для выстрела из лука (используется AI в ShootState).
        /// Обходит canAttack и ResetAttack1, чтобы гарантированно инициировать анимацию выстрела.
        /// Вызывается через UniTask.Delay, чтобы не блокировать основной поток.
        /// </summary>
        public void TriggerRangedAttack()
        {
            // ProcessAttack каждый FixedUpdate делает ResetAttack1(),
            // поэтому прямой SetTrigger может быть сброшен.
            // Решение: временно включить canAttack и установить Input.Attack1 = true.
            // ProcessAttack подхватит Attack1 на следующем FixedUpdate и вызовет TriggerAttack1.
            canAttack = true;
            _input.Attack1 = true;
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
            _respawning = false;
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

        private bool OnDamageBlocked(Damageable.DamageMessage damageMessage)
        {
            if (!_isBlocking || !IsFacingDamageSource(damageMessage.damageSource))
                return false;

            if (_blockTriggeredThisFixedUpdate)
                return true; 

            _blockTriggeredThisFixedUpdate = true;
            PlayBlockSound();
            _animCache.TriggerBlock();

            if (damageMessage.knockbackForce > 0f)
            {
                var knockbackDir = (transform.position - damageMessage.damageSource).normalized;
                knockbackDir.y = 0f;
                if (knockbackDir.sqrMagnitude > 0.001f)
                {
                    _knockbackVelocity = knockbackDir * (damageMessage.knockbackForce * 0.25f);
                    _isGrounded = false;
                }
            }

            if (damageMessage.knockbackForce > 0f && damageMessage.damager != null)
            {
                var attackerController = damageMessage.damager.GetComponent<HumanoidController>();
                if (attackerController != null)
                {
                    var knockbackDir = (damageMessage.damageSource - transform.position).normalized;
                    knockbackDir.y = 0f;
                    if (knockbackDir.sqrMagnitude > 0.001f)
                    {
                        var returnForce = damageMessage.knockbackForce * 0.25f;
                        attackerController.ApplyKnockback(knockbackDir * returnForce);
                    }
                }
            }

            if (IsPlayer)
            {
                _cameraSettings.Shake(0.5f, 0.2f, CinemachineImpulseDefinition.ImpulseShapes.Rumble, new Vector3(0.2f, 0, 0.5f));
            }

            return true;
        }

        private void Damaged(Damageable.DamageMessage damageMessage)
        {
            if (_damageTriggeredThisFixedUpdate) return;

            _damageTriggeredThisFixedUpdate = true;
            _animCache.TriggerHurt();

            var forward   = damageMessage.damageSource - transform.position;
            forward.y         = 0f;
            var localHurt = transform.InverseTransformDirection(forward);
            _animCache.SetHurtDirection(localHurt.x, localHurt.z);

            if (hurtAudioPlayer)
                hurtAudioPlayer.PlayRandomClipOneShot();

            if (IsPlayer)
            {
                _cameraSettings.Shake();
            }

            if (damageMessage.knockbackForce > 0f)
            {
                var knockbackDir = (transform.position - damageMessage.damageSource).normalized;
                knockbackDir.y = 0f;
                if (knockbackDir.sqrMagnitude > 0.001f)
                {
                    _knockbackVelocity = knockbackDir * damageMessage.knockbackForce;
                    _isGrounded = false;
                }
            }
        }

        public void Die(Damageable.DamageMessage damageMessage)
        {
            _animCache.TriggerDeath();
            _forwardSpeed              = 0f;
            _verticalSpeed             = 0f;
            _knockbackVelocity         = Vector3.zero;
            _respawning                = true;
            _damageable.isInvulnerable = true;
        }

        public void ApplyKnockback(Vector3 knockbackVelocity)
        {
            _knockbackVelocity = knockbackVelocity;
            _isGrounded = false;
        }
    }
}