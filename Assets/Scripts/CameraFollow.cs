using UnityEngine;

public class CameraFollowSimple : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform player;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 11.5f, -10.5f); // Adjust height & distance
    public float smoothTime = 0f;
    private Vector3 velocity = Vector3.zero;

    [Header("Look Settings")]
    public bool lookAtPlayer = true;
    public Vector3 lookOffset = new Vector3(0f, 6f, 0f); // look slightly above player

    void LateUpdate()
    {
        if (!player) return;

        // Target position
        Vector3 targetPosition = player.position + offset;

        // Smooth follow using SmoothDamp
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

        // Optional: look at player
        if (lookAtPlayer)
        {
            Vector3 lookTarget = player.position + lookOffset;
            transform.LookAt(lookTarget);
        }
    }
}
