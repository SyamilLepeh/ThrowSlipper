using UnityEngine;

public class CameraFollowSimple : MonoBehaviour
{
    [Header("Auto Follow Active Player")]
    public bool followActivePlayerFromManager = true;

    [Header("Manual Target (fallback)")]
    public Transform player;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 11.5f, -10.5f);
    public float smoothTime = 0.15f;
    private Vector3 velocity = Vector3.zero;

    [Header("Switch Transition")]
    public float switchBlendTime = 0.25f;   // ✅ smooth masa tukar target
    private Transform lastPlayer;
    private float switchT = 1f;
    private Vector3 switchFromPos;

    [Header("Look Settings")]
    public bool lookAtPlayer = true;
    public Vector3 lookOffset = new Vector3(0f, 6f, 0f);

    void LateUpdate()
    {
        // auto set target dari manager
        if (followActivePlayerFromManager && PlayerControlManager.Instance != null)
        {
            var ap = PlayerControlManager.Instance.ActivePlayer;
            if (ap != null) player = ap.transform;
        }

        if (!player) return;

        // ✅ detect target change → start transition
        if (player != lastPlayer)
        {
            lastPlayer = player;
            switchFromPos = transform.position;
            switchT = 0f;

            // optional: reset velocity supaya tak "whip"
            velocity = Vector3.zero;
        }

        Vector3 desired = player.position + offset;

        // ✅ blending masa switch
        if (switchT < 1f && switchBlendTime > 0f)
        {
            switchT += Time.deltaTime / switchBlendTime;
            float t = Mathf.SmoothStep(0f, 1f, switchT);
            transform.position = Vector3.Lerp(switchFromPos, desired, t);
        }
        else
        {
            // follow biasa
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }

        if (lookAtPlayer)
            transform.LookAt(player.position + lookOffset);
    }
}
