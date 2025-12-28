using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CatchTrigger : MonoBehaviour
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

        if (!obj.IsHeld())
        {
            // Stop motion
            obj.StopMotion();

            if (playerController.objectToAttach != null) return;

            // Reserve and attach automatically
            playerController.objectToAttach = obj;  // assign object to attach
            playerController.SetReadyToCatch(false);
        }
    }
}
