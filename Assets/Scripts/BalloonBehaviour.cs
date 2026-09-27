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
        if (GameManager.Instance.CurrentState.Value != GameState.Playing) return;

        // Efficiency 0 = damaged balloon without lift. Rising or sinking follows from the lift vs. the ship's weight.
        leftEfficiency = leftOfen != null ? leftOfen.Efficiency : 0f;
        rightEfficiency = rightOfen != null ? rightOfen.Efficiency : 0f;

        GameManager.Instance.ApplyShipLift(leftEfficiency, rightEfficiency);
    }
}
