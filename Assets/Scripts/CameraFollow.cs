using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;       // assign your player here
    public Vector3 offset = new Vector3(0f, 5f, -7f); // position behind player
    public float smoothSpeed = 0.125f; // how smoothly camera follows
    public float rotationSpeed = 5f;   // how fast camera rotates with player
    [Range(-45f, 45f)]
    public float cameraTiltX = 15f;    // adjustable camera tilt angle (X rotation)

    void LateUpdate()
    {
        if (!player) return;

        // Desired position
        Vector3 desiredPosition = player.position + player.TransformDirection(offset);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        // Base rotation to follow player
        Quaternion targetRotation = Quaternion.LookRotation(player.forward, Vector3.up);

        // Apply adjustable X rotation (tilt)
        Quaternion lookDownRotation = Quaternion.Euler(cameraTiltX, targetRotation.eulerAngles.y, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookDownRotation, rotationSpeed * Time.deltaTime);
    }
}
