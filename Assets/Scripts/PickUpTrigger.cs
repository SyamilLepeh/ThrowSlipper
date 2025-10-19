using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickUpTrigger : MonoBehaviour
{
    private PlayerController playerController;
    private ThrowableObject nearbyObject;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Throwable"))
        {
            ThrowableObject obj = other.GetComponent<ThrowableObject>();
            if (obj != null && !obj.IsHeld())
            {
                nearbyObject = obj;
                playerController.canPickUp = true;
                Debug.Log($"[PickUpTrigger] {obj.name} entered pickup zone");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Throwable"))
        {
            ThrowableObject obj = other.GetComponent<ThrowableObject>();
            if (obj == nearbyObject)
            {
                nearbyObject = null;
                playerController.canPickUp = false;
                Debug.Log("[PickUpTrigger] Left pickup zone");
            }
        }
    }

    private void Update()
    {
        if (nearbyObject == null || playerController.recentlyThrew) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            float distance = Vector3.Distance(playerController.transform.position, nearbyObject.transform.position);

            if (distance > 2f) return; // Ensure object is within pickup range

            // Assign the object to the player
            playerController.objectToAttach = nearbyObject;

            // Clear the trigger reference
            nearbyObject = null;
            playerController.canPickUp = false;

            // Trigger proper animation
            Animator animator = playerController.animator;
            float moveSpeed = animator.GetFloat("Speed");
            if (moveSpeed < 0.1f)
                animator.SetBool("isTakeObjectFull", true);
            else
                animator.SetBool("isTakeObject", true);

            Debug.Log("[PickUpTrigger] Object picked up");
        }
    }
}
