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
    public float layerBlendSpeed = 5.0f;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference runAction;
    public InputActionReference throwAction;

    [Header("Object Handling")]
    public ThrowableObject heldObject;
    public Transform rightHand;
    [Tooltip("Time for object to reach the target in seconds")]
    public float throwTime = 0.8f;
    [Tooltip("Distance ahead to throw the object")]
    public float throwDistance = 5f;
    [Tooltip("Height offset for throw arc")]
    public float throwHeight = 1f;

    [Header("Passing System")]
    public Transform passTarget; // teammate transform
    public Transform passTargetCatchPoint; // teammate catch point

    [Header("Pass Power (DEBUG)")]
    public float maxPassPower = 1.5f;
    public float minPassPower = 0.2f;
    public float passChargeSpeed = 1f;

    private float currentPassPower = 0f;
    private bool isChargingPass = false;
    private bool passPowerLocked = false;

    [HideInInspector] public bool canPickUp = false;
    [HideInInspector] public bool recentlyThrew = false;
    [HideInInspector] public ThrowableObject objectToAttach;
    [HideInInspector] public bool isPickUpInProgress = false;
    [HideInInspector] public bool isCatchingInProgress = false;
    [HideInInspector] public bool catchFreeze = false;

    // PROPERTY: CAN THROW
    private bool canThrow => heldObject != null && canProcessThrow && !isTurningToPassTarget;


    private bool canProcessThrow = false;
    private bool isTurningToPassTarget = false;
    private float passTurnSpeed = 12f;

    private int SpeedHash;
    private int IsThrowingHash;
    private int IsThrowingFullHash;
    private int IsTakeObjectHash;
    private int IsTakeObjectFullHash;
    private int IsCatchingHash;
    private int IsReadyToCatchHash;

    private bool isThrowingFullBody = false;
    private bool isTakingFullBody = false;
    public bool IsCatching => isCatchingInProgress;

    private float throwLayerWeight = 0f;
    private float takeLayerWeight = 0f;
    private float catchLayerWeight = 0f;
    private int throwLayerIndex;
    private int takeLayerIndex;
    private int catchLayerIndex;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        heldObject = null;
        objectToAttach = null;
        canPickUp = false;
        isChargingPass = false;
        passPowerLocked = false;
        isTurningToPassTarget = false;

        // Animator hashes
        SpeedHash = Animator.StringToHash("Speed");
        IsThrowingHash = Animator.StringToHash("isThrowing");
        IsThrowingFullHash = Animator.StringToHash("isThrowingFull");
        IsTakeObjectHash = Animator.StringToHash("isTakeObject");
        IsTakeObjectFullHash = Animator.StringToHash("isTakeObjectFull");
        IsCatchingHash = Animator.StringToHash("isCatching");
        IsReadyToCatchHash = Animator.StringToHash("isReadyToCatch");

        // Layer indices
        throwLayerIndex = animator.GetLayerIndex("ThrowLayer");
        takeLayerIndex = animator.GetLayerIndex("TakeObjectLayer");
        catchLayerIndex = animator.GetLayerIndex("CatchLayer");

        // Reset weights
        animator.SetLayerWeight(throwLayerIndex, 0f);
        animator.SetLayerWeight(takeLayerIndex, 0f);
        animator.SetLayerWeight(catchLayerIndex, 0f);

        // Delay throw input for 0.1s to prevent phantom throw
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

        // Speed blending
        float targetSpeed = moveInput.magnitude * currentMaxVelocity;
        float currentSpeed = animator.GetFloat(SpeedHash);
        if (!isThrowingFullBody && !isTakingFullBody)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, (Mathf.Abs(targetSpeed) > currentSpeed ? acceleration : deceleration) * Time.deltaTime);
            animator.SetFloat(SpeedHash, currentSpeed);
        }
        else animator.SetFloat(SpeedHash, 0f);

        // Rotation
        if (moveInput != Vector2.zero && !isThrowingFullBody && !isTakingFullBody)
        {
            Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * rotationSpeed);
        }

        // === PASS POWER CHARGE (DEBUG) ===
        if (throwHeld && heldObject != null && canProcessThrow && !passPowerLocked)
        {
            isChargingPass = true;
            currentPassPower += Time.deltaTime * passChargeSpeed;
            currentPassPower = Mathf.Clamp(currentPassPower, minPassPower, maxPassPower);

            Debug.Log($"[PASS CHARGING] Power: {currentPassPower:F2}");
        }

        if (throwReleased && isChargingPass)
        {
            isChargingPass = false;
            passPowerLocked = true;

            Debug.Log($"[PASS LOCKED] Final Power: {currentPassPower:F2}");

            if (passTarget != null)
                isTurningToPassTarget = true;
        }

        // === THROW / PASS ANIMATION ===
        if (passPowerLocked && heldObject != null && canProcessThrow && !isTurningToPassTarget)
        {
            if (moveInput != Vector2.zero)
                animator.SetBool(IsThrowingHash, true);
            else
                animator.SetBool(IsThrowingFullHash, true);
        }

        // Automatic pick-up when objectToAttach is set and not already held
        if (objectToAttach != null && objectToAttach.CanBePickedUpBy(this))
            {
            objectToAttach.Reserve(this); // reserve the object
            canPickUp = false;

            // Trigger pick-up animation
            TriggerPickUpAnimation();
        }



        // === CATCH DETECTION ===
        if (CheckForIncomingObject() && !isCatchingInProgress)
        {
            isCatchingInProgress = true;
            animator.SetBool(IsCatchingHash, true);
        }

        // === LAYER WEIGHT BLENDING ===
        UpdateLayerWeight(IsThrowingHash, ref throwLayerWeight, throwLayerIndex);
        UpdateLayerWeight(IsTakeObjectHash, ref takeLayerWeight, takeLayerIndex);
        UpdateCatchLayerWeight(ref catchLayerWeight, catchLayerIndex);

        // === STATE RESET ===
        ResetAnimationState(throwLayerIndex, "Throw_Run", IsThrowingHash);
        ResetAnimationState(0, "ThrowFullBody", IsThrowingFullHash);
        ResetAnimationState(takeLayerIndex, "Take_Object", IsTakeObjectHash);
        ResetAnimationState(0, "TakeObjectFull", IsTakeObjectFullHash);
        ResetAnimationState(catchLayerIndex, "Catch", IsCatchingHash);
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
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * passTurnSpeed
        );

        // Check if almost aligned
        float angle = Quaternion.Angle(transform.rotation, targetRot);
        if (angle < 2f)
        {
            isTurningToPassTarget = false;

            // Start throw animation after facing target
            AnimatorStateInfo baseState = animator.GetCurrentAnimatorStateInfo(0);
            if (baseState.IsName("Idle") || baseState.IsName("Walk"))
            {
                if (rb.linearVelocity.magnitude > 0.1f)
                    animator.SetBool(IsThrowingHash, true);
                else
                    animator.SetBool(IsThrowingFullHash, true);

                Debug.Log("[PASS] Facing target → animation triggered");
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

    private void ResetAnimationState(int layerIndex, string stateName, int boolHash)
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (state.IsName(stateName) && state.normalizedTime >= 0.95f)
        {
            animator.SetBool(boolHash, false);

            if ((boolHash == IsThrowingHash || boolHash == IsThrowingFullHash) && heldObject == null)
                animator.SetBool(boolHash, false);

            if ((boolHash == IsTakeObjectHash || boolHash == IsTakeObjectFullHash) && objectToAttach == null)
                animator.SetBool(boolHash, false);

            if (boolHash == IsCatchingHash)
                isCatchingInProgress = false;
            if ((boolHash == IsTakeObjectHash || boolHash == IsTakeObjectFullHash) && objectToAttach == null)
            {
                animator.SetBool(boolHash, false);
                isPickUpInProgress = false; // reset flag pick-up
            }
        }
    }

    public void ResetCatchAndPickLayers()
    {
        // Reset animasi catch
        isCatchingInProgress = false;
        catchFreeze = false;
        animator.SetBool(IsReadyToCatchHash, false);
        animator.SetBool(IsCatchingHash, false);

        // Reset pick up layer
        animator.SetBool(IsTakeObjectHash, false);
        animator.SetBool(IsTakeObjectFullHash, false);
        takeLayerWeight = 0f;
        animator.SetLayerWeight(takeLayerIndex, 0f);

        // Reset catch layer
        throwLayerWeight = 0f;
        animator.SetLayerWeight(throwLayerIndex, 0f);
        catchLayerWeight = 0f;
        animator.SetLayerWeight(catchLayerIndex, 0f);
    }

    public void TriggerPickUpAnimation()
    {
        if (isPickUpInProgress) return; // jika pick-up sedang berjalan, jangan trigger lagi

        isPickUpInProgress = true;

        if (animator.GetFloat(SpeedHash) > 0.1f)
            animator.SetBool(IsTakeObjectHash, true);
        else
            animator.SetBool(IsTakeObjectFullHash, true);
    }


    private bool CheckForIncomingObject() => false; // Optional

    public void AttachObjectToHand(ThrowableObject obj)
    {
        if (obj == null || rightHand == null) return;

        heldObject = obj;
        obj.ClearReservation();
        objectToAttach = null;

        obj.OnPickedUp(rightHand, this);
    }

    private Vector3 GetThrowTargetPoint()
    {
        return transform.position + transform.forward * throwDistance + Vector3.up * throwHeight;
    }

    private Vector3 CalculateThrowVelocity(Vector3 origin, Vector3 target, float timeToTarget)
    {
        Vector3 displacementXZ = new Vector3(target.x - origin.x, 0, target.z - origin.z);
        Vector3 velocityXZ = displacementXZ / timeToTarget;
        float velocityY = (target.y - origin.y) / timeToTarget - 0.5f * Physics.gravity.y * timeToTarget;
        return velocityXZ + Vector3.up * velocityY;
    }

    public void ReleaseHeldObjectEvent()
    {
        if (heldObject == null) return;

        heldObject.transform.SetParent(null);
        heldObject.GetComponent<Rigidbody>().isKinematic = false;
        heldObject.GetComponent<Collider>().enabled = true;

        Vector3 targetPoint = passTarget != null
            ? (passTargetCatchPoint != null ? passTargetCatchPoint.position : passTarget.position + Vector3.up * 1.5f)
            : GetThrowTargetPoint();

        PlayerController teammate = passTarget != null ? passTarget.GetComponent<PlayerController>() : null;
        Vector3 throwVelocity = CalculateThrowVelocity(heldObject.transform.position, targetPoint, throwTime);
        throwVelocity *= currentPassPower;

        Debug.Log($"[PASS THROW] Velocity Multiplier: {currentPassPower:F2}");
        heldObject.OnThrown(throwVelocity, teammate);

        heldObject = null;
        canPickUp = false;
        animator.SetBool(IsThrowingHash, false);
        animator.SetBool(IsThrowingFullHash, false);

        canProcessThrow = false;
        StartCoroutine(EnableThrowInput());
        StartCoroutine(ThrowCooldown());

        currentPassPower = 0f;
        passPowerLocked = false;
    }

    private IEnumerator ThrowCooldown()
    {
        recentlyThrew = true;
        yield return new WaitForSeconds(0.3f);
        recentlyThrew = false;
    }

    public void AttachNearbyObjectEvent()
    {
        if (objectToAttach == null) return;
        AttachObjectToHand(objectToAttach);
    }

    public void AttachCaughtObjectEvent()
    {
        if (objectToAttach == null) return;
        if (rightHand == null) return;

        AttachObjectToHand(objectToAttach);
        objectToAttach.transform.position = rightHand.position;
        objectToAttach.transform.rotation = rightHand.rotation;

        objectToAttach = null;
        ResetCatchAndPickLayers();
    }

    public void SetReadyToCatch(bool ready)
    {
        animator.SetBool(IsReadyToCatchHash, ready);
    }

    private void UpdateCatchLayerWeight(ref float layerWeight, int layerIndex)
    {
        if (catchFreeze)
        {
            // Kekalkan layer weight di 1 (freeze)
            animator.SetLayerWeight(layerIndex, 1f);
            return;
        }

        bool ready = animator.GetBool(IsReadyToCatchHash);
        bool catching = isCatchingInProgress;
        float target = (ready || catching) ? 1f : 0f;
        layerWeight = Mathf.MoveTowards(layerWeight, target, Time.deltaTime * layerBlendSpeed);
        animator.SetLayerWeight(layerIndex, layerWeight);
    }

    private IEnumerator ResetCatchLayerSmoothly()
    {
        float blend = animator.GetLayerWeight(catchLayerIndex);

        while (blend > 0f)
        {
            blend -= Time.deltaTime * layerBlendSpeed; // layerBlendSpeed kawal kelajuan fade
            animator.SetLayerWeight(catchLayerIndex, blend);
            yield return null;
        }

        animator.SetBool(IsCatchingHash, false);
        isCatchingInProgress = false;
        catchLayerWeight = 0f;
    }

    public void TriggerCatch()
    {
        if (!isCatchingInProgress)
        {
            isCatchingInProgress = true;
            animator.SetTrigger(IsCatchingHash);
            StartCoroutine(CatchLayerBlendRoutine());
        }
    }

    private IEnumerator CatchLayerBlendRoutine()
    {
        float blend = 0f;

        while (blend < 1f)
        {
            blend += Time.deltaTime * layerBlendSpeed;
            animator.SetLayerWeight(catchLayerIndex, blend);
            yield return null;
        }

        yield return new WaitForSeconds(0.6f);

        while (blend > 0f)
        {
            blend -= Time.deltaTime * layerBlendSpeed;
            animator.SetLayerWeight(catchLayerIndex, blend);
            yield return null;
        }

        isCatchingInProgress = false;
        animator.ResetTrigger(IsCatchingHash);
    }

    public void CatchObject(ThrowableObject obj)
    {
        if (obj == null || isCatchingInProgress) return;

        obj.StopMotion();
        objectToAttach = obj;

        TriggerCatch();

        Vector3 dir = obj.transform.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 8f);
        }
    }

    public void CatchObjectFromTrigger(ThrowableObject obj, bool isUpper)
    {
        if (obj == null || objectToAttach != null) return;

        obj.StopMotion();
        objectToAttach = obj;

        if (isUpper)
            animator.SetTrigger(IsCatchingHash); // animasi catch upper
        else
            TriggerPickUpAnimation(); // animasi pick up (catch lower)
    }

    public void ThrowHeldObject(Vector3 velocity, PlayerController target = null)
    {
        if (heldObject == null) return;

        heldObject.OnThrown(velocity, target);
        heldObject = null;
        objectToAttach = null; // jaga-jaga
    }


    private IEnumerator AutoAttachCaughtObject(ThrowableObject obj)
    {
        yield return new WaitForSeconds(0.05f);

        if (obj != null && rightHand != null)
        {
            AttachObjectToHand(obj);
            obj.transform.position = rightHand.position;
            obj.transform.rotation = rightHand.rotation;

            Debug.Log($"{name} auto-attached {obj.name} after catch delay.");
        }

        objectToAttach = null;
        isCatchingInProgress = false;

        animator.SetBool(IsCatchingHash, false);
    }

    public void TriggerCatchUpper()
    {
        animator.SetTrigger(IsCatchingHash); // animasi upper body
    }

    public void TriggerCatchLower()
    {
        TriggerPickUpAnimation(); // animasi pick-up sama dengan catch lower
    }

    public void SetReadyToCatchUpper(bool ready)
    {
        animator.SetBool(IsReadyToCatchHash, ready);
    }

    public void SetReadyToCatchLower(bool ready)
    {
        animator.SetBool(IsReadyToCatchHash, ready);
    }


}
