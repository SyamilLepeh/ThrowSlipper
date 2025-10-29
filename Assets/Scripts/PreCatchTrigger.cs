using UnityEngine;

public class PreCatchTrigger : MonoBehaviour
{
    private PlayerController playerController;
    private Transform playerTransform;
    private bool isRotating = false;
    private Quaternion targetRotation;

    [Header("Rotation Settings")]
    public float rotateSpeed = 5f; // kelajuan pusingan

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

        // Hanya respond kalau objek memang di-pass kepada player ini
        if (obj.passTarget != playerController) return;

        // Bekukan arah pada saat masuk (supaya tidak follow objek yang bergerak)
        Vector3 dir = (obj.transform.position - playerTransform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            targetRotation = Quaternion.LookRotation(dir);
            isRotating = true;
        }

        // Set animator param: siap untuk catch (masuk CatchFreeze)
        playerController.SetReadyToCatch(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;

        ThrowableObject obj = other.GetComponent<ThrowableObject>();
        if (obj == null) return;

        if (obj.passTarget != playerController) return;

        // Keluar dari zone: hentikan rotate (jika belum selesai) dan reset animator param
        isRotating = false;
        playerController.SetReadyToCatch(false);
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
