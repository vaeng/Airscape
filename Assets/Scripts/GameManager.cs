using System;
using Unity.Netcode;
using UnityEngine;

public enum GameState { Lobby, Playing }

/// <summary>
/// Owns game phase state (Lobby/Playing) for all clients via NetworkVariable.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public const int StartingCash = 1000000;
    [SerializeField] private int currentCash = StartingCash;
    [SerializeField] private Rigidbody shipRigidbody;
    [SerializeField] private Transform balloonLeft, balloonRight;
    [Tooltip("How quickly the ship reaches its target vertical speed (1/s).")]
    [SerializeField, Min(0f)] private float verticalResponsiveness = 2f;
    [Tooltip("How quickly extra lift builds up to carry additional load on deck (money, items).")]
    [SerializeField, Min(0f)] private float loadCompensation = 3f;
    [Tooltip("Maximum extra upward acceleration (m/s²) the load compensation may add. ~10 = extra load up to the ship's own mass.")]
    [SerializeField, Min(0f)] private float maxLoadCompensation = 20f;
    [Tooltip("Maximum share of the total lift that may shift to one balloon. 0 = always 50/50 (no tilt), 0.5 = one balloon may carry everything.")]
    [SerializeField, Range(0f, 0.5f)] private float maxLiftImbalance = 0.1f;
    [Tooltip("Mass (kg) each player standing on the ship pushes down with. Ship mass for comparison: see its Rigidbody.")]
    [SerializeField, Min(0f)] private float playerMass = 7f;
    [Tooltip("How far below the player's feet the ship deck is searched.")]
    [SerializeField, Min(0f)] private float playerGroundCheckDistance = 0.5f;
    [SerializeField] private float liftIntegral;

    private readonly RaycastHit[] _groundHits = new RaycastHit[8];

    public NetworkVariable<int> CurrentCash = new(
        StartingCash,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public static GameManager Instance { get; private set; }

    // C# event — safe to subscribe before the network starts (LobbyController, FPSController).
    public static event Action OnGameStarted;

    public NetworkVariable<GameState> CurrentState = new NetworkVariable<GameState>(
        GameState.Lobby,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Awake() => Instance = this;

    #region Network Callbacks and RPCs
    public override void OnNetworkSpawn()
    {
        CurrentState.OnValueChanged += (_, next) =>
        {
            if (next == GameState.Playing) OnGameStarted?.Invoke();
        };

        if (CurrentState.Value == GameState.Playing) OnGameStarted?.Invoke();
    }

    [Rpc(SendTo.Server)]
    public void StartGameRpc()
    {
        if (!IsServer) return;
        CurrentState.Value = GameState.Playing;
    }
    #endregion

    #region Public Methods

    [Rpc(SendTo.Server)]
    public void LoseCashRPC(int amount)
    {
        CurrentCash.Value = Mathf.Max(0, CurrentCash.Value - amount);
    }

    /// <summary>
    /// Server only, call from FixedUpdate. Applies balloon lift so the ship moves vertically at
    /// <paramref name="targetVerticalSpeed"/> (negative = sinking). The lift is applied at each balloon
    /// and shifted slightly towards the more efficient one, so the ship tilts a little towards the weaker side.
    /// </summary>
    public void ApplyShipLift(float leftEfficiency, float rightEfficiency, float targetVerticalSpeed)
    {
        if (!IsServer || shipRigidbody == null) return;

        float verticalSpeed = shipRigidbody.linearVelocity.y;
        float speedError = targetVerticalSpeed - verticalSpeed;

        // The integral builds up extra lift for loads the ship does not know about (money, items on deck).
        // It never goes negative: while sinking is blocked (ground, collision) it would otherwise wind up
        // and cancel the lift for many seconds after the ship should rise again.
        liftIntegral = Mathf.Clamp(liftIntegral + speedError * loadCompensation * Time.fixedDeltaTime, 0f, maxLoadCompensation);

        float gravityCompensation = shipRigidbody.useGravity ? -Physics.gravity.y : 0f;
        float dampingCompensation = shipRigidbody.linearDamping * verticalSpeed;
        float acceleration = gravityCompensation + dampingCompensation + speedError * verticalResponsiveness + liftIntegral;
        float totalLift = Mathf.Max(0f, shipRigidbody.mass * acceleration);

        // Shift at most maxLiftImbalance of the lift to the stronger balloon. Putting all lift on one
        // balloon (e.g. the other one damaged) creates far more torque than the ship can counter and flips it.
        float strongest = Mathf.Max(leftEfficiency, rightEfficiency);
        float imbalance = strongest > 0f ? (leftEfficiency - rightEfficiency) / strongest : 0f;
        float leftShare = 0.5f + imbalance * maxLiftImbalance;

        shipRigidbody.AddForceAtPosition(Vector3.up * (totalLift * leftShare), balloonLeft.position, ForceMode.Force);
        shipRigidbody.AddForceAtPosition(Vector3.up * (totalLift * (1f - leftShare)), balloonRight.position, ForceMode.Force);

        ApplyPlayerWeight();
    }

    /// <summary>
    /// Server only. Players move with a CharacterController and do not push rigidbodies, so their weight
    /// is applied manually where they stand. The lift controller compensates the extra load, so players
    /// only tilt the ship without making it sink.
    /// </summary>
    private void ApplyPlayerWeight()
    {
        if (playerMass <= 0f) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            if (!TryGetShipGroundPoint(client.PlayerObject.transform, out Vector3 point)) continue;

            shipRigidbody.AddForceAtPosition(Physics.gravity * playerMass, point, ForceMode.Force);
        }
    }

    private bool TryGetShipGroundPoint(Transform player, out Vector3 point)
    {
        point = default;
        Vector3 origin = player.position + Vector3.up * 0.1f;
        float distance = 0.1f + playerGroundCheckDistance;

        if (player.TryGetComponent<CharacterController>(out var cc))
        {
            // Start at the capsule center: rays starting inside the player's own collider ignore it.
            origin = cc.bounds.center;
            distance = cc.bounds.extents.y + playerGroundCheckDistance;
        }

        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (_groundHits[i].rigidbody != shipRigidbody) continue;
            point = _groundHits[i].point;
            return true;
        }
        return false;
    }
    #endregion
}
