using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    private Animator animator;
    private Rigidbody rb;

    [Header("Movement Settings")]
    public float acceleration = 60.0f;
    public float deceleration = 30.0f;
    public float maximumWalkVelocity = 6.5f;
    public float maximumRunVelocity = 12.0f;
    public float layerBlendSpeed = 5.0f;
    public float rotationSpeed = 10.0f;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference runAction;
    public InputActionReference takeAction;
    public InputActionReference throwAction;

    private int SpeedHash;
    private int IsThrowingHash;
    private int IsThrowingFullHash;
    private int IsTakeObjectHash;
    private int IsTakeObjectFullHash;

    private bool isThrowingFullBody = false;
    private bool isTakingFullBody = false;

    private float throwLayerWeight = 0f;
    private float takeLayerWeight = 0f;

    private int throwLayerIndex;
    private int takeLayerIndex;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        // Animator Hashes
        SpeedHash = Animator.StringToHash("Speed");
        IsThrowingHash = Animator.StringToHash("isThrowing");
        IsThrowingFullHash = Animator.StringToHash("isThrowingFull");
        IsTakeObjectHash = Animator.StringToHash("isTakeObject");
        IsTakeObjectFullHash = Animator.StringToHash("isTakeObjectFull");

        throwLayerIndex = animator.GetLayerIndex("ThrowLayer");
        takeLayerIndex = animator.GetLayerIndex("TakeObjectLayer");

        animator.SetLayerWeight(throwLayerIndex, 0f);
        animator.SetLayerWeight(takeLayerIndex, 0f);
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

        // === Battlefield-style input priority ===
        if (Keyboard.current != null)
        {
            // A + D = move left (A wins)
            if (Keyboard.current.aKey.isPressed && Keyboard.current.dKey.isPressed)
                moveInput.x = -1f;

            // W + S = move forward (W wins)
            if (Keyboard.current.wKey.isPressed && Keyboard.current.sKey.isPressed)
                moveInput.y = 1f;
        }

        bool runPressed = runAction.action.IsPressed();
        bool throwPressed = throwAction.action.WasPressedThisFrame();
        bool takePressed = takeAction.action.WasPressedThisFrame();

        float currentMaxVelocity = runPressed ? maximumRunVelocity : maximumWalkVelocity;

        // === Check full body animations ===
        AnimatorStateInfo baseState = animator.GetCurrentAnimatorStateInfo(0);
        isThrowingFullBody = baseState.IsName("ThrowFullBody") && baseState.normalizedTime < 0.95f;
        isTakingFullBody = baseState.IsName("TakeObjectFull") && baseState.normalizedTime < 0.95f;

        // === Disable movement during full-body animations ===
        if (isThrowingFullBody || isTakingFullBody)
        {
            animator.SetFloat(SpeedHash, 0f);
            return;
        }

        // === Movement speed control ===
        float targetSpeed = moveInput.magnitude * currentMaxVelocity;
        float currentSpeed = animator.GetFloat(SpeedHash);

        if (Mathf.Abs(targetSpeed) > currentSpeed)
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.deltaTime * acceleration);
        else
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, Time.deltaTime * deceleration);

        animator.SetFloat(SpeedHash, currentSpeed);

        // === Rotation ===
        if (moveInput != Vector2.zero)
        {
            Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y);
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        // === Throw logic ===
        bool isMoving = moveInput != Vector2.zero;
        if (throwPressed)
        {
            if (isMoving)
                animator.SetBool(IsThrowingHash, true);
            else
                animator.SetBool(IsThrowingFullHash, true);
        }

        // === Take object logic ===
        if (takePressed)
        {
            if (isMoving)
                animator.SetBool(IsTakeObjectHash, true);      // running/walking
            else
                animator.SetBool(IsTakeObjectFullHash, true);  // idle
        }

        // === Layer blending ===
        UpdateLayerWeight(IsThrowingHash, ref throwLayerWeight, throwLayerIndex);
        UpdateLayerWeight(IsTakeObjectHash, ref takeLayerWeight, takeLayerIndex);

        // === Reset animation states ===
        ResetAnimationState(throwLayerIndex, "Throw_Run", IsThrowingHash);
        ResetAnimationState(0, "ThrowFullBody", IsThrowingFullHash);
        ResetAnimationState(takeLayerIndex, "Take_Object", IsTakeObjectHash);
        ResetAnimationState(0, "TakeObjectFull", IsTakeObjectFullHash);
    }

    void FixedUpdate()
    {
        if (isThrowingFullBody || isTakingFullBody) return;

        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();

        // === Apply same Battlefield-style priority ===
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed && Keyboard.current.dKey.isPressed)
                moveInput.x = -1f; // left wins
            if (Keyboard.current.wKey.isPressed && Keyboard.current.sKey.isPressed)
                moveInput.y = 1f;  // forward wins
        }

        float currentSpeed = animator.GetFloat(SpeedHash);

        if (moveInput != Vector2.zero)
        {
            Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            Vector3 move = direction * currentSpeed;
            rb.MovePosition(rb.position + move * Time.fixedDeltaTime);
        }
    }

    private void UpdateLayerWeight(int boolHash, ref float layerWeight, int layerIndex)
    {
        if (animator.GetBool(boolHash))
            layerWeight = Mathf.MoveTowards(layerWeight, 1f, Time.deltaTime * layerBlendSpeed);
        else
            layerWeight = Mathf.MoveTowards(layerWeight, 0f, Time.deltaTime * layerBlendSpeed);

        animator.SetLayerWeight(layerIndex, layerWeight);
    }

    private void ResetAnimationState(int layerIndex, string stateName, int boolHash)
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (state.IsName(stateName) && state.normalizedTime >= 0.95f)
            animator.SetBool(boolHash, false);
    }
}
