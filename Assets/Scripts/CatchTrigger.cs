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

        if (!obj.IsHeld() && obj.passTarget == playerController)
        {
            // Stop motion and let PlayerController handle the rest (rotation/anim/attach)
            obj.StopMotion();

            // Make sure we don't leave isReadyToCatch true while catching
            playerController.SetReadyToCatch(false);

            // Ask player controller to handle catch (plays animation and will auto-attach)
            playerController.CatchObject(obj);
        }
    }
}
