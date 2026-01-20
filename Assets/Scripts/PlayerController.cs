using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [HideInInspector] public Animator animator;
    private Rigidbody rb;

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
    public InputActionReference throwAction;

    [Header("Object Handling")]
    public ThrowableObject heldObject;
    public Transform rightHand;
    public float throwTime = 0.8f;
    public float throwDistance = 5f;
    public float throwHeight = 1f;

    [Header("Passing System")]
    public Transform passTarget;
    public Transform passTargetCatchPoint;

    [Header("Pass Power (DEBUG)")]
    public float maxPassPower = 1.5f;
    public float minPassPower = 0.2f;
    public float passChargeSpeed = 1f;

    [Header("Pass Aim Rules")]
    public float passMaxAngle = 35f;      // cone aim (contoh sukan: 25-45 deg)
    public float passMaxDistance = 50f;   // jarak max pass
    public bool requireLineOfSight = false;
    public LayerMask lineOfSightMask = ~0; // optional

    [Header("Pass Feel (Tap vs Hold)")]
    public float tapThreshold = 0.15f;        // bawah ni kira "tap"
    public float fullChargeTime = 0.9f;       // hold sampai max

    public float tapPower = 0.35f;            // kuasa bila tap
    public float minHoldPower = 0.35f;        // kuasa minimum bila hold (biasanya sama tap)
    public float maxHoldPower = 1.5f;         // kuasa max

    public float slowPassTime = 0.85f;        // tap: masa sampai target (slow tapi logik)
    public float fastPassTime = 0.45f;        // hold: masa sampai target (laju)

    public AnimationCurve chargeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private float chargeStartTime = 0f;
    private float finalPassPower = 1f;
    private float finalPassTime = 0.8f;

    private float currentPassPower = 0f;
    private bool isChargingPass = false;
    private bool passPowerLocked = false;

    [HideInInspector] public bool canPickUp = false;
    [HideInInspector] public bool recentlyThrew = false;
    [HideInInspector] public ThrowableObject objectToAttach;
    [HideInInspector] public bool isPickUpInProgress = false;
    [HideInInspector] public bool isCatchingUpperInProgress = false;
    [HideInInspector] public bool isCatchingLowerInProgress = false;
    [HideInInspector] public bool catchFreeze = false;

    private bool canProcessThrow = false;
    private bool isTurningToPassTarget = false;
    private float passTurnSpeed = 12f;

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
    private int throwLayerIndex;
    private int takeLayerIndex;
    private int catchLayerIndex;

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

        animator.SetLayerWeight(throwLayerIndex, 0f);
        animator.SetLayerWeight(takeLayerIndex, 0f);
        animator.SetLayerWeight(catchLayerIndex, 0f);

        StartCoroutine(EnableThrowInput());
    }

    void OnEnable()
    {
        moveAction.action.Enable();
        runAction.action.Enable();
        throwAction.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        runAction.action.Disable();
        throwAction.action.Disable();
    }

    void Update()
    {
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        bool runPressed = runAction.action.IsPressed();
        bool throwHeld = throwAction.action.IsPressed();
        bool throwReleased = throwAction.action.WasReleasedThisFrame();

        float currentMaxVelocity = runPressed ? maximumRunVelocity : maximumWalkVelocity;

        AnimatorStateInfo baseState = animator.GetCurrentAnimatorStateInfo(0);
        isThrowingFullBody = baseState.IsName("ThrowFullBody") && baseState.normalizedTime < 0.95f;
        isTakingFullBody = baseState.IsName("TakeObjectFull") && baseState.normalizedTime < 0.95f;

        // Speed
        float targetSpeed = moveInput.magnitude * currentMaxVelocity;
        float currentSpeed = animator.GetFloat(SpeedHash);
        if (!isThrowingFullBody && !isTakingFullBody)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed,
                (Mathf.Abs(targetSpeed) > currentSpeed ? acceleration : deceleration) * Time.deltaTime);
            animator.SetFloat(SpeedHash, currentSpeed);
        }
        else animator.SetFloat(SpeedHash, 0f);

        // Rotation
        if (moveInput != Vector2.zero && !isThrowingFullBody && !isTakingFullBody)
        {
            Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * rotationSpeed);
        }

        // PASS POWER (Tap vs Hold)
        if (throwHeld && heldObject != null && canProcessThrow && !passPowerLocked)
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

            // Preview/charging value (optional)
            currentPassPower = Mathf.Lerp(minHoldPower, maxHoldPower, curved);
        }

        if (throwReleased && isChargingPass)
        {
            isChargingPass = false;
            passPowerLocked = true;

            float heldTime = Time.time - chargeStartTime;

            if (heldTime <= tapThreshold)
            {
                // ✅ TAP: slow, normal pass (tak terapung lama)
                finalPassPower = tapPower;
                finalPassTime = slowPassTime;
            }
            else
            {
                // ✅ HOLD: semakin lama, semakin laju & kuat
                float t = Mathf.Clamp01(heldTime / fullChargeTime);
                float curved = chargeCurve.Evaluate(t);

                finalPassPower = Mathf.Lerp(minHoldPower, maxHoldPower, curved);
                finalPassTime = Mathf.Lerp(slowPassTime, fastPassTime, curved);
            }

            // ❌ Jangan auto turn (kalau kau ikut aim cone)
            isTurningToPassTarget = false;
        }



        // THROW ANIMATION
        if (passPowerLocked && heldObject != null && canProcessThrow && !isTurningToPassTarget)
        {
            if (moveInput != Vector2.zero)
                animator.SetBool(IsThrowingHash, true);
            else
                animator.SetBool(IsThrowingFullHash, true);
        }

        // AUTO PICK-UP
        if (objectToAttach != null && objectToAttach.CanBePickedUpBy(this) &&
            !isCatchingUpperInProgress && !isCatchingLowerInProgress)
        {
            objectToAttach.Reserve(this);
            canPickUp = false;
            TriggerPickUpAnimation();
        }

        // LAYER BLENDING
        UpdateLayerWeight(IsThrowingHash, ref throwLayerWeight, throwLayerIndex);
        UpdateLayerWeight(IsTakeObjectHash, ref takeLayerWeight, takeLayerIndex);
        UpdateCatchLayerWeight(ref catchLayerWeight, catchLayerIndex);

        // RESET STATE
        ResetAnimationState(throwLayerIndex, "Throw_Run", IsThrowingHash);
        ResetAnimationState(0, "ThrowFullBody", IsThrowingFullHash);
        ResetAnimationState(takeLayerIndex, "Take_Object", IsTakeObjectHash);
        ResetAnimationState(0, "TakeObjectFull", IsTakeObjectFullHash);
    }

    void FixedUpdate()
    {
        if (isThrowingFullBody || isTakingFullBody) return;

        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        float currentSpeed = animator.GetFloat(SpeedHash);

        if (moveInput != Vector2.zero)
        {
            Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            rb.MovePosition(rb.position + direction * currentSpeed * Time.fixedDeltaTime);
        }
    }

    private void LateUpdate()
    {
        if (!isTurningToPassTarget || passTarget == null) return;

        Vector3 dir = passTarget.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * passTurnSpeed);

        float angle = Quaternion.Angle(transform.rotation, targetRot);
        if (angle < 2f)
        {
            isTurningToPassTarget = false;

            AnimatorStateInfo baseState = animator.GetCurrentAnimatorStateInfo(0);
            if (baseState.IsName("Idle") || baseState.IsName("Walk"))
            {
                if (rb.linearVelocity.magnitude > 0.1f)
                    animator.SetBool(IsThrowingHash, true);
                else
                    animator.SetBool(IsThrowingFullHash, true);
            }
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

        // CatchLayer aktif jika READY atau sedang CATCH
        float target = (ready || catching) ? 1f : 0f;

        // Blend
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

    public void ResetCatchAndPickLayers()
    {
        isCatchingUpperInProgress = false;
        isCatchingLowerInProgress = false;
        catchFreeze = false;

        animator.SetBool(IsReadyToCatchHash, false);

        animator.SetBool(IsTakeObjectHash, false);
        animator.SetBool(IsTakeObjectFullHash, false);
        takeLayerWeight = 0f;
        animator.SetLayerWeight(takeLayerIndex, 0f);

        throwLayerWeight = 0f;
        animator.SetLayerWeight(throwLayerIndex, 0f);
        catchLayerWeight = 0f;
        animator.SetLayerWeight(catchLayerIndex, 0f);
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

        obj.OnPickedUp(rightHand, this); // RULE EMAS: animation event attach
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

        // Matikan ready bila catch bermula
        animator.SetBool(IsReadyToCatchHash, false);

        obj.StopMotion();
        objectToAttach = obj;
        isCatchingUpperInProgress = true;

        StartCoroutine(RotateTowardsObject(obj.transform, true));
        TriggerCatchUpper();
    }

    public void CatchLowerObject(ThrowableObject obj)
    {
        if (obj == null || isCatchingLowerInProgress) return;

        // Matikan ready bila catch bermula
        animator.SetBool(IsReadyToCatchHash, false);

        obj.StopMotion();
        objectToAttach = obj;
        isCatchingLowerInProgress = true;

        StartCoroutine(RotateTowardsObject(obj.transform, false));
        TriggerCatchLower();
    }


    private IEnumerator RotateTowardsObject(Transform target, bool isUpper)
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) yield break;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        while (Quaternion.Angle(transform.rotation, targetRot) > 1f &&
              (isUpper ? isCatchingUpperInProgress : isCatchingLowerInProgress))
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            yield return null;
        }
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

    // === THROW FUNCTIONS ===
    public void ThrowHeldObject(Vector3 velocity, PlayerController target = null)
    {
        if (heldObject == null) return;

        heldObject.OnThrown(velocity, target);
        heldObject = null;
        objectToAttach = null;
    }

    public void ReleaseHeldObjectEvent()
    {
        if (heldObject == null) return;

        // ✅ Decide PASS vs THROW-FORWARD based on facing angle
        bool doPass = (passTarget != null) && CanPassToTarget(passTarget);

        Vector3 targetPoint;
        PlayerController teammate = null;

        if (doPass)
        {
            targetPoint = (passTargetCatchPoint != null)
                ? passTargetCatchPoint.position
                : passTarget.position + Vector3.up * 1.5f;

            teammate = passTarget.GetComponent<PlayerController>();

            // optional: bagi teammate masuk "ready catch" sekejap
            if (teammate != null)
            {
                teammate.SetReadyToCatch(true);
                teammate.StartReadyCatchTimeout(1.0f);
            }
        }
        else
        {
            // throw biasa ke depan
            targetPoint = transform.position
                + transform.forward * throwDistance
                + Vector3.up * throwHeight;
        }

        Vector3 throwVelocity = CalculateThrowVelocity(heldObject.transform.position, targetPoint, throwTime) * currentPassPower; 

        heldObject.OnThrown(throwVelocity, doPass ? teammate : null);

        heldObject = null;
        canPickUp = false;

        animator.SetBool(IsThrowingHash, false);
        animator.SetBool(IsThrowingFullHash, false);

        canProcessThrow = false;
        StartCoroutine(EnableThrowInput());
        StartCoroutine(ThrowCooldown());

        finalPassPower = 1f;
        finalPassTime = throwTime;   // fallback ke default
        currentPassPower = 0f;
        passPowerLocked = false;


    }


    private IEnumerator ThrowCooldown()
    {
        recentlyThrew = true;
        yield return new WaitForSeconds(0.3f);
        recentlyThrew = false;
    }

    private Vector3 CalculateThrowVelocity(Vector3 origin, Vector3 target, float timeToTarget)
    {
        Vector3 displacementXZ = new Vector3(target.x - origin.x, 0, target.z - origin.z);
        Vector3 velocityXZ = displacementXZ / timeToTarget;
        float velocityY = (target.y - origin.y) / timeToTarget - 0.5f * Physics.gravity.y * timeToTarget;
        return velocityXZ + Vector3.up * velocityY;
    }

    public void SetReadyToCatch(bool ready)
    {
        // Jangan tunjuk ready kalau sedang catch / pickup / anim full body
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
            // Kalau catch dah start / attach dah berlaku, stop timeout
            if (isCatchingUpperInProgress || isCatchingLowerInProgress || heldObject != null)
                yield break;

            t += Time.deltaTime;
            yield return null;
        }

        SetReadyToCatch(false);
    }

    public void EndCatchUpperEvent()
    {
        isCatchingUpperInProgress = false;

        // bila catch habis, biar layer turun semula (UpdateCatchLayerWeight akan handle)
    }

    public void EndCatchLowerEvent()
    {
        isCatchingLowerInProgress = false;
    }

    private bool CanPassToTarget(Transform target)
    {
        if (target == null) return false;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;
        if (dist < 0.01f || dist > passMaxDistance) return false;

        Vector3 dir = toTarget / dist; // normalized
        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > passMaxAngle) return false;

        if (requireLineOfSight)
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Vector3 dest = target.position + Vector3.up * 1.2f;
            if (Physics.Linecast(origin, dest, out RaycastHit hit, lineOfSightMask))
            {
                // kalau hit sesuatu sebelum target, tak boleh pass
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
