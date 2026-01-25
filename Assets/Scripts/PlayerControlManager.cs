using System.Collections.Generic;
using UnityEngine;

public class PlayerControlManager : MonoBehaviour
{
    public static PlayerControlManager Instance { get; private set; }

    private readonly List<PlayerController> players = new();
    private PlayerController activePlayer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(PlayerController p)
    {
        if (p == null || players.Contains(p)) return;
        players.Add(p);

        // kalau belum ada active, set player pertama sebagai active
        if (activePlayer == null)
            SwitchTo(p);
        else
            p.SetControlActive(false);

        RefreshPickupAreas();
    }

    public void SwitchTo(PlayerController next)
    {
        if (next == null) return;
        activePlayer = next;

        foreach (var p in players)
            p.SetControlActive(p == activePlayer);

        RefreshPickupAreas();
    }

    /// <summary>
    /// Rule:
    /// - Jika ada holder (heldObject != null): hanya holder punya PickUpArea ON
    /// - Jika tiada holder: hanya activePlayer punya PickUpArea ON
    /// </summary>
    public void RefreshPickupAreas()
    {
        PlayerController holder = null;
        foreach (var p in players)
        {
            if (p != null && p.heldObject != null)
            {
                holder = p;
                break;
            }
        }

        foreach (var p in players)
        {
            if (p == null) continue;

            bool enable =
                (holder != null) ? (p == holder)
                                 : (p == activePlayer);

            p.SetPickUpAreaEnabled(enable);
        }
    }
}
