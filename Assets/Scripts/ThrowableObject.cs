﻿using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ThrowableObject : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;
    private PlayerController handOwner;

    [HideInInspector] public PlayerController passTarget;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void OnPickedUp(Transform hand, PlayerController owner)
    {
        rb.isKinematic = true;
        col.enabled = false;

        transform.SetParent(hand);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        handOwner = owner;
        passTarget = null;
    }

    public void OnThrown(Vector3 velocity, PlayerController target = null)
    {
        rb.isKinematic = false;
        col.enabled = true;

        // Reset velocity untuk pastikan lontaran bersih
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Gunakan velocity terus dari parameter
        rb.linearVelocity = velocity;

        handOwner = null;
        passTarget = target;
    }

    public bool IsHeld() => handOwner != null;

    public void StopMotion()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        col.enabled = false;
    }

    public void SetOwner(PlayerController owner)
    {
        handOwner = owner;
    }

    public PlayerController GetOwner()
    {
        return handOwner;
    }
}
