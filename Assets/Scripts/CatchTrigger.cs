using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CatchTrigger : MonoBehaviour
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
                Debug.Log($"[CatchTrigger] {obj.name} entered pickup zone");
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
                Debug.Log("[CatchTrigger] Left pickup zone");
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && nearbyObject != null && !playerController.recentlyThrew)
        {
            playerController.objectToAttach = nearbyObject;
            nearbyObject.SetOwner(playerController);

            Animator animator = playerController.animator;
            float moveSpeed = animator.GetFloat("Speed");

            if (moveSpeed < 0.1f)
                animator.SetBool("isTakeObjectFull", true);
            else
                animator.SetBool("isTakeObject", true);

            nearbyObject = null;
            playerController.canPickUp = false;
        }
    }
}
