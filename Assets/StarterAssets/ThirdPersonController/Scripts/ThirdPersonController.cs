using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        public float MoveSpeed = 2.0f;
        public float SprintSpeed = 5.335f;
        [Range(0.0f, 0.3f)] public float RotationSmoothTime = 0.12f;
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        public float JumpHeight  = 1.2f;

        [Space(10)]
        public float JumpTimeout = 0.50f;
        public float FallTimeout = 0.15f;

        [Header("Gravity Orientation")]
        [Tooltip("How fast the player body rotates to align with new gravity")]
        public float OrientationSpeed = 10f;

        [Header("Player Grounded")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.28f;
        public LayerMask GroundLayers;
        private Vector3 _lastGravityUp = Vector3.up;

        [Header("Cinemachine")]
        public GameObject CinemachineCameraTarget;
        public float TopClamp            = 70.0f;
        public float BottomClamp         = -30.0f;
        public float CameraAngleOverride  = 0.0f;
        public bool  LockCameraPosition   = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _rotationVelocity;
        private float _verticalVelocity;   // speed along UpDirection axis (+up / -down)
        private float _terminalVelocity = 53.0f;

        // timeouts
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

        // ── Camera transition state ────────────────────────────────────────────
        // When gravity starts changing we lock mouse input and remap yaw/pitch each frame
        // so the camera's world-space look direction stays constant while the gravity
        // reference frame rotates beneath it.  The result is a smooth roll that matches
        // the style shown in the reference video.
        private bool    _wasTransitioning;
        private Vector3 _cameraWorldForwardAtTransitionStart;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Animator            _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject          _mainCamera;

        private const float _threshold = 0.01f;
        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }

        private void Start()
        {
           _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

        _hasAnimator = TryGetComponent(out _animator);
        _controller  = GetComponent<CharacterController>();
        _input       = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
        _playerInput = GetComponent<PlayerInput>();
#else
        Debug.LogError("Starter Assets package is missing dependencies. ...");
#endif

        // Allow the character to stand on any surface regardless of world-Y slope.
        _controller.slopeLimit = 90f;   // ← ADD THIS

        AssignAnimationIDs();
        _jumpTimeoutDelta = 0f;
        _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            ResetVelocityOnGravitySwitch();
            TrackGravityTransition(); // must be first — keeps yaw/pitch correct this frame
            // AlignWithGravity();
            GroundedCheck();
            JumpAndGravity();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        // ── Animation IDs ──────────────────────────────────────────────────────

        private void AssignAnimationIDs()
        {
            _animIDSpeed       = Animator.StringToHash("Speed");
            _animIDGrounded    = Animator.StringToHash("Grounded");
            _animIDJump        = Animator.StringToHash("Jump");
            _animIDFreeFall    = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        // ── Grounded check ─────────────────────────────────────────────────────

        private void GroundedCheck()
        {
            // Sphere sits 0.14 units toward "up" from the pivot — inside the capsule
            // bottom — so it detects any floor regardless of gravity direction.
            Vector3 up             = GravityManager.Instance.UpDirection;
            Vector3 spherePosition = transform.position + up * (-GroundedOffset);

            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            if (_hasAnimator)
                _animator.SetBool(_animIDGrounded, Grounded);
        }

        // ── Camera rotation (gravity-relative + smooth transition) ─────────────
        //
        // WHAT CHANGED vs the original:
        //   1. We build the final rotation inside a "gravity frame" (a quaternion that
        //      rotates world-Y to the current anti-gravity direction).  This means yaw/pitch
        //      are always measured relative to whatever surface the player is standing on.
        //
        //   2. Mouse input is locked while GravityManager.IsTransitioning is true.
        //      Instead, TrackGravityTransition() back-solves yaw/pitch each frame to hold
        //      the camera's world look-direction constant — producing the smooth roll you
        //      see in the reference video.

        private void CameraRotation()
        {
            bool transitioning = GravityManager.Instance != null &&
                                 GravityManager.Instance.IsTransitioning;

            // Accept look input only when gravity is settled
            if (!transitioning && _input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                float mult = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
                _cinemachineTargetYaw   += _input.look.x * mult;
                _cinemachineTargetPitch += _input.look.y * mult;
            }

            _cinemachineTargetYaw   = ClampAngle(_cinemachineTargetYaw,   float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp,    TopClamp);

            // gravityFrame rotates world-Y onto the current anti-gravity direction.
            // Pre-multiplying turns our local pitch/yaw into the correct world-space
            // rotation for any gravity direction.
            Vector3    gravityUp    = GravityManager.Instance != null ? GravityManager.Instance.UpDirection : Vector3.up;
            Quaternion gravityFrame = Quaternion.FromToRotation(Vector3.up, gravityUp);

            CinemachineCameraTarget.transform.rotation =
                gravityFrame *
                Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
        }

        // ── Gravity transition tracking ────────────────────────────────────────

        private void TrackGravityTransition()
        {
            if (GravityManager.Instance == null) return;
            bool isTransitioning = GravityManager.Instance.IsTransitioning;

            // On the first frame of a transition, snapshot the camera's world forward.
            if (isTransitioning && !_wasTransitioning)
                _cameraWorldForwardAtTransitionStart = CinemachineCameraTarget.transform.forward;

            // While gravity is rotating, keep remapping yaw/pitch so CameraRotation()
            // produces the same world-space look direction in the new (rotating) frame.
            if (isTransitioning)
                RemapCameraAnglesToCurrentGravityFrame(_cameraWorldForwardAtTransitionStart);

            _wasTransitioning = isTransitioning;
        }

        /// <summary>
        /// Back-solve for the yaw/pitch angles that reproduce <paramref name="worldForward"/>
        /// in the current gravity reference frame, and store them in the target fields.
        /// </summary>
        private void RemapCameraAnglesToCurrentGravityFrame(Vector3 worldForward)
        {
            Vector3    gravityUp    = GravityManager.Instance.UpDirection;
            Quaternion gravityFrame = Quaternion.FromToRotation(Vector3.up, gravityUp);

            // Express the desired world-forward in the gravity frame's local space
            Vector3 localForward = Quaternion.Inverse(gravityFrame) * worldForward;
            localForward.Normalize();

            // Yaw  = horizontal angle around local-Y
            _cinemachineTargetYaw = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;

            // Pitch = vertical angle (negative because Unity's pitch convention is inverted)
            _cinemachineTargetPitch = -Mathf.Asin(Mathf.Clamp(localForward.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        // ── Movement (gravity-plane aware) ─────────────────────────────────────

        private void Move()
        {
           float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            Vector3 gravityUp = GravityManager.Instance.UpDirection;

            float currentHorizontalSpeed =
                Vector3.ProjectOnPlane(_controller.velocity, gravityUp).magnitude;

            float speedOffset    = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            Vector3 camForward = Vector3.ProjectOnPlane(_mainCamera.transform.forward, gravityUp).normalized;
            Vector3 camRight   = Vector3.ProjectOnPlane(_mainCamera.transform.right,   gravityUp).normalized;

            Vector3 targetDirection = Vector3.zero;

            if (_input.move != Vector2.zero)
                targetDirection = (camForward * _input.move.y + camRight * _input.move.x).normalized;

            // ── Unified rotation: handles both movement steering and gravity alignment ──
            // Preserves current yaw when idle so the player doesn't snap to a default forward
            Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, gravityUp);
            if (currentForward.sqrMagnitude < 0.001f)
                currentForward = Vector3.ProjectOnPlane(Vector3.forward, gravityUp);

            Vector3 desiredForward = (targetDirection.sqrMagnitude > 0.001f)
                ? targetDirection
                : currentForward.normalized;

            Quaternion targetRotation = Quaternion.LookRotation(desiredForward, gravityUp);

            // Track the gravity slerp faster during transitions so the capsule doesn't lag
            float rotRate = GravityManager.Instance.IsTransitioning
                ? OrientationSpeed * 3f
                : (1f / Mathf.Max(RotationSmoothTime, 0.001f));

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                Time.deltaTime * rotRate);

            // ── Apply movement ──
            _controller.Move(
                (targetDirection.sqrMagnitude > 0.001f ? targetDirection : Vector3.zero)
                    * (_speed * Time.deltaTime)
                + gravityUp * (_verticalVelocity * Time.deltaTime));

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed,       _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        // ── Jump and gravity ───────────────────────────────────────────────────

        private void JumpAndGravity()
        {
           float g = GravityManager.Instance != null ? GravityManager.Instance.GravityStrength : 15f;

    if (Grounded)
    {
        _fallTimeoutDelta = FallTimeout;

        if (_hasAnimator)
        {
            _animator.SetBool(_animIDJump, false);
            _animator.SetBool(_animIDFreeFall, false);
        }

        // Stop infinite downward acceleration when on floor
        if (_verticalVelocity < 0.0f)
        {
            _verticalVelocity = -2f;
        }

        // Jump logic
        if (_input.jump && _jumpTimeoutDelta <= 0.0f)
        {
            // Calculate jump velocity
            _verticalVelocity = Mathf.Sqrt(JumpHeight * 2f * g);

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDJump, true);
            }
            
            // IMPORTANT: Manually set Grounded to false so gravity 
            // starts applying immediately on the next frame.
            Grounded = false; 
        }

        if (_jumpTimeoutDelta >= 0.0f)
        {
            _jumpTimeoutDelta -= Time.deltaTime;
        }
    }
    else
    {
        // Reset jump timeout when in air
        _jumpTimeoutDelta = JumpTimeout;

        if (_fallTimeoutDelta >= 0.0f)
        {
            _fallTimeoutDelta -= Time.deltaTime;
        }
        else if (_hasAnimator)
        {
            _animator.SetBool(_animIDFreeFall, true);
        }

        // Cancel jump input so we don't double jump instantly
        _input.jump = false;
    }

    // APPLY GRAVITY ALWAYS (unless we hit terminal velocity)
    if (_verticalVelocity > -_terminalVelocity)
    {
        _verticalVelocity -= g * Time.deltaTime;
    }
           
        }

        // ── Player body orientation ────────────────────────────────────────────

        private void AlignWithGravity()
        {
            Vector3 targetUp = GravityManager.Instance != null
                ? GravityManager.Instance.UpDirection : Vector3.up;

            if (Vector3.Dot(transform.up, targetUp) >= 0.9999f) return;

            Quaternion targetRotation =
                Quaternion.FromToRotation(transform.up, targetUp) * transform.rotation;

            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRotation, Time.deltaTime * OrientationSpeed);
        }

        // ── Utilities ──────────────────────────────────────────────────────────

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f)  lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Grounded ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
            Vector3 up = Application.isPlaying && GravityManager.Instance != null
                ? GravityManager.Instance.UpDirection : Vector3.up;
            Gizmos.DrawSphere(transform.position + up * (-GroundedOffset), GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && FootstepAudioClips.Length > 0)
            {
                int index = Random.Range(0, FootstepAudioClips.Length);
                AudioSource.PlayClipAtPoint(
                    FootstepAudioClips[index],
                    transform.TransformPoint(_controller.center),
                    FootstepAudioVolume);
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
                AudioSource.PlayClipAtPoint(
                    LandingAudioClip,
                    transform.TransformPoint(_controller.center),
                    FootstepAudioVolume);
            StartCoroutine(ResetRadius());
        }
    
    

        /// <summary>
        /// When the gravity axis changes substantially, wipe accumulated vertical
        /// velocity so the player doesn't get flung on the first frame of the new frame.
        /// </summary>
        private void ResetVelocityOnGravitySwitch()
        {
            if (GravityManager.Instance == null) return;

            Vector3 currentUp = GravityManager.Instance.UpDirection;

            // Only reset on the FIRST frame the axis changes significantly,
            // not on every frame of a smooth transition.
            bool axisChanged = Vector3.Dot(_lastGravityUp, currentUp) < 0.95f;
            bool wasAlreadyTransitioning = GravityManager.Instance.IsTransitioning && _wasTransitioning;

            if (axisChanged && !wasAlreadyTransitioning)
                _verticalVelocity = 0f;

            _lastGravityUp = currentUp;
        }

        IEnumerator ResetRadius()
        {
            GroundedRadius = 0.1f;
            yield return new WaitForSeconds(1.5f);
            GroundedRadius = 0.2f;
        }
        
    }
}