using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class RootMotionPlatformerController : MonoBehaviour
{
    [Header("Animator Parameters")]
    [Tooltip("Animator float parameter controlling locomotion blend: 0 idle, 0.5 walk, 1 run")]
    [SerializeField] private string moveParam = "Move";
    [Tooltip("Animator float parameter controlling turning: -0.5..0.5 while walking, -1..1 while running")]
    [SerializeField] private string turnParam = "Turn";
    [Tooltip("Animator bool parameter set true when character is falling")]
    [SerializeField] private string isFallingParam = "IsFalling";
    [Tooltip("Animator trigger for jumping")]
    [SerializeField] private string jumpTrigger = "Jump";
    [Tooltip("Animator trigger for a standard landing")]
    [SerializeField] private string landNormalTrigger = "Land";
    [Tooltip("Animator trigger for a hard landing")]
    [SerializeField] private string landHardTrigger = "LandHard";
    [Tooltip("Animator trigger for a damaging landing")]
    [SerializeField] private string landDamageTrigger = "LandDamage";
    [Tooltip("Animator trigger for vault action")]
    [SerializeField] private string vaultTrigger = "Vault";
    [Tooltip("Animator trigger for climb type 1 (Waist + Feet)")]
    [SerializeField] private string climb1Trigger = "Climb1";
    [Tooltip("Animator trigger for climb type 2 (Head + Waist + Feet)")]
    [SerializeField] private string climb2Trigger = "Climb2";
    [Tooltip("Animator state tag used for actions (Vault/Climb), disables gravity while active")]
    [SerializeField] private string actionTag = "Action";

    [Header("Input (optional)")]
    [Tooltip("Enable to use basic built-in input (WASD/Arrows + Shift to Run + Space to Jump)")]
    [SerializeField] private bool useBuiltInInput = true;
    [Tooltip("Turning sensitivity when walking (maps input to -0.5..0.5)")]
    [Range(0.1f, 5f)]
    [SerializeField] private float walkTurnSensitivity = 1f;
    [Tooltip("Turning sensitivity when running (maps input to -1..1)")]
    [Range(0.1f, 5f)]
    [SerializeField] private float runTurnSensitivity = 1.5f;
    [Tooltip("How fast the character rotates towards desired turn (deg/sec)")]
    [Range(90f, 720f)]
    [SerializeField] private float rotationSpeed = 360f;
    [Tooltip("Smoothing time for animator parameters (seconds)")]
    [Range(0f, 0.25f)]
    [SerializeField] private float animatorDampTime = 0.08f;

    [Header("Physics (Vertical Motion)")]
    [Tooltip("Upwards jump speed in m/s")]
    [Range(1f, 20f)]
    [SerializeField] private float jumpSpeed = 7.5f;
    [Tooltip("Gravity acceleration (m/s^2)")]
    [Range(1f, 50f)]
    [SerializeField] private float gravity = 18f;
    [Tooltip("Additional gravity multiplier while falling")]
    [Range(0.5f, 4f)]
    [SerializeField] private float fallGravityMultiplier = 1.25f;
    [Tooltip("Extra downward stick-to-ground force when grounded")]
    [Range(0f, 10f)]
    [SerializeField] private float stickToGroundForce = 4f;
    [Tooltip("Max distance to consider still grounded when stepping down slopes/steps")]
    [Range(0f, 0.6f)]
    [SerializeField] private float stepDownDistance = 0.3f;
    [Tooltip("Layers considered as ground for grounding and landing")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Landing Detection")]
    [Tooltip("Vertical speed threshold (m/s) for hard landing")]
    [Range(3f, 30f)]
    [SerializeField] private float hardLandingSpeed = 9f;
    [Tooltip("Vertical speed threshold (m/s) for damage landing")]
    [Range(6f, 60f)]
    [SerializeField] private float damageLandingSpeed = 15f;
    [Tooltip("Cooldown after landing triggers to prevent double-firing (s)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float landingCooldown = 0.1f;

    [Header("Ground & Ledge Probes")]
    [Tooltip("Radius of the ground check sphere")]
    [Range(0.05f, 0.6f)]
    [SerializeField] private float groundCheckRadius = 0.25f;
    [Tooltip("Offset from the CharacterController center to begin the ground check (downwards)")]
    [Range(0f, 1.0f)]
    [SerializeField] private float groundCheckOffset = 0.1f;
    [Tooltip("How far ahead to probe for ground to detect ledges")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float ledgeProbeForward = 0.6f;
    [Tooltip("Height above feet to start the ledge probe")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float ledgeProbeHeight = 0.6f;
    [Tooltip("Extra downward distance for ledge probe")]
    [Range(0.1f, 2f)]
    [SerializeField] private float ledgeProbeDown = 1.2f;

    [Header("Action Detection (SphereCasts)")]
    [Tooltip("Transform representing head probe origin, forward used for casts")]
    [SerializeField] private Transform headProbe;
    [Tooltip("Transform representing waist probe origin, forward used for casts")]
    [SerializeField] private Transform waistProbe;
    [Tooltip("Transform representing feet probe origin, forward used for casts")]
    [SerializeField] private Transform feetProbe;
    [Tooltip("Radius for action sphere casts")]
    [Range(0.05f, 0.6f)]
    [SerializeField] private float actionProbeRadius = 0.2f;
    [Tooltip("Distance forward for action sphere casts")]
    [Range(0.1f, 2.5f)]
    [SerializeField] private float actionProbeDistance = 0.8f;
    [Tooltip("Layers considered as climbable/vaultable geometry")]
    [SerializeField] private LayerMask actionLayers = ~0;
    [Tooltip("Minimum speed (Move param > this) to allow action detection")]
    [Range(0f, 1f)]
    [SerializeField] private float minMoveForActions = 0.25f;
    [Tooltip("Cooldown to avoid re-triggering actions repeatedly (seconds)")]
    [Range(0f, 2f)]
    [SerializeField] private float actionCooldown = 0.5f;

    [Header("Debug & Gizmos")]
    [Tooltip("Draw helpful gizmos for probes and motion")]
    [SerializeField] private bool drawGizmos = true;
    [Tooltip("Color for ground probe gizmos")]
    [SerializeField] private Color groundGizmoColor = new Color(0f, 1f, 0.5f, 0.4f);
    [Tooltip("Color for ledge probe gizmos")]
    [SerializeField] private Color ledgeGizmoColor = new Color(1f, 0.8f, 0f, 0.4f);
    [Tooltip("Color for action probe gizmos")]
    [SerializeField] private Color actionGizmoColor = new Color(0f, 0.5f, 1f, 0.4f);
    [Tooltip("Color for velocity gizmo")]
    [SerializeField] private Color velocityGizmoColor = new Color(1f, 0f, 0f, 0.6f);

    // Internal state
    private CharacterController controller;
    private Animator animator;

    private int moveHash, turnHash, isFallingHash;
    private int jumpHash, landHash, landHardHash, landDamageHash, vaultHash, climb1Hash, climb2Hash;

    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded;
    private bool wasGrounded;
    private bool forcedFall; // set by ledge detection to force falling even if CC reports grounded
    private float verticalVelocity;
    private float lastAirborneDownSpeed; // tracked to determine landing type
    private float lastLandingTime;
    private float lastActionTime;

    private bool inActionTaggedState;
    private float turnValue;  // -1..1 depending on run/walk mapping
    private float moveValue;  // 0, 0.5, 1 applied to Animator

    // Input buffering
    private bool jumpRequested;

    // Public read-only properties
    public bool IsFalling => !isGrounded;
    public bool InAction => inActionTaggedState;

    // External control API
    // - Call these if not using built-in input
    public void SetMoveAndTurn(float moveIntensity01, float turnInputNeg1To1, bool running)
    {
        moveValue = Mathf.Approximately(moveIntensity01, 0f) ? 0f : (running ? 1f : 0.5f);
        if (running)
            turnValue = Mathf.Clamp(turnInputNeg1To1, -1f, 1f);
        else
            turnValue = Mathf.Clamp(turnInputNeg1To1 * 0.5f, -0.5f, 0.5f);
    }

    public void RequestJump()
    {
        jumpRequested = true;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        moveHash = Animator.StringToHash(moveParam);
        turnHash = Animator.StringToHash(turnParam);
        isFallingHash = Animator.StringToHash(isFallingParam);
        jumpHash = Animator.StringToHash(jumpTrigger);
        landHash = Animator.StringToHash(landNormalTrigger);
        landHardHash = Animator.StringToHash(landHardTrigger);
        landDamageHash = Animator.StringToHash(landDamageTrigger);
        vaultHash = Animator.StringToHash(vaultTrigger);
        climb1Hash = Animator.StringToHash(climb1Trigger);
        climb2Hash = Animator.StringToHash(climb2Trigger);

        // We apply root motion manually
        animator.applyRootMotion = false;

        ValidateProbes();
    }

    private void OnValidate()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponent<Animator>();
        ValidateProbes();
    }

    private void ValidateProbes()
    {
        // Optional: auto-find child transforms named "HeadProbe"/"WaistProbe"/"FeetProbe"
        if (headProbe == null) headProbe = transform.Find("HeadProbe");
        if (waistProbe == null) waistProbe = transform.Find("WaistProbe");
        if (feetProbe == null) feetProbe = transform.Find("FeetProbe");
    }

    private void Update()
    {
        // Read input if enabled
        if (useBuiltInInput)
        {
            // Simple forward movement model: Vertical axis controls forward intent, Shift toggles run
            float forward = Input.GetAxisRaw("Vertical");
            bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            moveValue = Mathf.Approximately(forward, 0f) ? 0f : (run ? 1f : 0.5f);

            // Turning: Horizontal axis mapped to ranges depending on walk/run
            float horiz = Input.GetAxisRaw("Horizontal");
            float desiredTurn = 0f;
            if (run)
                desiredTurn = Mathf.Clamp(horiz * runTurnSensitivity, -1f, 1f);
            else
                desiredTurn = Mathf.Clamp(horiz * walkTurnSensitivity * 0.5f, -0.5f, 0.5f);

            // Smooth turn
            turnValue = Mathf.MoveTowards(turnValue, desiredTurn, Time.deltaTime * 3f);
            if (Input.GetKeyDown(KeyCode.Space)) jumpRequested = true;
        }

        // Update animator parameters (smoothed)
        animator.SetFloat(moveHash, moveValue, animatorDampTime, Time.deltaTime);
        animator.SetFloat(turnHash, turnValue, animatorDampTime, Time.deltaTime);

        // Rotate character based on desired turn input
        float maxTurnMagnitude = Mathf.Max(Mathf.Abs(turnValue), 0.001f);
        float signedTurn = Mathf.Sign(turnValue) * maxTurnMagnitude;
        transform.Rotate(0f, signedTurn * rotationSpeed * Time.deltaTime, 0f);

        // Check if currently in an Action-tagged state (disables gravity)
        UpdateActionTaggedState();

        // Grounding and ledge checks
        wasGrounded = isGrounded;
        GroundCheck();

        // Jump handling (physics vertical motion)
        HandleJumpAndGravity();

        // Action detection (Vault/Climb) using sphere casts
        if (!inActionTaggedState && Time.time >= lastActionTime + actionCooldown)
        {
            TryDetectAndTriggerAction();
        }

        // Set Animator IsFalling
        animator.SetBool(isFallingHash, !isGrounded);

        // Landing triggers
        HandleLandingTriggers();

        // Clear one-frame requests
        jumpRequested = false;
    }

    private void UpdateActionTaggedState()
    {
        // Check current & next state on base layer for 'Action' tag
        var state = animator.GetCurrentAnimatorStateInfo(0);
        var next = animator.GetNextAnimatorStateInfo(0);
        bool tagged = state.IsTag(actionTag) || next.IsTag(actionTag);
        inActionTaggedState = tagged;
    }

    private void HandleJumpAndGravity()
    {
        if (inActionTaggedState)
        {
            // Gravity disabled during Action; vertical driven by root motion
            lastAirborneDownSpeed = 0f;
            return;
        }

        float dt = Time.deltaTime;

        if (isGrounded)
        {
            // Stick to ground: small downward bias keeps CharacterController grounded
            if (verticalVelocity <= 0f)
                verticalVelocity = -stickToGroundForce;

            // Jump
            if (jumpRequested)
            {
                verticalVelocity = jumpSpeed;
                animator.SetTrigger(jumpHash);
                isGrounded = false;
                forcedFall = false; // clear forced fall on jump
            }
        }
        else
        {
            // Airborne: accumulate gravity (with extra while falling)
            float g = gravity;
            if (verticalVelocity <= 0f)
            {
                g *= fallGravityMultiplier;
                lastAirborneDownSpeed = Mathf.Min(lastAirborneDownSpeed, verticalVelocity);
            }
            verticalVelocity -= g * dt;
        }
    }

    private void GroundCheck()
    {
        // Start the ground check slightly below controller center
        Vector3 origin = transform.position + controller.center;
        Vector3 down = Vector3.down;
        float castDistance = (controller.height * 0.5f) - controller.radius + groundCheckOffset + stepDownDistance;

        bool hitFound = Physics.SphereCast(origin, groundCheckRadius, down, out RaycastHit hit, castDistance, groundLayers, QueryTriggerInteraction.Ignore);

        if (hitFound)
        {
            groundNormal = hit.normal;

            // Ledge probe ahead: if no ground found ahead, force falling
            bool groundAhead = ProbeGroundAhead();
            forcedFall = !groundAhead && moveValue > 0f; // moving off a ledge

            // Accept grounded if slope not too steep and not forced to fall
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            bool slopeOk = slopeAngle <= controller.slopeLimit + 0.1f;

            isGrounded = slopeOk && !forcedFall;
        }
        else
        {
            groundNormal = Vector3.up;
            isGrounded = false;
            forcedFall = true;
        }
    }

    private bool ProbeGroundAhead()
    {
        // Start from a point in front of feet at specified height
        Vector3 feet = transform.position + Vector3.up * controller.stepOffset;
        Vector3 start = feet + transform.forward * ledgeProbeForward + Vector3.up * ledgeProbeHeight;
        float downDistance = ledgeProbeHeight + ledgeProbeDown;

        bool ok = Physics.SphereCast(start, groundCheckRadius * 0.8f, Vector3.down, out RaycastHit hit, downDistance, groundLayers, QueryTriggerInteraction.Ignore);

        if (!ok) return false;

        // Check slope
        float angle = Vector3.Angle(hit.normal, Vector3.up);
        return angle <= controller.slopeLimit + 0.1f;
    }

    private void HandleLandingTriggers()
    {
        // Landing occurs when transitioning from airborne to grounded and not in action
        if (!wasGrounded && isGrounded && !inActionTaggedState)
        {
            // Prevent spamming
            if (Time.time < lastLandingTime + landingCooldown) return;

            float impactSpeed = -lastAirborneDownSpeed; // positive m/s
            lastAirborneDownSpeed = 0f;
            lastLandingTime = Time.time;

            if (impactSpeed >= damageLandingSpeed)
            {
                animator.SetTrigger(landDamageHash);
            }
            else if (impactSpeed >= hardLandingSpeed)
            {
                animator.SetTrigger(landHardHash);
            }
            else
            {
                animator.SetTrigger(landHash);
            }
        }
    }

    private void TryDetectAndTriggerAction()
    {
        // Require some forward intent
        if (moveValue <= minMoveForActions) return;
        if (!ProbesAssigned()) return;

        bool headHit = SphereProbe(headProbe, out _);
        bool waistHit = SphereProbe(waistProbe, out _);
        bool feetHit = SphereProbe(feetProbe, out _);

        // Combination logic:
        // - Waist + Feet (no Head) => Climb1
        // - Waist only => Vault
        // - Head + Waist + Feet => Climb2
        // Priority: Climb2 > Climb1 > Vault
        if (headHit && waistHit && feetHit)
        {
            animator.ResetTrigger(vaultHash);
            animator.ResetTrigger(climb1Hash);
            animator.SetTrigger(climb2Hash);
            lastActionTime = Time.time;
        }
        else if (!headHit && waistHit && feetHit)
        {
            animator.ResetTrigger(vaultHash);
            animator.SetTrigger(climb1Hash);
            lastActionTime = Time.time;
        }
        else if (!headHit && waistHit && !feetHit)
        {
            animator.SetTrigger(vaultHash);
            lastActionTime = Time.time;
        }
    }

    private bool SphereProbe(Transform originT, out RaycastHit hit)
    {
        Vector3 origin = originT.position;
        Vector3 dir = originT.forward;
        float distance = actionProbeDistance;

        // Perform a SphereCast to detect forward obstacle
        bool ok = Physics.SphereCast(origin, actionProbeRadius, dir, out hit, distance, actionLayers, QueryTriggerInteraction.Ignore);

        if (!ok) return false;

        // Filter out mostly-horizontal surfaces (we want walls/ledges), keep if surface is not too flat
        // i.e., ignore if normal is almost up
        float upDot = Vector3.Dot(hit.normal, Vector3.up);
        return upDot < 0.7f; // ~ <45 degrees from vertical wall
    }

    private bool ProbesAssigned()
    {
        if (headProbe == null || waistProbe == null || feetProbe == null)
        {
            Debug.LogWarning($"[RootMotionPlatformerController] Action probe transforms are not fully assigned.", this);
            return false;
        }
        return true;
    }

    private void OnAnimatorMove()
    {
        // Apply root motion horizontally, and optionally vertically when in Action animations.
        Vector3 rootDelta = animator.deltaPosition;
        Quaternion rootRotation = animator.deltaRotation;

        // Split components
        Vector3 horizontal = new Vector3(rootDelta.x, 0f, rootDelta.z);
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // Project horizontal onto ground when grounded and not forced falling and not in action
        if (isGrounded && !forcedFall && !inActionTaggedState)
        {
            horizontal = Vector3.ProjectOnPlane(horizontal, groundNormal);
        }

        // Vertical motion
        float verticalDelta = 0f;
        if (inActionTaggedState)
        {
            // Use animation's vertical movement (enable animated climbs/vaults)
            verticalDelta = rootDelta.y;
            verticalVelocity = 0f; // gravity disabled
        }
        else
        {
            // Physics-driven vertical
            verticalDelta = verticalVelocity * dt;
        }

        Vector3 motion = horizontal + Vector3.up * verticalDelta;

        // Move controller
        controller.Move(motion);

        // Apply root rotation (yaw) produced by animation
        transform.rotation = rootRotation * transform.rotation;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        // Ground check gizmo
        if (controller != null)
        {
            Gizmos.color = groundGizmoColor;
            Vector3 origin = transform.position + controller.center;
            float castDistance = (controller.height * 0.5f) - controller.radius + groundCheckOffset + stepDownDistance;
            Gizmos.DrawWireSphere(origin + Vector3.down * castDistance, groundCheckRadius);

            // Ledge probe
            Gizmos.color = ledgeGizmoColor;
            Vector3 feet = transform.position + Vector3.up * controller.stepOffset;
            Vector3 start = feet + transform.forward * ledgeProbeForward + Vector3.up * ledgeProbeHeight;
            Gizmos.DrawWireSphere(start, groundCheckRadius * 0.8f);
            Gizmos.DrawLine(start, start + Vector3.down * (ledgeProbeHeight + ledgeProbeDown));
        }

        // Action probes
        Gizmos.color = actionGizmoColor;
        if (headProbe != null)
        {
            Gizmos.DrawWireSphere(headProbe.position, actionProbeRadius);
            Gizmos.DrawLine(headProbe.position, headProbe.position + headProbe.forward * actionProbeDistance);
        }
        if (waistProbe != null)
        {
            Gizmos.DrawWireSphere(waistProbe.position, actionProbeRadius);
            Gizmos.DrawLine(waistProbe.position, waistProbe.position + waistProbe.forward * actionProbeDistance);
        }
        if (feetProbe != null)
        {
            Gizmos.DrawWireSphere(feetProbe.position, actionProbeRadius);
            Gizmos.DrawLine(feetProbe.position, feetProbe.position + feetProbe.forward * actionProbeDistance);
        }

        // Velocity gizmo
        Gizmos.color = velocityGizmoColor;
        Vector3 vel = new Vector3(0f, verticalVelocity, 0f);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, vel * 0.1f);
    }

    // Safety: prevent hang on physics pause/resume
    private void OnDisable()
    {
        verticalVelocity = 0f;
        inActionTaggedState = false;
    }
}