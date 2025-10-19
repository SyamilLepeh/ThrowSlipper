using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CatchTrigger : MonoBehaviour
{
    public PlayerController player;

    private void OnTriggerEnter(Collider other)
    {
        ThrowableObject throwable = other.GetComponent<ThrowableObject>();
        if (throwable != null && throwable.passTarget == player && !throwable.IsHeld())
        {
            // Stop the object's motion
            throwable.StopMotion();

            // Attach object to player's hand
            player.AttachObjectToHand(throwable);

            // Play catch animation
            player.TriggerCatch();

            // Clear pass target
            throwable.passTarget = null;
        }
    }
}
