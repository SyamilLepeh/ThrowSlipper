using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CatchTrigger : MonoBehaviour
{
    private PlayerController playerController;

    [Header("Catch Settings")]
    public bool upperBodyZone = false; // trigger untuk upper body
    public bool lowerBodyZone = true;  // trigger untuk lower body

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playerController.isCatchingInProgress) return;
        if (!other.CompareTag("Throwable")) return;

        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (obj == null) return;

        if (!obj.IsHeld() && obj.passTarget == playerController)
        {
            obj.StopMotion();

            if (playerController.objectToAttach != null) return;

            // Set object untuk attach
            playerController.objectToAttach = obj;

            // Trigger animasi catch sesuai zona
            if (upperBodyZone)
            {
                playerController.CatchUpperObject(obj); // gunakan coroutine handleCatch
            }
            else if (lowerBodyZone)
            {
                playerController.CatchLowerObject();
            }
        }
    }
}
