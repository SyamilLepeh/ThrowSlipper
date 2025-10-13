using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    private Animator animator;
    private Rigidbody rb;

    [Header("Movement Settings")]
    public float acceleration = 2.0f;
    public float deceleration = 2.0f;
    public float maximumWalkVelocity = 2.5f;
    public float maximumRunVelocity = 6.0f;
    public float throwLayerBlendSpeed = 5.0f;

    [Header("Input Actions")]
    public InputActionReference moveAction; // Vector2 (WASD or Left Stick)
    public InputActionReference runAction;  // Button (Left Shift or Controller Trigger)

    private int SpeedHash;
    private int IsThrowingHash;
    private int IsThrowingFullHash;

    private float throwLayerWeight = 0f;
    private int throwLayerIndex;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        SpeedHash = Animator.StringToHash("Speed");
        IsThrowingHash = Animator.StringToHash("isThrowing");
        IsThrowingFullHash = Animator.StringToHash("isThrowingFull");

        throwLayerIndex = animator.GetLayerIndex("ThrowLayer");
        animator.SetLayerWeight(throwLayerIndex, 0f);
    }

    void OnEnable()
    {
        moveAction.action.Enable();
        runAction.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        runAction.action.Disable();
    }

    void Update()
    {
        // === INPUT ===
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        bool runPressed = runAction.action.IsPressed();
        bool throwPressed = Mouse.current.leftButton.wasPressedThisFrame;

        float currentMaxVelocity = runPressed ? maximumRunVelocity : maximumWalkVelocity;

        // === MOVEMENT SPEED ===
        float targetSpeed = moveInput.magnitude * currentMaxVelocity;
        float currentSpeed = Mathf.MoveTowards(animator.GetFloat(SpeedHash), targetSpeed, Time.deltaTime * acceleration);
        animator.SetFloat(SpeedHash, currentSpeed);

        // === ROTATION ===
        if (moveInput != Vector2.zero)
        {
            Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
        }

        // === THROW LOGIC ===
        bool isMoving = moveInput != Vector2.zero;

        if (throwPressed)
        {
            if (isMoving)
                animator.SetBool(IsThrowingHash, true);  // upper body throw
            else
                animator.SetBool(IsThrowingFullHash, true); // full body throw
        }

        // === LAYER BLENDING ===
        if (animator.GetBool(IsThrowingHash))
            throwLayerWeight = Mathf.MoveTowards(throwLayerWeight, 1f, Time.deltaTime * throwLayerBlendSpeed);
        else
            throwLayerWeight = Mathf.MoveTowards(throwLayerWeight, 0f, Time.deltaTime * throwLayerBlendSpeed);

        animator.SetLayerWeight(throwLayerIndex, throwLayerWeight);

        // === RESET THROW STATES ===
        AnimatorStateInfo throwState = animator.GetCurrentAnimatorStateInfo(throwLayerIndex);
        AnimatorStateInfo baseState = animator.GetCurrentAnimatorStateInfo(0);

        if (throwState.IsName("Throw_Run") && throwState.normalizedTime >= 0.95f)
            animator.SetBool(IsThrowingHash, false);

        if (baseState.IsName("ThrowFullBody") && baseState.normalizedTime >= 0.95f)
            animator.SetBool(IsThrowingFullHash, false);
    }

    void FixedUpdate()
    {
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        bool runPressed = runAction.action.IsPressed();
        float currentMaxVelocity = runPressed ? maximumRunVelocity : maximumWalkVelocity;
        float currentSpeed = animator.GetFloat(SpeedHash);

        if (moveInput != Vector2.zero)
        {
            Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y).normalized;
            Vector3 move = direction * currentSpeed;
            rb.MovePosition(rb.position + move * Time.fixedDeltaTime);
        }
    }

}
