using UnityEngine;

public class PreCatchTrigger : MonoBehaviour
{
    private PlayerController playerController;
    private Transform playerTransform;
    private bool isRotating = false;
    private Quaternion targetRotation;

    [Header("Rotation Settings")]
    public float rotateSpeed = 5f;

    [Header("Trigger Settings")]
    public bool upperBodyZone = false; // tandakan jika trigger ini untuk upper body
    public bool lowerBodyZone = true;  // tandakan jika trigger ini untuk lower body

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
        playerTransform = playerController.transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;
        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (obj == null) return;

        if (obj.passTarget != playerController) return;

        // Setup rotation ke arah objek
        Vector3 dir = obj.transform.position - playerTransform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            targetRotation = Quaternion.LookRotation(dir);
            isRotating = true;
        }

        // Tentukan animasi siap catch berdasarkan zona
        if (upperBodyZone)
            playerController.SetReadyToCatchUpper(true);
        else if (lowerBodyZone)
            playerController.SetReadyToCatchLower(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;
        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (obj == null) return;
        if (obj.passTarget != playerController) return;

        playerController.catchFreeze = false;
        isRotating = false;

        // Reset animator param
        if (upperBodyZone)
            playerController.SetReadyToCatchUpper(false);
        else if (lowerBodyZone)
            playerController.SetReadyToCatchLower(false);
    }

    private void Update()
    {
        if (!isRotating) return;

        playerTransform.rotation = Quaternion.Lerp(
            playerTransform.rotation,
            targetRotation,
            Time.deltaTime * rotateSpeed
        );

        if (Quaternion.Angle(playerTransform.rotation, targetRotation) < 2f)
        {
            playerTransform.rotation = targetRotation;
            isRotating = false;
        }
    }
}
