using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickUpTrigger : MonoBehaviour
{
    private PlayerController playerController;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;

        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (obj == null) return;

        // PRIORITY CHECK: skip pick-up jika object berada dalam catch zone
        if (playerController.IsCatching || playerController.objectToAttach != null)
            return;

        if (!obj.CanBePickedUpBy(playerController)) return;

        obj.Reserve(playerController);

        playerController.objectToAttach = obj;
        playerController.canPickUp = true;

        // Start pick-up animation automatically
        playerController.TriggerPickUpAnimation();
    }


    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;

        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (playerController.objectToAttach == obj)
        {
            playerController.objectToAttach = null;
            playerController.canPickUp = false;

            obj.ClearReservation();
        }
    }
}
