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
    [Tooltip("Upward force of ONE balloon per oven efficiency level, as a share of the ship's weight. Index 0 = damaged balloon, 1..3 = efficiency level. Both balloons together above 1 = the ship rises.")]
    [SerializeField] private float[] liftPerLevel = { 0f, 0.45f, 0.75f, 0.95f };
    [Tooltip("Air resistance against vertical movement (1/s), on top of the Rigidbody's linear damping. Higher = slower rising/sinking.")]
    [SerializeField, Min(0f)] private float verticalDrag = 2f;
    [Tooltip("Mass (kg) each player standing on the ship pushes down with. Ship mass for comparison: see its Rigidbody.")]
    [SerializeField, Min(0f)] private float playerMass = 7f;
    [Tooltip("How far below the player's feet the ship deck is searched.")]
    [SerializeField, Min(0f)] private float playerGroundCheckDistance = 0.5f;
    [Tooltip("Runtime: lift of both balloons as a share of the ship's weight. Above 1 = rising (without players/cargo).")]
    [SerializeField] private float totalLiftShare;
    [Tooltip("Logs the ship's lift and rigidbody state once per second (server only).")]
    [SerializeField] private bool logLiftDebug = true;
    private float _nextLiftLog, _lastLiftTime;

    private readonly RaycastHit[] _groundHits = new RaycastHit[8];

    public Rigidbody ShipRigidbody => shipRigidbody;

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
    /// Server only, call from FixedUpdate. Every balloon pushes the ship up at its own position with a force
    /// depending on its oven efficiency (0 = damaged). Whether the ship rises, hovers or sinks follows
    /// from lift vs. weight (ship, players, cargo), so a weaker balloon lets its side drop.
    /// </summary>
    public void ApplyShipLift(float leftEfficiency, float rightEfficiency)
    {
        if (!IsServer) return;
        if (shipRigidbody == null || balloonLeft == null || balloonRight == null)
        {
            if (Time.time >= _nextLiftLog)
            {
                _nextLiftLog = Time.time + 1f;
                Debug.LogError("[ShipLift] GameManager is missing Ship Rigidbody, Balloon Left or Balloon Right. Assign them in the inspector.", this);
            }
            return;
        }

        float shipWeight = shipRigidbody.mass * -Physics.gravity.y;
        totalLiftShare = GetLiftShare(leftEfficiency) + GetLiftShare(rightEfficiency);
        shipRigidbody.AddForceAtPosition(Vector3.up * (shipWeight * GetLiftShare(leftEfficiency)), balloonLeft.position, ForceMode.Force);
        shipRigidbody.AddForceAtPosition(Vector3.up * (shipWeight * GetLiftShare(rightEfficiency)), balloonRight.position, ForceMode.Force);

        // Air resistance limits the vertical speed instead of a speed controller.
        float verticalSpeed = shipRigidbody.linearVelocity.y;
        shipRigidbody.AddForce(Vector3.down * (verticalSpeed * verticalDrag * shipRigidbody.mass), ForceMode.Force);

        ApplyPlayerWeight();

        _lastLiftTime = Time.time;
        if (logLiftDebug && Time.time >= _nextLiftLog)
        {
            _nextLiftLog = Time.time + 1f;
            Vector3 force = shipRigidbody.GetAccumulatedForce();
            Debug.Log($"[ShipLift] eff L/R {leftEfficiency}/{rightEfficiency}, lift {totalLiftShare:F2}x weight, " +
                      $"net force {force.y + shipWeight * (shipRigidbody.useGravity ? -1f : 0f):F1} N (mass {shipRigidbody.mass}), " +
                      $"vel.y {shipRigidbody.linearVelocity.y:F2}, pos.y {shipRigidbody.position.y:F2}, " +
                      $"kinematic {shipRigidbody.isKinematic}, constraints {shipRigidbody.constraints}, sleeping {shipRigidbody.IsSleeping()}", shipRigidbody);
        }
    }

    private void Update()
    {
        if (!logLiftDebug || !IsSpawned || !IsServer || CurrentState.Value != GameState.Playing) return;
        if (shipRigidbody != null && Time.time - _lastLiftTime > 1f && Time.time >= _nextLiftLog)
        {
            _nextLiftLog = Time.time + 1f;
            Debug.LogWarning("[ShipLift] ApplyShipLift is not being called. Is BalloonBehaviour active and are its ovens assigned?", this);
        }
    }

    /// <summary>
    /// Server only. Players move with a CharacterController and do not push rigidbodies, so their weight
    /// is applied manually where they stand, so they tilt the ship and count against the balloon lift.
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

    private float GetLiftShare(float efficiency)
    {
        if (liftPerLevel == null || liftPerLevel.Length == 0) return 0f;
        int level = Mathf.Clamp(Mathf.RoundToInt(efficiency), 0, liftPerLevel.Length - 1);
        return liftPerLevel[level];
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
