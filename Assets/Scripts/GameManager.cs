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
    [Tooltip("Upward force of ONE balloon per oven efficiency level, as a share of the total weight (ship + players on deck). Index 0 = damaged balloon, 1..3 = efficiency level. Both balloons together 1 = the ship holds its height, above 1 = it rises, below 1 = it sinks. Keep level 3 below 1, so a single balloon can never carry the ship.")]
    [SerializeField] private float[] liftPerLevel = { 0f, 0.5f, 0.65f, 0.8f };
    [Tooltip("Rising speed (units/s) per 1.0 of lift above the weight. E.g. 10: lift 1.5 rises with 5 units/s.")]
    [SerializeField, Min(0f)] private float verticalSpeedPerLift = 10f;
    [Tooltip("Sinking speed (units/s) per 1.0 of lift below the weight. E.g. 30: lift 0.8 sinks with 6 units/s, lift 0 with 30 units/s.")]
    [SerializeField, Min(0f)] private float sinkSpeedPerLift = 30f;
    [Tooltip("How fast the ship reaches its rising/sinking speed (1/s). Higher = snappier.")]
    [SerializeField, Min(0.1f)] private float verticalResponse = 2f;
    [Tooltip("Amplifies the torque of balloons and players. Higher = the ship tilts faster (the final angle stays the same).")]
    [SerializeField, Min(1f)] private float tiltTorqueMultiplier = 8f;
    [Tooltip("Mass (kg) each player standing on the ship pushes down with. Only tilts the ship, the balloons carry it. Higher = stronger tilt.")]
    [SerializeField, Min(0f)] private float playerMass = 10f;
    [Tooltip("How far below the player's feet the ship deck is searched.")]
    [SerializeField, Min(0f)] private float playerGroundCheckDistance = 0.5f;
    [Tooltip("Runtime: lift of both balloons as a share of the ship's weight. Above 1 = rising (without players/cargo).")]
    [SerializeField] private float totalLiftShare;
    [Tooltip("Logs the ship's lift and rigidbody state once per second (server only).")]
    [SerializeField] private bool logLiftDebug = true;
    private float _nextLiftLog, _lastLiftTime;
    private Vector3 _tiltTorque;

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
    /// depending on its oven efficiency (0 = damaged). The lift is measured against the ship plus the players on deck,
    /// so both balloons at level 1 hold the height, higher levels rise and a damaged balloon always sinks the ship
    /// (the higher the other balloon, the slower). A single lifting balloon rolls the ship heavily towards the damaged side,
    /// players tilt it where they stand.
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

        _tiltTorque = Vector3.zero;
        float shipWeight = shipRigidbody.mass * -Physics.gravity.y;
        float playerWeight = ApplyPlayerWeight();
        float totalWeight = shipWeight + playerWeight;
        totalLiftShare = GetLiftShare(leftEfficiency) + GetLiftShare(rightEfficiency);
        AddShipForce(Vector3.up * (totalWeight * GetLiftShare(leftEfficiency)), balloonLeft.position);
        AddShipForce(Vector3.up * (totalWeight * GetLiftShare(rightEfficiency)), balloonRight.position);

        // The balloon and player forces give the tilt, it is amplified so the ship reacts quickly.
        shipRigidbody.AddTorque(_tiltTorque * (tiltTorqueMultiplier - 1f), ForceMode.Force);

        // Vertical movement: replace the net lift vs. weight at the center of mass with a force that drives the ship
        // towards a speed depending on the lift, so rising/sinking is easy to tune and does not depend on the mass.
        float verticalSpeed = shipRigidbody.linearVelocity.y;
        float targetSpeed = (totalLiftShare - 1f) * (totalLiftShare < 1f ? sinkSpeedPerLift : verticalSpeedPerLift);
        float netLift = totalWeight * (totalLiftShare - 1f);
        float correction = shipRigidbody.mass * (verticalResponse * (targetSpeed - verticalSpeed) + shipRigidbody.linearDamping * verticalSpeed);
        shipRigidbody.AddForce(Vector3.up * (correction - netLift), ForceMode.Force);

        _lastLiftTime = Time.time;
        if (logLiftDebug && Time.time >= _nextLiftLog)
        {
            _nextLiftLog = Time.time + 1f;
            Debug.Log($"[ShipLift] state {CurrentState.Value}, eff L/R {leftEfficiency}/{rightEfficiency}, lift {totalLiftShare:F2}x weight, " +
                      $"target vel.y {targetSpeed:F2}, players {playerWeight / -Physics.gravity.y:F1} kg, roll {Mathf.DeltaAngle(0f, shipRigidbody.rotation.eulerAngles.z):F1}°, " +
                      $"vel.y {shipRigidbody.linearVelocity.y:F2}, pos.y {shipRigidbody.position.y:F2}, " +
                      $"kinematic {shipRigidbody.isKinematic}, constraints {shipRigidbody.constraints}, sleeping {shipRigidbody.IsSleeping()}", shipRigidbody);
        }
    }

    private void Update()
    {
        if (!logLiftDebug || !IsSpawned || !IsServer) return;
        if (shipRigidbody != null && Time.time - _lastLiftTime > 1f && Time.time >= _nextLiftLog)
        {
            _nextLiftLog = Time.time + 1f;
            Debug.LogWarning("[ShipLift] ApplyShipLift is not being called. Is BalloonBehaviour active and are its ovens assigned?", this);
        }
    }

    /// <summary>
    /// Server only. Players move with a CharacterController and do not push rigidbodies, so their weight
    /// is applied manually where they stand, so they tilt the ship. Returns the applied total weight (N).
    /// </summary>
    private float ApplyPlayerWeight()
    {
        if (playerMass <= 0f) return 0f;

        float totalWeight = 0f;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            if (!TryGetShipGroundPoint(client.PlayerObject.transform, out Vector3 point)) continue;

            AddShipForce(Physics.gravity * playerMass, point);
            totalWeight += playerMass * -Physics.gravity.y;
        }
        return totalWeight;
    }

    /// <summary>Applies a force at a point of the ship and remembers its torque for the tilt amplification.</summary>
    private void AddShipForce(Vector3 force, Vector3 point)
    {
        shipRigidbody.AddForceAtPosition(force, point, ForceMode.Force);
        _tiltTorque += Vector3.Cross(point - shipRigidbody.worldCenterOfMass, force);
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
