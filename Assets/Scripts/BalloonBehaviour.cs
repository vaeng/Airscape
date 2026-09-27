using Unity.Netcode;
using UnityEngine;

public class BalloonBehaviour : MonoBehaviour
{
    [SerializeField] private OfenManager leftOfen, rightOfen;

    [Header("Vertical Movement")]
    [Tooltip("Vertical speed (m/s) while at least one balloon is damaged or both ovens are at efficiency 1.")]
    [SerializeField, Min(0f)] private float sinkSpeed = 0.5f;
    [Tooltip("Vertical speed (m/s) while no balloon is damaged and at least one oven is at efficiency 2 or higher.")]
    [SerializeField, Min(0f)] private float riseSpeed = 0.5f;

    [Header("Runtime Variables")]
    [SerializeField] private float leftEfficiency;
    [SerializeField] private float rightEfficiency;
    [SerializeField] private float targetVerticalSpeed;

    private void FixedUpdate()
    {
        // Ship physics is server authoritative, so only the server applies lift.
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsSpawned) return;
        if (GameManager.Instance.CurrentState.Value != GameState.Playing) return;

        leftEfficiency = leftOfen != null ? leftOfen.Efficiency : 0f;
        rightEfficiency = rightOfen != null ? rightOfen.Efficiency : 0f;

        // A damaged balloon (efficiency 0) always makes the whole ship sink, even if the other one still burns.
        bool anyDamaged = leftEfficiency <= 0f || rightEfficiency <= 0f;
        bool anyBoosted = leftEfficiency >= 2f || rightEfficiency >= 2f;
        targetVerticalSpeed = !anyDamaged && anyBoosted ? riseSpeed : -sinkSpeed;

        GameManager.Instance.ApplyShipLift(leftEfficiency, rightEfficiency, targetVerticalSpeed);
    }
}
