using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ThrowableObject : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;
    private bool isHeld = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void OnPickedUp(Transform hand)
    {
        if (isHeld) return;
        isHeld = true;

        rb.isKinematic = true;
        col.enabled = false;

        transform.SetParent(hand);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void OnThrown(Vector3 throwForce)
    {
        if (!isHeld) return;
        isHeld = false;

        transform.SetParent(null);
        rb.isKinematic = false;
        col.enabled = true;

        rb.AddForce(throwForce, ForceMode.VelocityChange);
    }

    public bool IsHeld()
    {
        return isHeld;
    }
}
