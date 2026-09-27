using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bridges PlayerMovement with NGO ownership and game state events.
/// </summary>
public class PlayerNetworkBridge : NetworkBehaviour
{
    private PlayerMovement _movement;

    private void Awake() => _movement = GetComponent<PlayerMovement>();

    /// <summary>
    /// Handling logic for not allowing the player to roam in the menu state.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Remote players must be on Default layer so other FPS cameras can see them.
            int defaultLayer = LayerMask.NameToLayer("Default");
            foreach (var t in GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = defaultLayer;

            var pi = GetComponent<PlayerInput>();
            if (pi != null) pi.enabled = false;

            _movement.enabled = false;
            IgnoreShipCollision();
            return;
        }

        GameManager.OnGameStarted += _movement.StartPlaying;

        // Already playing (e.g. reconnect or late spawn).
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState.Value == GameState.Playing)
            _movement.StartPlaying();
    }

    /// <summary>
    /// Remote players arrive slightly delayed. Their CharacterController blocks rigidbodies like a wall,
    /// so the rising ship would get stuck on them. They move with the synced transform anyway.
    /// </summary>
    private void IgnoreShipCollision()
    {
        var cc = GetComponent<CharacterController>();
        var ship = GameManager.Instance != null ? GameManager.Instance.ShipRigidbody : null;
        if (cc == null || ship == null) return;

        foreach (var col in ship.GetComponentsInChildren<Collider>(true))
            Physics.IgnoreCollision(cc, col);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        GameManager.OnGameStarted -= _movement.StartPlaying;
    }
}
