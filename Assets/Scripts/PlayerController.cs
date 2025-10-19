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
    public InputActionReference takeAction;
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

    [HideInInspector] public bool canPickUp = false;
    [HideInInspector] public bool recentlyThrew = false;
    [HideInInspector] public ThrowableObject objectToAttach;

    private bool canProcessThrow = false;

    private int SpeedHash;
    private int IsThrowingHash;
    private int IsThrowingFullHash;
    private int IsTakeObjectHash;
    private int IsTakeObjectFullHash;
    private int IsCatchingHash;

    private bool isThrowingFullBody = false;
    private bool isTakingFullBody = false;
    private bool isCatchingInProgress = false;

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

        // Animator hashes
        SpeedHash = Animator.StringToHash("Speed");
        IsThrowingHash = Animator.StringToHash("isThrowing");
        IsThrowingFullHash = Animator.StringToHash("isThrowingFull");
        IsTakeObjectHash = Animator.StringToHash("isTakeObject");
        IsTakeObjectFullHash = Animator.StringToHash("isTakeObjectFull");
        IsCatchingHash = Animator.StringToHash("isCatching");

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
        takeAction.action.Enable();
        throwAction.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        runAction.action.Disable();
        takeAction.action.Disable();
        throwAction.action.Disable();
    }

    void Update()
    {
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        bool runPressed = runAction.action.IsPressed();
        bool throwPressed = throwAction.action.WasPressedThisFrame();
        bool takePressed = takeAction.action.WasPressedThisFrame();

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

        // === THROW ===
        if (throwPressed && heldObject != null && canProcessThrow)
        {
            // Ensure heldObject is still in hand
            if (!heldObject.IsHeld())
                return;

            if (moveInput != Vector2.zero)
                animator.SetBool(IsThrowingHash, true);
            else
                animator.SetBool(IsThrowingFullHash, true);
        }


        // === TAKE ===
        if (takePressed && canPickUp && objectToAttach != null)
        {
            AttachObjectToHand(objectToAttach); // assign immediately
            objectToAttach = null;

            if (moveInput != Vector2.zero)
                animator.SetBool(IsTakeObjectHash, true);
            else
                animator.SetBool(IsTakeObjectFullHash, true);
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
        UpdateLayerWeight(IsCatchingHash, ref catchLayerWeight, catchLayerIndex);

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

    private IEnumerator EnableThrowInput()
    {
        yield return new WaitForSeconds(0.1f);
        canProcessThrow = true;
    }

    // === HELPER METHODS ===
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

            // Extra safety: if no object is held, reset throw flags
            if (boolHash == IsThrowingHash || boolHash == IsThrowingFullHash)
            {
                if (heldObject == null)
                    animator.SetBool(boolHash, false);
            }
            if (boolHash == IsTakeObjectHash || boolHash == IsTakeObjectFullHash)
            {
                if (objectToAttach == null)
                    animator.SetBool(boolHash, false);
            }

            if (boolHash == IsCatchingHash)
                isCatchingInProgress = false;
        }
    }



    private bool CheckForIncomingObject() => false; // Optional custom detection

    // === OBJECT ATTACH ===
    public void AttachObjectToHand(ThrowableObject obj)
    {
        if (obj == null || rightHand == null) return;
        heldObject = obj;
        obj.OnPickedUp(rightHand, this);
    }

    // === THROW SYSTEM ===
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

    // === ANIMATION EVENTS ===
    public void ReleaseHeldObjectEvent()
    {
        if (heldObject == null) return;

        // Detach object immediately from hand
        heldObject.transform.SetParent(null);

        // Ensure Rigidbody is active and collider enabled
        heldObject.GetComponent<Rigidbody>().isKinematic = false;
        heldObject.GetComponent<Collider>().enabled = true;

        // Compute throw target
        Vector3 targetPoint = passTarget != null
            ? (passTargetCatchPoint != null ? passTargetCatchPoint.position : passTarget.position + Vector3.up * 1.5f)
            : GetThrowTargetPoint();

        PlayerController teammate = passTarget != null ? passTarget.GetComponent<PlayerController>() : null;

        // Apply velocity
        Vector3 throwVelocity = CalculateThrowVelocity(heldObject.transform.position, targetPoint, throwTime);
        heldObject.OnThrown(throwVelocity, teammate);

        // Clear local references
        heldObject = null;
        canPickUp = false;

        // Reset animator flags
        animator.SetBool(IsThrowingHash, false);
        animator.SetBool(IsThrowingFullHash, false);

        // Prevent phantom throw
        canProcessThrow = false;
        StartCoroutine(EnableThrowInput());

        StartCoroutine(ThrowCooldown());
    }

    private IEnumerator ThrowCooldown()
    {
        recentlyThrew = true;
        yield return new WaitForSeconds(0.3f);
        recentlyThrew = false;
    }

    public void AttachNearbyObjectEvent()
    {
        if (objectToAttach != null && rightHand != null)
        {
            // Check distance to make sure object is still close enough
            float distance = Vector3.Distance(objectToAttach.transform.position, transform.position);
            if (distance <= 2f) // 2 units pickup range
            {
                AttachObjectToHand(objectToAttach);
            }
            else
            {
                Debug.LogWarning($"{name} tried to pick up {objectToAttach.name}, but it's too far.");
            }

            objectToAttach = null;
        }
    }


    // === Animation Event: Attach caught object ===
    public void AttachCaughtObjectEvent()
    {
        if (rightHand == null) return;

        // Find the nearest throwable object that is not held
        ThrowableObject nearestObj = null;
        float nearestDistance = 2f; // catch radius

        Collider[] hits = Physics.OverlapSphere(transform.position, nearestDistance);
        foreach (var hit in hits)
        {
            ThrowableObject obj = hit.GetComponent<ThrowableObject>();
            if (obj != null && !obj.IsHeld())
            {
                float dist = Vector3.Distance(hit.transform.position, transform.position);
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    nearestObj = obj;
                }
            }
        }

        // If found, attach to hand
        if (nearestObj != null)
        {
            AttachObjectToHand(nearestObj);
            Debug.Log($"{name} caught {nearestObj.name}");

            // Optional small snap correction
            nearestObj.transform.position = rightHand.position;
            nearestObj.transform.rotation = rightHand.rotation;
        }
        else
        {
            Debug.LogWarning($"{name} tried to catch, but no throwable object nearby.");
        }
    }


    // === CATCH SYSTEM (Trigger Based) ===
    public void TriggerCatch()
    {
        // Only trigger if not already catching
        if (!isCatchingInProgress)
        {
            isCatchingInProgress = true;

            // Trigger catch animation
            animator.SetTrigger(IsCatchingHash);

            // Start blend-in/out coroutine
            StartCoroutine(CatchLayerBlendRoutine());
        }
    }

    private IEnumerator CatchLayerBlendRoutine()
    {
        float blend = 0f;

        // Blend IN CatchLayer
        while (blend < 1f)
        {
            blend += Time.deltaTime * layerBlendSpeed;
            animator.SetLayerWeight(catchLayerIndex, blend);
            yield return null;
        }

        // Wait until catch animation finishes
        yield return new WaitForSeconds(0.6f); // Adjust to your animation length

        // Blend OUT CatchLayer
        while (blend > 0f)
        {
            blend -= Time.deltaTime * layerBlendSpeed;
            animator.SetLayerWeight(catchLayerIndex, blend);
            yield return null;
        }

        isCatchingInProgress = false;
        animator.ResetTrigger(IsCatchingHash);
    }
}