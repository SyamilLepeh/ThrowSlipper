using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ThrowableObject : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;

    private PlayerController handOwner;

    private bool isReserved = false;
    private PlayerController reservedBy;   // ✅ WAJIB

    [HideInInspector] public PlayerController passTarget;

    public bool IsHeld() => handOwner != null;
    public bool IsReserved() => isReserved;

    private bool guidedPass = false;
    private Transform guidedTarget;
    private float guidedDuration = 0.35f;
    private float guidedElapsed = 0f;

    private Vector3 guidedStart;
    private Vector3 guidedEnd;
    private float guidedArcHeight = 1.5f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void OnPickedUp(Transform hand, PlayerController owner)
    {
        rb.isKinematic = true;
        col.enabled = false;

        transform.SetParent(hand);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        handOwner = owner;
        passTarget = null;

        // clear reserve
        isReserved = false;
        reservedBy = null;
    }

    public void OnThrown(Vector3 velocity, PlayerController target = null)
    {
        transform.SetParent(null);

        rb.isKinematic = false;
        col.enabled = true;

        // Unity 6: linearVelocity ok
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = velocity;

        handOwner = null;
        passTarget = target;

        isReserved = false;
        reservedBy = null;
    }

    public void StartGuidedPass(Transform target, float duration, float arcHeight, PlayerController targetPlayer)
    {
        if (target == null) return;

        transform.SetParent(null);

        // ❌ JANGAN sentuh velocity langsung
        rb.isKinematic = true;
        col.enabled = true;

        guidedPass = true;
        guidedTarget = target;
        guidedStart = transform.position;

        guidedDuration = Mathf.Max(0.06f, duration);
        guidedElapsed = 0f;
        guidedArcHeight = Mathf.Max(0.01f, arcHeight);

        passTarget = targetPlayer;

        handOwner = null;
        isReserved = false;
        reservedBy = null;
    }



    private void FixedUpdate()
    {
        if (!guidedPass) return;

        if (guidedTarget != null)
            guidedEnd = guidedTarget.position; // follow moving catch point

        guidedElapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(guidedElapsed / guidedDuration);

        // Quadratic Bezier for nice arc
        Vector3 mid = (guidedStart + guidedEnd) * 0.5f + Vector3.up * guidedArcHeight;
        Vector3 a = Vector3.Lerp(guidedStart, mid, t);
        Vector3 b = Vector3.Lerp(mid, guidedEnd, t);
        Vector3 pos = Vector3.Lerp(a, b, t);

        rb.MovePosition(pos);

        if (t >= 1f)
        {
            guidedPass = false;

            rb.isKinematic = false; // balik ke physics
        }


    }

    public void Reserve(PlayerController owner)
    {
        isReserved = true;
        reservedBy = owner;
    }

    public void ClearReservation()
    {
        isReserved = false;
        reservedBy = null;
    }

    public void StopMotion()
    {
        // ✅ kalau guided pass sedang berjalan, biarkan CatchTrigger urus
        if (guidedPass) return;

        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.isKinematic = true;
        col.enabled = false;
    }

    public void SoftStop()
    {
        // hanya clear velocity kalau masih dynamic
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        // ❌ jangan set kinematic / jangan disable collider di sini
    }


    public bool CanBePickedUpBy(PlayerController player)
    {
        if (IsHeld()) return false;
        if (isReserved && reservedBy != player) return false;
        return true;
    }

    public void CancelGuidedPass()
    {
        guidedPass = false;
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = IsHeld() ? Color.red : IsReserved() ? Color.yellow : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }

    public PlayerController GetOwner() => handOwner;
}
