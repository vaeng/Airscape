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
    [SerializeField] private float liftIntegral;

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
    /// <paramref name="targetVerticalSpeed"/> (negative = sinking). The lift is split between the
    /// balloons by their efficiency and applied at each balloon, so a single working balloon pulls
    /// its side up and tilts the ship.
    /// </summary>
    public void ApplyShipLift(float leftEfficiency, float rightEfficiency, float targetVerticalSpeed)
    {
        if (!IsServer || shipRigidbody == null) return;

        float verticalSpeed = shipRigidbody.linearVelocity.y;
        float speedError = targetVerticalSpeed - verticalSpeed;

        // The integral builds up extra lift for loads the ship does not know about (money, items on deck).
        liftIntegral = Mathf.Clamp(liftIntegral + speedError * loadCompensation * Time.fixedDeltaTime, -maxLoadCompensation, maxLoadCompensation);

        float gravityCompensation = shipRigidbody.useGravity ? -Physics.gravity.y : 0f;
        float dampingCompensation = shipRigidbody.linearDamping * verticalSpeed;
        float acceleration = gravityCompensation + dampingCompensation + speedError * verticalResponsiveness + liftIntegral;
        float totalLift = Mathf.Max(0f, shipRigidbody.mass * acceleration);

        float efficiencySum = leftEfficiency + rightEfficiency;
        float leftShare = efficiencySum > 0f ? leftEfficiency / efficiencySum : 0.5f;

        shipRigidbody.AddForceAtPosition(Vector3.up * (totalLift * leftShare), balloonLeft.position, ForceMode.Force);
        shipRigidbody.AddForceAtPosition(Vector3.up * (totalLift * (1f - leftShare)), balloonRight.position, ForceMode.Force);
    }
    #endregion
}
