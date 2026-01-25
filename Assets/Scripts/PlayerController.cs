using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [HideInInspector] public Animator animator;
    private Rigidbody rb;

    [Header("PickUp Area Reference")]
    public Collider pickUpAreaCollider; // drag collider PickUpArea (PickUpTrigger) sini

    [Header("Movement Settings")]
    public float acceleration = 60.0f;
    public float deceleration = 30.0f;
    public float maximumWalkVelocity = 6.5f;
    public float maximumRunVelocity = 12.0f;
    public float rotationSpeed = 10.0f;
    public float layerBlendSpeed = 6.0f;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference runAction;
    public InputActionReference throwAction;   // PASS
    public InputActionReference attackAction;  // ATTACK/SHOOT (forward)

    [Header("Object Handling")]
    public ThrowableObject heldObject;
    public Transform rightHand;

    [Tooltip("Arc height (higher = more curve)")]
    public float throwHeight = 2.0f;      // dulu 1f, naik sikit untuk arc cantik

    [Tooltip("Overall throw speed multiplier (applies to pass & throw)")]
    public float objectThrowSpeed = 1.0f; // ✅ speed baru

    [Header("Arc Tuning (Distance Based)")]
    public float arcMinHeight = 1.2f;   // dekat
    public float arcMaxHeight = 2.6f;   // jauh (limit supaya tak melambung)
    public float arcMaxDistance = 30f;  // jarak yang dianggap "jauh" untuk cap
    public AnimationCurve arcByDistance = AnimationCurve.EaseInOut(0, 0, 1, 1);



    [Header("Passing System")]
    public Transform passTarget;
    public Transform passTargetCatchPoint;

    [Header("Pass Aim Rules")]
    public float passMaxAngle = 35f;
    public float passMaxDistance = 50f;
    public bool requireLineOfSight = false;
    public LayerMask lineOfSightMask = ~0;

    [Header("Pass Feel (Tap vs Hold)")]
    public float tapThreshold = 0.15f;
    public float fullChargeTime = 0.9f;

    public float tapPower = 0.35f;
    public float minHoldPower = 0.35f;
    public float maxHoldPower = 1.5f;

    public float slowPassTime = 0.85f;
    public float fastPassTime = 0.45f;

    public AnimationCurve chargeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Pass Distance (Tap vs Hold)")]
    public float tapMaxPassDistance = 10f;   // TAP: pendek sahaja
    public float holdMaxPassDistance = 28f;  // HOLD: panjang (<= passMaxDistance)

    [Header("Attack Throw Feel")]
    public float attackTapPower = 0.55f;
    public float attackMinHoldPower = 0.55f;
    public float attackMaxHoldPower = 1.8f;

    public float attackSlowTime = 0.70f;
    public float attackFastTime = 0.40f;

    public float attackForwardHeight = 1.2f;
    public float attackForwardMinDist = 10f;
    public float attackForwardMaxDist = 22f;

    // PASS charge state
    private float chargeStartTime = 0f;
    private float finalPassPower = 1f;
    private float finalPassTime = 0.8f;
    private float finalPassCharge01 = 0f;   // penting untuk jarak tap vs hold
    private float currentPassPower = 0f;
    private bool isChargingPass = false;
    private bool passPowerLocked = false;

    // ATTACK charge state
    private bool isChargingAttack = false;
    private bool attackPowerLocked = false;
    private float attackChargeStartTime = 0f;
    private float finalAttackPower = 1f;
    private float finalAttackTime = 0.6f;
    private float finalAttackCharge01 = 0f;
    private bool doAttackThrow = false;

    [HideInInspector] public bool canPickUp = false;
    [HideInInspector] public bool recentlyThrew = false;
    [HideInInspector] public ThrowableObject objectToAttach;
    [HideInInspector] public bool isPickUpInProgress = false;
    [HideInInspector] public bool isCatchingUpperInProgress = false;
    [HideInInspector] public bool isCatchingLowerInProgress = false;
    [HideInInspector] public bool catchFreeze = false;

    private bool canProcessThrow = false;
    private bool isTurningToPassTarget = false;

    private int SpeedHash;
    private int IsThrowingHash;
    private int IsThrowingFullHash;
    private int IsTakeObjectHash;
    private int IsTakeObjectFullHash;
    private int IsCatchingUpperHash;
    private int IsCatchingLowerHash;
    private int IsReadyToCatchHash;

    private bool isThrowingFullBody = false;
    private bool isTakingFullBody = false;

    private float throwLayerWeight = 0f;
    private float takeLayerWeight = 0f;
    private float catchLayerWeight = 0f;
    private int throwLayerIndex = -1;
    private int takeLayerIndex = -1;
    private int catchLayerIndex = -1;

    private bool isControlActive = true;

    private bool canThrow => heldObject != null && canProcessThrow && !isTurningToPassTarget;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        heldObject = null;
        objectToAttach = null;

        SpeedHash = Animator.StringToHash("Speed");
        IsThrowingHash = Animator.StringToHash("isThrowing");
        IsThrowingFullHash = Animator.StringToHash("isThrowingFull");
        IsTakeObjectHash = Animator.StringToHash("isTakeObject");
        IsTakeObjectFullHash = Animator.StringToHash("isTakeObjectFull");
        IsCatchingUpperHash = Animator.StringToHash("isCatchingUpper");
        IsCatchingLowerHash = Animator.StringToHash("isCatchingLower");
        IsReadyToCatchHash = Animator.StringToHash("isReadyToCatch");

        throwLayerIndex = animator.GetLayerIndex("ThrowLayer");
        takeLayerIndex = animator.GetLayerIndex("TakeObjectLayer");
        catchLayerIndex = animator.GetLayerIndex("CatchLayer");

        if (throwLayerIndex != -1) animator.SetLayerWeight(throwLayerIndex, 0f);
        if (takeLayerIndex != -1) animator.SetLayerWeight(takeLayerIndex, 0f);
        if (catchLayerIndex != -1) animator.SetLayerWeight(catchLayerIndex, 0f);

        StartCoroutine(EnableThrowInput());

        // Register to manager
        PlayerControlManager.Instance?.Register(this);
    }

    void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (runAction != null) runAction.action.Enable();
        if (throwAction != null) throwAction.action.Enable();
        if (attackAction != null) attackAction.action.Enable();
    }

    void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (runAction != null) runAction.action.Disable();
        if (throwAction != null) throwAction.action.Disable();
        if (attackAction != null) attackAction.action.Disable();
    }

    // ===== Manager hooks =====
    public void SetControlActive(bool active)
    {
        isControlActive = active;

        if (!active)
        {
            animator.SetFloat(SpeedHash, 0f);
        }
    }

    public void SetPickUpAreaEnabled(bool enabled)
    {
        if (pickUpAreaCollider != null)
            pickUpAreaCollider.enabled = enabled;
    }

    void Update()
    {
        // Untuk inactive player, input kosong (supaya tak trigger pass/attack)
        Vector2 moveInput = isControlActive && moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        bool runPressed = isControlActive && runAction != null && runAction.action.IsPressed();

        bool passHeld = isControlActive && throwAction != null && throwAction.action.IsPressed();
        bool passReleased = isControlActive && throwAction != null && throwAction.action.WasReleasedThisFrame();

        bool attackHeld = isControlActive && attackAction != null && attackAction.action.IsPressed();
        bool attackReleased = isControlActive && attackAction != null && attackAction.action.WasReleasedThisFrame();

        float currentMaxVelocity = runPressed ? maximumRunVelocity : maximumWalkVelocity;

        AnimatorStateInfo baseState = animator.GetCurrentAnimatorStateInfo(0);
        isThrowingFullBody = baseState.IsName("ThrowFullBody") && baseState.normalizedTime < 0.95f;
        isTakingFullBody = baseState.IsName("TakeObjectFull") && baseState.normalizedTime < 0.95f;

        // Speed
        float targetSpeed = moveInput.magnitude * currentMaxVelocity;
        float currentSpeed = animator.GetFloat(SpeedHash);

        if (!isThrowingFullBody && !isTakingFullBody)
        {
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                (Mathf.Abs(targetSpeed) > currentSpeed ? acceleration : deceleration) * Time.deltaTime
            );
            animator.SetFloat(SpeedHash, currentSpeed);
        }
        else
        {
            animator.SetFloat(SpeedHash, 0f);
        }

        // Rotation (lower body) hanya bila active
        if (isControlActive && moveInput != Vector2.zero && !isThrowingFullBody && !isTakingFullBody)
        {
            Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                Time.deltaTime * rotationSpeed
            );
        }

        // =====================
        // PASS POWER (Tap vs Hold)
        // =====================
        if (passHeld && heldObject != null && canProcessThrow && !passPowerLocked && !attackPowerLocked)
        {
            if (!isChargingPass)
            {
                isChargingPass = true;
                chargeStartTime = Time.time;
                currentPassPower = 0f;
            }

            float heldTime = Time.time - chargeStartTime;
            float t = Mathf.Clamp01(heldTime / fullChargeTime);
            float curved = chargeCurve.Evaluate(t);

            currentPassPower = Mathf.Lerp(minHoldPower, maxHoldPower, curved);
        }

        if (passReleased && isChargingPass)
        {
            isChargingPass = false;
            passPowerLocked = true;
            doAttackThrow = false;

            float heldTime = Time.time - chargeStartTime;

            if (heldTime <= tapThreshold)
            {
                finalPassPower = tapPower;
                finalPassTime = slowPassTime;
                finalPassCharge01 = 0f; // TAP short
            }
            else
            {
                float t = Mathf.Clamp01(heldTime / fullChargeTime);
                float curved = chargeCurve.Evaluate(t);

                finalPassPower = Mathf.Lerp(minHoldPower, maxHoldPower, curved);
                finalPassTime = Mathf.Lerp(slowPassTime, fastPassTime, curved);
                finalPassCharge01 = curved; // HOLD amount
            }

            isTurningToPassTarget = false;
            currentPassPower = finalPassPower;

            // Trigger anim throw
            if (moveInput != Vector2.zero)
                animator.SetBool(IsThrowingHash, true);
            else
                animator.SetBool(IsThrowingFullHash, true);
        }

        // =====================
        // ATTACK POWER (Tap vs Hold) - ALWAYS FORWARD
        // =====================
        if (attackHeld && heldObject != null && canProcessThrow && !attackPowerLocked && !passPowerLocked)
        {
            if (!isChargingAttack)
            {
                isChargingAttack = true;
                attackChargeStartTime = Time.time;
            }
        }

        if (attackReleased && isChargingAttack)
        {
            isChargingAttack = false;
            attackPowerLocked = true;
            doAttackThrow = true;

            float heldTime = Time.time - attackChargeStartTime;

            if (heldTime <= tapThreshold)
            {
                finalAttackPower = attackTapPower;
                finalAttackTime = attackSlowTime;
                finalAttackCharge01 = 0f;
            }
            else
            {
                float t = Mathf.Clamp01(heldTime / fullChargeTime);
                float curved = chargeCurve.Evaluate(t);

                finalAttackPower = Mathf.Lerp(attackMinHoldPower, attackMaxHoldPower, curved);
                finalAttackTime = Mathf.Lerp(attackSlowTime, attackFastTime, curved);
                finalAttackCharge01 = curved;
            }

            // Trigger anim throw
            if (moveInput != Vector2.zero)
                animator.SetBool(IsThrowingHash, true);
            else
                animator.SetBool(IsThrowingFullHash, true);
        }

        // AUTO PICK-UP (tak ganggu sebab PickUpArea hanya 1 player ON)
        if (objectToAttach != null && objectToAttach.CanBePickedUpBy(this) &&
            !isCatchingUpperInProgress && !isCatchingLowerInProgress)
        {
            objectToAttach.Reserve(this);
            canPickUp = false;
            TriggerPickUpAnimation();
        }

        // ===== LAYER BLENDING =====
        if (throwLayerIndex != -1) UpdateLayerWeight(IsThrowingHash, ref throwLayerWeight, throwLayerIndex);
        if (takeLayerIndex != -1) UpdateLayerWeight(IsTakeObjectHash, ref takeLayerWeight, takeLayerIndex);
        if (catchLayerIndex != -1) UpdateCatchLayerWeight(ref catchLayerWeight, catchLayerIndex);

        // RESET STATE
        if (throwLayerIndex != -1) ResetAnimationState(throwLayerIndex, "Throw_Run", IsThrowingHash);
        ResetAnimationState(0, "ThrowFullBody", IsThrowingFullHash);
        if (takeLayerIndex != -1) ResetAnimationState(takeLayerIndex, "Take_Object", IsTakeObjectHash);
        ResetAnimationState(0, "TakeObjectFull", IsTakeObjectFullHash);
    }

    void FixedUpdate()
    {
        if (!isControlActive) return;
        if (isThrowingFullBody || isTakingFullBody) return;

        Vector2 moveInput = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        float currentSpeed = animator.GetFloat(SpeedHash);

        if (moveInput != Vector2.zero)
        {
            Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            rb.MovePosition(rb.position + direction * currentSpeed * Time.fixedDeltaTime);
        }
    }

    private IEnumerator EnableThrowInput()
    {
        yield return new WaitForSeconds(0.1f);
        canProcessThrow = true;
    }

    private void UpdateLayerWeight(int boolHash, ref float layerWeight, int layerIndex)
    {
        float target = animator.GetBool(boolHash) ? 1f : 0f;
        layerWeight = Mathf.MoveTowards(layerWeight, target, Time.deltaTime * layerBlendSpeed);
        animator.SetLayerWeight(layerIndex, layerWeight);
    }

    private void UpdateCatchLayerWeight(ref float layerWeight, int layerIndex)
    {
        bool ready = animator.GetBool(IsReadyToCatchHash);
        bool catching = isCatchingUpperInProgress || isCatchingLowerInProgress;

        float target = (ready || catching) ? 1f : 0f;
        layerWeight = Mathf.MoveTowards(layerWeight, target, Time.deltaTime * layerBlendSpeed);
        animator.SetLayerWeight(layerIndex, layerWeight);
    }

    private void ResetAnimationState(int layerIndex, string stateName, int boolHash)
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (!state.IsName(stateName) || state.normalizedTime < 0.95f) return;

        animator.SetBool(boolHash, false);

        if ((boolHash == IsTakeObjectHash || boolHash == IsTakeObjectFullHash))
            isPickUpInProgress = false;
    }

    public void TriggerPickUpAnimation()
    {
        if (isPickUpInProgress || isCatchingUpperInProgress || isCatchingLowerInProgress) return;

        isPickUpInProgress = true;

        if (animator.GetFloat(SpeedHash) > 0.1f)
            animator.SetBool(IsTakeObjectHash, true);
        else
            animator.SetBool(IsTakeObjectFullHash, true);
    }

    public void AttachObjectToHand(ThrowableObject obj)
    {
        if (obj == null || rightHand == null) return;

        heldObject = obj;
        obj.ClearReservation();
        objectToAttach = null;

        obj.OnPickedUp(rightHand, this);

        // ✅ holder berubah → update PickUpArea rules
        PlayerControlManager.Instance?.RefreshPickupAreas();
    }

    public void AttachNearbyObjectEvent()
    {
        if (objectToAttach == null) return;
        AttachObjectToHand(objectToAttach);
    }

    // === CATCH FUNCTIONS ===
    public void CatchUpperObject(ThrowableObject obj)
    {
        if (obj == null || isCatchingUpperInProgress) return;

        animator.SetBool(IsReadyToCatchHash, false);

        obj.StopMotion();
        objectToAttach = obj;
        isCatchingUpperInProgress = true;

        TriggerCatchUpper();
    }

    public void CatchLowerObject(ThrowableObject obj)
    {
        if (obj == null || isCatchingLowerInProgress) return;

        animator.SetBool(IsReadyToCatchHash, false);

        obj.StopMotion();
        objectToAttach = obj;
        isCatchingLowerInProgress = true;

        TriggerCatchLower();
    }

    public void TriggerCatchUpper()
    {
        animator.ResetTrigger(IsCatchingLowerHash);
        animator.SetTrigger(IsCatchingUpperHash);
    }

    public void TriggerCatchLower()
    {
        animator.ResetTrigger(IsCatchingUpperHash);
        animator.SetTrigger(IsCatchingLowerHash);
    }

    public void SetReadyToCatch(bool ready)
    {
        if (isPickUpInProgress || isCatchingUpperInProgress || isCatchingLowerInProgress || isThrowingFullBody || isTakingFullBody)
            ready = false;

        animator.SetBool(IsReadyToCatchHash, ready);
    }

    private Coroutine readyTimeoutCo;

    public void StartReadyCatchTimeout(float seconds)
    {
        if (readyTimeoutCo != null) StopCoroutine(readyTimeoutCo);
        readyTimeoutCo = StartCoroutine(ReadyCatchTimeout(seconds));
    }

    private IEnumerator ReadyCatchTimeout(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (isCatchingUpperInProgress || isCatchingLowerInProgress || heldObject != null)
                yield break;

            t += Time.deltaTime;
            yield return null;
        }

        SetReadyToCatch(false);
    }

    public void EndCatchUpperEvent() => isCatchingUpperInProgress = false;
    public void EndCatchLowerEvent() => isCatchingLowerInProgress = false;

    // Animation Event: release ball/slipper here
    public void ReleaseHeldObjectEvent()
    {
        if (heldObject == null) return;

        bool isAttack = doAttackThrow && attackPowerLocked;

        // PASS "short vs long" distance gating
        bool doPass = false;
        if (!isAttack && passTarget != null && CanPassToTarget(passTarget))
        {
            float distToTarget = Vector3.Distance(transform.position, passTarget.position);

            float allowedDist = Mathf.Lerp(
                tapMaxPassDistance,
                Mathf.Min(holdMaxPassDistance, passMaxDistance),
                finalPassCharge01
            );

            if (distToTarget <= allowedDist)
                doPass = true;
        }

        Vector3 targetPoint;
        PlayerController teammate = null;

        float powerToUse;

        if (isAttack)
        {
            powerToUse = finalAttackPower;

            float dist = Mathf.Lerp(attackForwardMinDist, attackForwardMaxDist, finalAttackCharge01);

            // ✅ DESTINATION sahaja (arc akan naikkan)
            targetPoint = transform.position + transform.forward * dist;
        }
        else
        {
            powerToUse = finalPassPower;

            if (doPass)
            {
                targetPoint = passTargetCatchPoint != null
                    ? passTargetCatchPoint.position
                    : passTarget.position + Vector3.up * 1.2f; // fallback ringan

                teammate = passTarget.GetComponent<PlayerController>();
                if (teammate != null)
                {
                    teammate.SetReadyToCatch(true);
                    teammate.StartReadyCatchTimeout(1f);

                    // ✅ SWITCH CONTROL NOW (bola masih terbang)
                    PlayerControlManager.Instance?.SwitchTo(teammate);
                }
            }
            else
            {
                // ✅ forward fallback bila tak boleh pass (ikut jarak ke passTarget kalau ada)
                float forwardDist;

                if (passTarget != null)
                {
                    Vector3 toT = passTarget.position - transform.position;
                    toT.y = 0f;
                    forwardDist = Mathf.Clamp(toT.magnitude, 6f, passMaxDistance);
                }
                else
                {
                    forwardDist = Mathf.Clamp(passMaxDistance * 0.4f, 6f, passMaxDistance);
                }

                targetPoint = transform.position + transform.forward * forwardDist;
            }
        }

        // ✅ ARC THROW (apply untuk PASS & ATTACK)
        Vector3 velocity =
            CalculateThrowVelocityArc(heldObject.transform.position, targetPoint, throwHeight)
            * powerToUse
            * objectThrowSpeed;

        heldObject.OnThrown(velocity, doPass ? teammate : null);

        // ✅ ownership berubah → refresh pickup areas
        PlayerControlManager.Instance?.RefreshPickupAreas();

        heldObject = null;
        canPickUp = false;

        animator.SetBool(IsThrowingHash, false);
        animator.SetBool(IsThrowingFullHash, false);

        canProcessThrow = false;
        StartCoroutine(EnableThrowInput());
        StartCoroutine(ThrowCooldown());

        // reset PASS
        finalPassPower = 1f;
        finalPassTime = 0.8f;     // (kalau kau nak, boleh buang variable ni terus)
        finalPassCharge01 = 0f;
        passPowerLocked = false;
        isChargingPass = false;

        // reset ATTACK
        finalAttackPower = 1f;
        finalAttackTime = 0.6f;   // (kalau kau nak, boleh buang variable ni terus)
        finalAttackCharge01 = 0f;
        attackPowerLocked = false;
        isChargingAttack = false;
        doAttackThrow = false;

        // ✅ confirm refresh selepas clear
        PlayerControlManager.Instance?.RefreshPickupAreas();
    }


    private IEnumerator ThrowCooldown()
    {
        recentlyThrew = true;
        yield return new WaitForSeconds(0.3f);
        recentlyThrew = false;
    }

    private Vector3 CalculateThrowVelocityArc(Vector3 origin, Vector3 target, float baseHeight)
    {
        float g = Physics.gravity.y; // negative

        // distance on XZ
        Vector3 toTargetXZ = target - origin;
        toTargetXZ.y = 0f;
        float dist = toTargetXZ.magnitude;

        // ✅ arc height ikut jarak (clamped)
        float d01 = Mathf.Clamp01(dist / Mathf.Max(0.01f, arcMaxDistance));
        float curved = arcByDistance != null ? arcByDistance.Evaluate(d01) : d01;

        // baseHeight (throwHeight) still acts like "overall arc feel"
        float arcHeight = Mathf.Lerp(arcMinHeight, arcMaxHeight, curved) * Mathf.Max(0.01f, baseHeight);

        // ✅ Pick an apex above the higher of origin/target
        float apexY = Mathf.Max(origin.y, target.y) + Mathf.Max(0.01f, arcHeight);

        float timeUp = Mathf.Sqrt(2f * (apexY - origin.y) / -g);
        float timeDown = Mathf.Sqrt(2f * (apexY - target.y) / -g);
        float totalTime = timeUp + timeDown;

        Vector3 velocityXZ = toTargetXZ / Mathf.Max(0.01f, totalTime);
        float velocityY = -g * timeUp;

        return velocityXZ + Vector3.up * velocityY;
    }



    private bool CanPassToTarget(Transform target)
    {
        if (target == null) return false;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;
        if (dist < 0.01f || dist > passMaxDistance) return false;

        Vector3 dir = toTarget / dist;
        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > passMaxAngle) return false;

        if (requireLineOfSight)
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Vector3 dest = target.position + Vector3.up * 1.2f;

            if (Physics.Linecast(origin, dest, out RaycastHit hit, lineOfSightMask))
            {
                if (hit.transform != target && !hit.transform.IsChildOf(target))
                    return false;
            }
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        // elak error bila belum start
        if (!enabled) return;

        Gizmos.color = Color.cyan;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Vector3 forward = transform.forward;

        // Garis tengah (arah player)
        Gizmos.DrawLine(origin, origin + forward * passMaxDistance);

        // Kira arah kiri & kanan berdasarkan passMaxAngle
        Quaternion leftRot = Quaternion.AngleAxis(-passMaxAngle, Vector3.up);
        Quaternion rightRot = Quaternion.AngleAxis(passMaxAngle, Vector3.up);

        Vector3 leftDir = leftRot * forward;
        Vector3 rightDir = rightRot * forward;

        // Garis cone kiri & kanan
        Gizmos.DrawLine(origin, origin + leftDir * passMaxDistance);
        Gizmos.DrawLine(origin, origin + rightDir * passMaxDistance);

        // Optional: lukis arc (nampak lebih jelas)
        int segments = 20;
        Vector3 prevPoint = origin + leftDir * passMaxDistance;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(-passMaxAngle, passMaxAngle, t);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 point = origin + dir * passMaxDistance;

            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }
}
