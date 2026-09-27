using Unity.Netcode;
using UnityEngine;

public class BalloonBehaviour : MonoBehaviour
{
    [SerializeField] private OfenManager leftOfen, rightOfen;

    [Header("Runtime Variables")]
    [SerializeField] private float leftEfficiency;
    [SerializeField] private float rightEfficiency;

    private void FixedUpdate()
    {
        // Ship physics is server authoritative, so only the server applies lift.
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsSpawned) return;

        if (GameManager.Instance.CurrentState.Value != GameState.Playing)
        {
            // Lobby: the ship just hovers (level 1 on both sides), damage does not count yet.
            leftEfficiency = rightEfficiency = 1f;
        }
        else
        {
            // Efficiency 0 = damaged balloon without lift. Level 1 on both sides holds the height, more rises, less sinks.
            leftEfficiency = leftOfen != null ? leftOfen.Efficiency : 0f;
            rightEfficiency = rightOfen != null ? rightOfen.Efficiency : 0f;
        }

        GameManager.Instance.ApplyShipLift(leftEfficiency, rightEfficiency);
    }
}
