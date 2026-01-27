using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CatchTrigger : MonoBehaviour
{
    private PlayerController playerController;

    [Header("Catch Settings")]
    public bool upperBodyZone = false; // trigger untuk upper body
    public bool lowerBodyZone = false;  // trigger untuk lower body

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;

        // ✅ check dengan flag baru
        if (playerController.isCatchingUpperInProgress || playerController.isCatchingLowerInProgress) return;

        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (obj == null) return;
        if (obj.passTarget != playerController) return;

        // Stop object
        obj.CancelGuidedPass();
        obj.StopMotion();

        // Simpan untuk attach (Animation Event akan attach)
        playerController.objectToAttach = obj;

        // Set flag yang sesuai
        if (upperBodyZone)
            playerController.isCatchingUpperInProgress = true;
        else if (lowerBodyZone)
            playerController.isCatchingLowerInProgress = true;

        // Trigger animation sahaja
        if (upperBodyZone)
            playerController.TriggerCatchUpper();
        else if (lowerBodyZone)
            playerController.TriggerCatchLower();
    }
}
