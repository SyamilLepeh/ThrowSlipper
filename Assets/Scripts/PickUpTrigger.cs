using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickUpTrigger : MonoBehaviour
{
    private PlayerController player;

    private void Start()
    {
        player = GetComponentInParent<PlayerController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;

        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (obj == null) return;

        if (obj.IsHeld() || obj.IsReserved()) return;

        player.objectToAttach = obj;
        player.canPickUp = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;

        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (player.objectToAttach == obj)
        {
            player.objectToAttach = null;
            player.canPickUp = false;
        }
    }
}
