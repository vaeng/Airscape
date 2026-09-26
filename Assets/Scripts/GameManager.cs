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

        // Already Playing when this spawns (shouldn't happen, but guard anyway).
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

    [Rpc(SendTo.Server)]
    public void HandleShipPhysicsRPC(float leftBallLift, float rightBallLift, 
        Vector3 position = default, Vector3 linearVelocity = default, Quaternion rotation = default, Vector3 angularVelocity = default)
    {
        if (!IsServer) return;

        // Debug.Log($"Lift RPC: {leftBallLift}, {rightBallLift}");
        shipRigidbody.AddForceAtPosition(Vector3.up * leftBallLift, balloonLeft.position, ForceMode.Force);
        shipRigidbody.AddForceAtPosition(Vector3.up * rightBallLift, balloonRight.position, ForceMode.Force);

    }
    #endregion
}
