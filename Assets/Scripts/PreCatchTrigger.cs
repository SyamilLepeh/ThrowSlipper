using UnityEngine;

public class PreCatchTrigger : MonoBehaviour
{
    private PlayerController playerController;
    private Transform playerTransform;

    [Header("Rotation Settings")]
    public float rotateSpeed = 5f;

    private bool isRotating = false;
    private Quaternion targetRotation;

    // Track objek yang sedang “ready”
    private ThrowableObject trackedObj = null;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
        playerTransform = playerController.transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;

        var obj = other.GetComponent<ThrowableObject>();
        if (obj == null) return;

        // Hanya ready kalau bola ini memang ditujukan kepada player ini
        if (obj.passTarget != playerController) return;

        trackedObj = obj;

        // Rotate ke arah objek
        Vector3 dir = obj.transform.position - playerTransform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            targetRotation = Quaternion.LookRotation(dir);
            isRotating = true;
        }

        playerController.SetReadyToCatch(true);
        playerController.StartReadyCatchTimeout(6f); // optional (kalau awak guna)
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Throwable")) return;

        var obj = other.GetComponent<ThrowableObject>();
        if (obj == null) return;

        // Penting: hanya cancel kalau objek yang keluar itu memang yang kita track
        if (trackedObj != obj) return;

        CancelReady();
    }

    private void Update()
    {
        // Jika tiada objek untuk track, tak buat apa-apa
        if (trackedObj == null) return;

        // Kalau tiba-tiba bola dah bukan target kita (contoh Player A ambil / passTarget berubah)
        if (trackedObj.passTarget != playerController)
        {
            CancelReady();
            return;
        }

        // Rotation smoothing
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

    private void CancelReady()
    {
        trackedObj = null;
        isRotating = false;
        playerController.SetReadyToCatch(false);
    }
}
