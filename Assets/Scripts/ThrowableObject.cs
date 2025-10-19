using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ThrowableObject : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;

    private PlayerController currentThrower;
    private PlayerController handOwner; // current holder

    [Header("Catch Settings")]
    public float catchDetectionRadius = 1.5f;
    public LayerMask playerLayer;
    public float minCatchSpeed = 1.0f;

    [HideInInspector] public PlayerController passTarget; // Only this player can catch

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    // --- PICK UP ---
    public void OnPickedUp(Transform hand, PlayerController owner)
    {
        rb.isKinematic = true;
        col.enabled = false;

        transform.SetParent(hand);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        handOwner = owner;
        currentThrower = null;
        passTarget = null;
    }

    public void SetOwner(PlayerController owner)
    {
        handOwner = owner;
    }

    // --- THROW ---
    public void OnThrown(Vector3 throwVelocity, PlayerController target = null)
    {
        if (handOwner == null) return;

        transform.SetParent(null);
        rb.isKinematic = false;
        col.enabled = true;

        rb.linearVelocity = throwVelocity;
        currentThrower = handOwner;
        handOwner = null;

        passTarget = target; // assign the intended catcher

        CancelInvoke(nameof(CheckForCatch));
        InvokeRepeating(nameof(CheckForCatch), 0.05f, 0.05f);
    }

    // --- CATCH DETECTION ---
    private void CheckForCatch()
    {
        if (rb.linearVelocity.magnitude < minCatchSpeed) return;

        if (passTarget != null)
        {
            float distance = Vector3.Distance(transform.position, passTarget.transform.position);
            if (distance <= catchDetectionRadius && !passTarget.recentlyThrew)
            {
                CancelInvoke(nameof(CheckForCatch));
                StartCoroutine(CatchSequence(passTarget));
            }
        }
    }

    private IEnumerator CatchSequence(PlayerController catcher)
    {
        // 1. Play catch animation
        catcher.TriggerCatch();

        // 2. Wait until animation is synced
        yield return new WaitForSeconds(0.45f);

        // 3. Attach object to player hand
        rb.isKinematic = true;
        col.enabled = false;

        transform.SetParent(catcher.rightHand);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        handOwner = catcher;
        catcher.AttachObjectToHand(this);

        Debug.Log($"{catcher.name} successfully caught {name}");

        // Clear pass target after catch
        passTarget = null;
    }

    // --- HELPER ---
    public bool IsHeld() => handOwner != null;
}
