using System.Collections;
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
        TryPick(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryPick(other);
    }


    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;

        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (playerController.objectToAttach == obj)
        {
            // block pickup hanya jika tengah attach proses
            if (playerController.isPickUpInProgress) return;

            // block jika tengah catch (memang betul)
            if (playerController.isCatchingUpperInProgress || playerController.isCatchingLowerInProgress) return;

            // kalau objectToAttach ada tapi itu bukan object yang sedang overlap, clear dulu
            if (playerController.objectToAttach != null && playerController.objectToAttach != obj)
            {
                playerController.objectToAttach.ClearReservation();
                playerController.objectToAttach = null;
            }

            playerController.canPickUp = false;

            obj.ClearReservation();
        }
    }

    private void TryPick(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;

        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (obj == null) return;

        // skip pick-up jika object berada dalam catch state
        if (playerController.isCatchingUpperInProgress || playerController.isCatchingLowerInProgress || playerController.objectToAttach != null)
            return;

        if (!obj.CanBePickedUpBy(playerController)) return;

        obj.Reserve(playerController);

        playerController.objectToAttach = obj;
        playerController.MarkObjectToAttachTime();
        playerController.canPickUp = true;

        playerController.TriggerPickUpAnimation();
    }

}
