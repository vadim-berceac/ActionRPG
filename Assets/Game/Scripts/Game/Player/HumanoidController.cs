using System.Runtime.CompilerServices;
using UnityEngine;
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
        #region Serialized Fields

        [field: SerializeField] public bool IsPlayer { get; private set; }
        [field: SerializeField] public Transform ModelTransform { get; private set; }
        [field: SerializeField] public LayerMask TargetLayer { get; private set; }
        [field: SerializeField] public RangeWeapon RangeWeaponRoot { get; private set; }
        [field: SerializeField] public PropBones PropBones { get; private set; }

        [field: Header("Sound")]
        [field: SerializeField] public RandomAudioPlayer FootstepPlayer { get; private set; }
        [field: SerializeField] public RandomAudioPlayer HurtAudioPlayer { get; private set; }
        [field: SerializeField] public RandomAudioPlayer LandingPlayer { get; private set; }
        [field: SerializeField] public RandomAudioPlayer EmoteLandingPlayer { get; private set; }
        [field: SerializeField] public RandomAudioPlayer EmoteDeathPlayer { get; private set; }
        [field: SerializeField] public RandomAudioPlayer EmoteAttackPlayer { get; private set; }
        [field: SerializeField] public RandomAudioPlayer EmoteJumpPlayer { get; private set; }
        [field: SerializeField] public AudioSource BlockAudioSource { get; private set; }

        [field: SerializeField] public CharacterParamsSettings Settings { get; private set; }

        #endregion

        #region Public Properties

        public bool IsGrounded => _charCtrl && _charCtrl.isGrounded && _isGrounded;
        public bool HasPrimaryWeapon => _primaryWeaponInstance;
        public bool HasRangeWeapon => _rangedWeaponInstance;
        public bool HasAdditionalWeapon => _additionalWeaponInstance;
        public bool IsBlocking { get; private set; }
        public bool IsInteracting { get; private set; }
        public int PrimaryWeaponIndex => _primaryWeaponData ? _primaryWeaponData.AnimationSetIndex : 0;
        public int RangeWeaponIndex => _rangedWeaponData ? _rangedWeaponData.AnimationSetIndex : 0;
        public float PrimaryWeaponPreferredAttackDistance => _primaryWeaponData.preferredDistance;
        public float RangeWeaponPreferredDistance => _rangedWeaponData ? _rangedWeaponData.preferredDistance : 35f;
        public float LoadProgressCurve => _animCache.LoadProgressCurve;
        public bool IsMeleeWeaponEquipped => _isMeleeWeaponEquipped;
        public bool IsRangedWeaponEquipped => _isRangeWeaponEquipped;
        public bool IsShootPressed => _shootPressed;
        public Stamina Stamina { get; private set; }
        public PlayableGraphHandle Graph { get; private set; }

        private bool IsDead => _damageable.currentHitPoints < 1;

        #endregion

        #region Private Fields

        private CameraSettings _cameraSettings;
        private DiContainer _diContainer;
        private bool _isMeleeWeaponEquipped;
        private bool _isRangeWeaponEquipped;

        private AnimatorStateCache _animCache;
        private Animator _animator;

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
        private float _coyoteTimer;
        private bool _isJumping;
        private bool _fallOriginCaptured;
        private float _fallOriginY;
        private bool _animGrounded = true;

        private RangedAttackHandler _rangedAttackHandler;
        private bool _shootPressed;
        private bool _bowCameraOn;
        private Quaternion _modelOriginalLocalRotation;

        private IInput _input;
        private CharacterController _charCtrl;
        private Material _currentWalkingSurface;
        private Quaternion _targetRotation;
        private float _angleDiff;
        private float _turnVelocity;
        private bool _inAttack;
        private bool _isShoot;
        private bool _blockTriggeredThisFixedUpdate;
        private bool _damageTriggeredThisFixedUpdate;
        private Damageable _damageable;
        private Transform _transform;
        private float _idleTimer;
        private Vector3 _knockbackVelocity;
        private GameObject _projectileView;

        private int[] _comboHashes;
        private Vector3? _rangedTargetPosition;

        private bool IsMoveInput => !Mathf.Approximately(_input.MoveInput.sqrMagnitude, 0f);

        #endregion

        #region Injection & Unity Lifecycle

        [Inject]
        private void Construct(DiContainer container, CameraSettings cameraSettings, 
            PlayerInputHandlerService playerInputHandlerService)
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
            _animator = GetComponent<Animator>();
            _animCache = new AnimatorStateCache(_animator, RangeWeaponRoot);
            _transform = transform;
            _coyoteTimer = Settings.CharacterParams.CoyoteTime;

            if (ModelTransform)
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

            Stamina = new Stamina(_damageable, Settings.CharacterParams);

            if (RangeWeaponRoot)
            {
                _rangedAttackHandler = new RangedAttackHandler(RangeWeaponRoot, _damageable, TargetLayer);
            }

            Graph = PlayableGraphHandle.Create(_animator);
        }

        private void OnDisable()
        {
            _damageable.onDamageMessageReceivers.Remove(this);
            _damageable.onDamageBlocked = null;

            Stamina.Dispose();
            Graph.Destroy();
            Graph = null;
        }

        private void Update()
        {
            if (IsInteracting && Graph != null && Graph.IsValid)
            {
                Graph.Evaluate(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (!Settings) return;

            _animCache.OnUpdate();
            _animCache.SetStateTime();
            _animCache.SetInteract(IsInteracting);

            if (IsDead)
            {
                _previouslyGrounded = _isGrounded;
                return;
            }

            _blockTriggeredThisFixedUpdate = false;
            _damageTriggeredThisFixedUpdate = false;
            
            ConnectWeaponToHands(_isMeleeWeaponEquipped, _primaryWeaponData,    _primaryWeaponInstance,    _animCache.HashAttack1);
            ConnectWeaponToHands(_isMeleeWeaponEquipped, _additionalWeaponData, _additionalWeaponInstance, _animCache.HashAttack2);
            ConnectWeaponToHands(_isRangeWeaponEquipped, _rangedWeaponData, _rangedWeaponInstance, _animCache.Shoot);

            if (IsInteracting)
            {
                return;
            }

            UpdateInputBlocking();
            ProcessAttack();
            ProcessBlocking();
            ProcessShoot();
            CalcForwardMovement();
            CalcVerticalMovement();

            TimeoutToIdle();

            _previouslyGrounded = _isGrounded;
        }

        private void LateUpdate()
        {
            if (IsDead || IsInteracting) return;

            CalcOrientation();
            ApplyOrientation();
            PlayAudio();
        }

        private void OnAnimatorMove()
        {
            Vector3 movement;

            if (_isGrounded)
            {
                var ray = new Ray(_transform.position + Vector3.up
                    * Constants.GroundedRayDistance * 0.5f, -Vector3.up);
                if (Physics.Raycast(ray, out var hit, Constants.GroundedRayDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
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
                movement = _forwardSpeed * _transform.forward * Time.deltaTime;
            }

            movement += _verticalSpeed * Vector3.up * Time.deltaTime;
            _charCtrl.Move(movement);

            if (_knockbackVelocity.sqrMagnitude > 0.01f)
            {
                var knockbackMovement = _knockbackVelocity * Time.deltaTime;
                _charCtrl.Move(knockbackMovement);
                _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, Constants.KnockbackDeceleration * Time.deltaTime);
            }
            else
            {
                _knockbackVelocity = Vector3.zero;
            }

            _isGrounded = _charCtrl.isGrounded;

            if (_isGrounded)
            {
                _coyoteTimer        = Settings.CharacterParams.CoyoteTime;
                _isJumping          = false;
                _fallOriginCaptured = false;
                _animGrounded       = true;
            }
            else
            {
                _coyoteTimer -= Time.deltaTime;
                _animCache.SetAirborneVerticalSpeed(_verticalSpeed);

                if (_isJumping)
                {
                    _animGrounded = false;
                }
                else if (_animGrounded)
                {
                    if (!_fallOriginCaptured)
                    {
                        _fallOriginCaptured = true;
                        _fallOriginY = _transform.position.y;
                    }

                    var fallDistance = _fallOriginY - _transform.position.y;
                    if (fallDistance >= Settings.CharacterParams.MinFallHeightForAirborneAnim)
                    {
                        _animGrounded = false;
                    }
                }
            }

            _animCache.SetGrounded(_animGrounded);
        }

        #endregion

        #region Weapon Setup

        private void ConnectCombo(WeaponData data)
        {
            _comboHashes = new int[data.ComboNames.Length];
            for (var i = 0; i < data.ComboNames.Length; i++)
            {
                _comboHashes[i] = Animator.StringToHash(data.ComboNames[i]);
            }
        }

        private bool CheckCombo()
        {
            if (_comboHashes == null) return false;

            foreach (var hash in _comboHashes)
            {
                if (_animCache.IsInState(hash)) return true;
            }

            return false;
        }

        private void CreateWeapon(WeaponData fromData, ref WeaponData prevData, ref WeaponInstance weaponInstance, int trigger)
        {
            SetIsMeleeWeaponEquipped(false);

            if (weaponInstance)
            {
                weaponInstance.DestroyInstance();
            }
            if (!fromData)
            {
                prevData = null;
                return;
            }

            prevData = fromData;
            var weaponObj = prevData.GetViewInstance(_transform, _diContainer);
            weaponInstance = weaponObj.GetComponent<WeaponInstance>();
            weaponInstance.Initialize(gameObject, TargetLayer);
            weaponInstance.SetWeaponData(prevData);
            weaponInstance.SetKnockbackForce(prevData.knockbackForce);
            weaponInstance.SetStaticParts(prevData.GetStaticParts(PropBones, _diContainer));
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

        public void SetInteracting(bool value)
        {
            IsInteracting = value;
        }

        public void PlayInteractClip(AnimationClip clip, float blendLength)
        {
            if (Graph == null || !Graph.IsValid || clip == null) return;

            Graph.PlayClip(_animator, clip, blendLength);
        }
        
        public void StopInteractClip()
        {
            if (Graph == null || !Graph.IsValid) return;

            Graph.Stop();
        }

        private void ConnectWeaponToHands(bool equip, WeaponData data, WeaponInstance weaponInstanceInstance, int trigger)
        {
            if (!data) return;

            var settings = equip ? data.ActiveProp : data.UnActiveProp;

            if (weaponInstanceInstance)
            {
                weaponInstanceInstance.SetViewParent(PropBones, settings);
            }

            if (!equip)
                _animCache.ResetTrigger(trigger);
        }

        #endregion

        #region Combat Processing

        private void UpdateInputBlocking()
        {
            _input.InputBlocked = _animCache.IsInputBlocked();
        }

        private void ProcessAttack()
        {
            _animCache.SetHasAdditionalWeapon(_additionalWeaponData);
            _animCache.ResetAttack1();
            _animCache.ResetAttack2();

            if (IsBlocking)
            {
                return;
            }

            if (_input.Attack1 && Stamina.HasEnoughStamina(Settings.CharacterParams.Attack1StaminaCost))
            {
                _animCache.TriggerAttack1();
            }

            if (_input.Attack2 && Stamina.HasEnoughStamina(Settings.CharacterParams.Attack2StaminaCost))
            {
                _animCache.TriggerAttack2();
            }
        }

        private void ProcessBlocking()
        {
            if (!_primaryWeaponInstance && !_additionalWeaponInstance)
            {
                IsBlocking = false;
                _animCache.SetBlock(false);
                return;
            }

            if (_input.Block)
            {
                var holdCost = Settings.CharacterParams.BlockHoldStaminaCostPerSecond * Time.deltaTime;

                if (holdCost > 0f && !Stamina.TryChangeStamina(-holdCost))
                {
                    IsBlocking = false;
                    _animCache.SetBlock(false);
                    return;
                }

                IsBlocking = true;
            }
            else
            {
                IsBlocking = false;
            }

            _animCache.SetBlock(IsBlocking);
        }

        private void ProcessShoot()
        {
            var canShoot = _rangedWeaponInstance && _ammunitionWeaponInstance;

            if (!canShoot || !Stamina.HasEnoughStamina(Settings.CharacterParams.ShootStaminaCost))
            {
                _shootPressed = false;
                _isShoot = false;
                _animCache.SetShoot(false);

                if (IsPlayer && _bowCameraOn)
                {
                    _bowCameraOn = false;
                    _cameraSettings.SwitchCamera(CameraSettings.CameraType.Exploration);
                }

                return;
            }

            if (_input.Shoot)
            {
                var holdCost = Settings.CharacterParams.AimHoldStaminaCostPerSecond * Time.deltaTime;

                if (holdCost > 0f && !Stamina.TryChangeStamina(-holdCost))
                {
                    _shootPressed = false;
                }
                else
                {
                    _shootPressed = true;
                }
            }
            else
            {
                _shootPressed = false;
            }

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

        #endregion

        #region Movement & Orientation

        private void CalcForwardMovement()
        {
            var moveInput = IsBlocking || (_shootPressed && IsPlayer) || _inAttack ? Vector2.zero : _input.MoveInput;
            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            _desiredForwardSpeed = moveInput.magnitude * Settings.CharacterParams.MaxForwardSpeed;
            var acceleration    = IsMoveInput ? Constants.GroundAcceleration : Constants.GroundDeceleration;
            _forwardSpeed        = Mathf.MoveTowards(_forwardSpeed, _desiredForwardSpeed, acceleration * Time.deltaTime);

            _animCache.SetForwardSpeed(_forwardSpeed);
        }

        private void CalcVerticalMovement()
        {
            if (!_input.JumpInput && _isGrounded)
            {
                _readyToJump = true;
            }

            var canJump = _coyoteTimer > 0f;

            if (_input.JumpInput && canJump && _readyToJump && !_inAttack && !IsBlocking)
            {
                _verticalSpeed = Settings.CharacterParams.JumpSpeed;
                _isGrounded    = false;
                _coyoteTimer   = 0f;
                _readyToJump   = false;
                _isJumping     = true;
                return;
            }

            if (_isGrounded)
            {
                _verticalSpeed = -Settings.CharacterParams.Gravity * Constants.StickingGravityProportion;
            }
            else
            {
                if (!_input.JumpInput && _verticalSpeed > 0.0f)
                    _verticalSpeed -= Constants.JumpAbortSpeed * Time.deltaTime;

                if (Mathf.Approximately(_verticalSpeed, 0f))
                    _verticalSpeed = 0f;

                _verticalSpeed -= Settings.CharacterParams.Gravity * Time.deltaTime;
            }
        }

        private void CalcOrientation()
        {
            var cameraForward = Quaternion.Euler(0f, _input.RotationYaw, 0f) * Vector3.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            if (_inAttack || IsBlocking || _shootPressed)
            {
                _targetRotation = Quaternion.LookRotation(cameraForward);
                _angleDiff = Mathf.DeltaAngle(
                    Mathf.Atan2(_transform.forward.x, _transform.forward.z) * Mathf.Rad2Deg,
                    Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg
                );
                return;
            }

            var moveInput = _input.MoveInput;

            if (moveInput.sqrMagnitude < 0.0001f)
            {
                _angleDiff = 0f;
                _targetRotation = _transform.rotation;
                return;
            }

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

            var angleCurrent = Mathf.Atan2(_transform.forward.x, _transform.forward.z) * Mathf.Rad2Deg;
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
                || IsBlocking
                || _shootPressed;
        }

        private void ApplyOrientation()
        {
            if (!IsOrientationUpdated() && !(IsMoveInput || IsBlocking || _shootPressed || _inAttack)) return;

            _animCache.SetAngleDeltaRad(_angleDiff * Mathf.Deg2Rad);

            if (_shootPressed)
            {
                _transform.rotation = Quaternion.RotateTowards(_transform.rotation, _targetRotation,
                    Settings.CharacterParams.AimTurnSpeed * Time.deltaTime);
                return;
            }

            if (IsBlocking || _inAttack)
            {
                _transform.rotation = Quaternion.RotateTowards(_transform.rotation, _targetRotation,
                    Settings.CharacterParams.CombatTurnSpeed * Time.deltaTime);
                return;
            }

            var currentEuler = _transform.rotation.eulerAngles.y;
            var targetEuler  = _targetRotation.eulerAngles.y;

            var newYaw = Mathf.SmoothDampAngle(currentEuler, targetEuler, ref _turnVelocity,
                Settings.CharacterParams.GroundedTurnSmoothTime);
            _transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        }

        private void TimeoutToIdle()
        {
            var inputDetected = IsMoveInput || IsBlocking || _shootPressed || _inAttack || _input.Attack1 || _input.Attack2 || _input.JumpInput;

            if (_isGrounded && !inputDetected)
            {
                _idleTimer += Time.deltaTime;
                if (_idleTimer >= Settings.CharacterParams.IdleTimeout)
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

        #endregion

        #region Audio

        private void PlayAudio()
        {
            var footfall = _animCache.FootFall;

            if (footfall > 0.01f && !FootstepPlayer.playing)
            {
                FootstepPlayer.playing = true;
                FootstepPlayer.PlayRandomClip(_currentWalkingSurface, _forwardSpeed < 4 ? 0 : 1);
            }
            else if (FootstepPlayer.playing)
            {
                FootstepPlayer.playing = false;
            }

            if (_isGrounded && !_previouslyGrounded)
            {
                LandingPlayer.PlayRandomClipOneShot(_currentWalkingSurface, bankId: _forwardSpeed < 4 ? 0 : 1);
                EmoteLandingPlayer.PlayRandomClipOneShot();
            }

            if (!_isGrounded && _previouslyGrounded && _verticalSpeed > 0f)
                EmoteJumpPlayer.PlayRandomClipOneShot();

            if (_animCache.JustEntered(_animCache.HashHurt))
                HurtAudioPlayer.PlayRandomClipOneShot();

            if (_animCache.JustEntered(_animCache.HashDeath))
                EmoteDeathPlayer.PlayRandomClip();

            if (_comboHashes == null || _comboHashes.Length < 1) return;

            foreach (var hash in _comboHashes)
            {
                if (!_animCache.JustEntered(hash))
                {
                    continue;
                }
                EmoteAttackPlayer.PlayRandomClipOneShot();
                break;
            }
        }

        private void PlayBlockSound()
        {
            AudioClip clip = null;

            if (_additionalWeaponData)
                clip = _additionalWeaponData.blockSound;

            if (!clip && _primaryWeaponData)
                clip = _primaryWeaponData.blockSound;

            if (!clip) return;

            BlockAudioSource.clip = clip;
            BlockAudioSource.Play();
        }

        #endregion

        #region Ranged Attack Helpers

        public void SetRangedTargetPosition(Vector3 position)
        {
            _rangedTargetPosition = position;
        }

        private Vector3 ResolveShootTarget()
        {
            var ray = new Ray(_transform.position + Vector3.up * 1.2f, _transform.forward);
            if (Physics.Raycast(ray, out var hit, 100f, TargetLayer, QueryTriggerInteraction.Ignore))
            {
                var targetDamageable = hit.collider.GetComponentInParent<Damageable>();
                if (targetDamageable != null && targetDamageable.currentHitPoints > 0)
                {
                    return hit.collider.bounds.center;
                }
            }

            if (_rangedTargetPosition.HasValue)
                return _rangedTargetPosition.Value;

            return _transform.position + Vector3.up * 1.2f + _transform.forward * 20f;
        }

        private bool IsFacingDamageSource(Vector3 damageSource)
        {
            var toSource = (damageSource - _transform.position).normalized;
            toSource.y = 0f;
            return Vector3.Dot(_transform.forward, toSource) > 0f;
        }

        public void CreateProjectile()
        {
            if(_projectileView) return;

            _projectileView = Instantiate(_ammunitionWeaponData.ViewPrefab);
            _ammunitionWeaponData.ActiveProp.SetPropBone(_projectileView.transform, PropBones);
        }

        public void DestroyProjectile()
        {
            if (!_projectileView) return;

            Destroy(_projectileView);
            _projectileView = null;
        }

        #endregion

        #region Animation Events

        public void TriggerRangedAttack()
        {
            _input.Attack1 = true;
        }

        public void Shoot()
        {
            if (_rangedAttackHandler == null || !_rangedAttackHandler.IsValid)
                return;

            Stamina.TryChangeStamina(-Settings.CharacterParams.ShootStaminaCost);

            var targetPosition = ResolveShootTarget();
            _rangedAttackHandler.Shoot(targetPosition);
        }

        public void MeleeAttackStart(int throwing = 0)
        {
            if (!_primaryWeaponInstance) return;

            Stamina.TryChangeStamina(-Settings.CharacterParams.Attack1StaminaCost);

            _primaryWeaponInstance.BeginAttack(throwing != 0);
            _inAttack = true;
        }

        public void MeleeAttackEnd()
        {
            if (!_primaryWeaponInstance) return;
            _primaryWeaponInstance.EndAttack();
            _inAttack = false;
        }

        public void AdditionalAttackStart(int throwing = 0)
        {
            if (!_additionalWeaponInstance) return;

            Stamina.TryChangeStamina(-Settings.CharacterParams.Attack2StaminaCost);

            _additionalWeaponInstance.BeginAttack(throwing != 0);
            _inAttack = true;
        }

        public void AdditionalAttackEnd()
        {
            if (!_additionalWeaponInstance) return;
            _additionalWeaponInstance.EndAttack();
            _inAttack = false;
        }

        #endregion

        #region Damage & Death

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
            if (!IsBlocking || !IsFacingDamageSource(damageMessage.damageSource))
                return false;

            if (_blockTriggeredThisFixedUpdate)
                return true;

            if (!Stamina.TryChangeStamina(-Settings.CharacterParams.BlockStaminaCost))
                return false;

            _blockTriggeredThisFixedUpdate = true;
            PlayBlockSound();
            _animCache.TriggerBlock();

            if (damageMessage.knockbackForce > 0f)
            {
                var knockbackDir = (_transform.position - damageMessage.damageSource).normalized;
                if (knockbackDir.sqrMagnitude > 0.001f)
                {
                    _knockbackVelocity = knockbackDir * (damageMessage.knockbackForce * 0.25f);
                    _isGrounded  = false;
                    _coyoteTimer = 0f;
                }
            }

            if (damageMessage.knockbackForce > 0f && damageMessage.damager)
            {
                var attackerController = damageMessage.damager.GetComponent<HumanoidController>();
                if (attackerController)
                {
                    var knockbackDir = (damageMessage.damageSource - _transform.position).normalized;
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

            var forward   = damageMessage.damageSource - _transform.position;
            forward.y         = 0f;
            var localHurt = _transform.InverseTransformDirection(forward);
            _animCache.SetHurtDirection(localHurt.x, localHurt.z);

            if (HurtAudioPlayer)
            {
                HurtAudioPlayer.PlayRandomClipOneShot();
            }

            if (IsPlayer)
            {
                _cameraSettings.Shake();
            }

            if (damageMessage.knockbackForce <= 0f)
            {
                return;
            }

            var knockbackDir = (_transform.position - damageMessage.damageSource).normalized;

            if (knockbackDir.sqrMagnitude <= 0.001f)
            {
                return;
            }
            _knockbackVelocity = knockbackDir * damageMessage.knockbackForce;
            _isGrounded  = false;
            _coyoteTimer = 0f;
        }

        public void Die(Damageable.DamageMessage damageMessage)
        {
            _animCache.TriggerDeath(true);
            _forwardSpeed              = 0f;
            _verticalSpeed             = 0f;
            _knockbackVelocity         = Vector3.zero;
            _damageable.isInvulnerable = true;

            _inAttack       = false;
            IsBlocking      = false;
            _shootPressed   = false;
            _isShoot        = false;
            _readyToJump    = false;
            _isJumping      = false;
            _bowCameraOn    = false;
            _idleTimer      = 0f;

            _animCache.SetBlock(false);
            _animCache.SetShoot(false);
        }

        private void ApplyKnockback(Vector3 knockbackVelocity)
        {
            _knockbackVelocity = knockbackVelocity;
            _isGrounded  = false;
            _coyoteTimer = 0f;
        }

        #endregion
    }
}