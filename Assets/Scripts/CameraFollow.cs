using UnityEngine;

public class CameraFollowSimple : MonoBehaviour
{
    public enum CameraMode { Static, TopDown }
    public CameraMode cameraMode = CameraMode.Static;

    [Header("Common Settings")]
    public Transform player;
    public float smoothSpeed = 5f;

    [Header("Static Camera Settings")]
    public Vector3 staticPosition = new Vector3(0f, 5f, -10f);
    public bool lookAtPlayer = true;

    [Header("Top-Down Camera Settings")]
    public float topDownHeight = 10f;
    public float topDownTilt = 60f; // degrees

    void LateUpdate()
    {
        if (!player) return;

        switch (cameraMode)
        {
            // --- STATIC CAMERA ---
            case CameraMode.Static:
                transform.position = Vector3.Lerp(transform.position, staticPosition, Time.deltaTime * smoothSpeed);
                if (lookAtPlayer)
                    transform.LookAt(player.position + Vector3.up * 1.5f);
                break;

            // --- TOP DOWN CAMERA ---
            case CameraMode.TopDown:
                Vector3 targetPosition = player.position + Vector3.up * topDownHeight;
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
                Quaternion desiredRotation = Quaternion.Euler(topDownTilt, 0f, 0f);
                transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime * smoothSpeed);
                break;
        }
    }
}
