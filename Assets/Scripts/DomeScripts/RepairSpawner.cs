using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class RepairSpawner : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Parent object. Every direct child is a spawn point, whose first child is the indicator that is only visible while the point is active.")]
    [SerializeField] private GameObject repairSpawns;
    [SerializeField] private GameObject smokeVFX;

    [Header("Settings")]
    [Tooltip("How many repair points can be active at the same time, across ALL spawners. Taken from the first spawned spawner.")]
    [SerializeField, Min(1)] private int maxActive = 1;
    [Tooltip("Seconds until a new repair point spawns while fewer than Max Active are active. Shared by all spawners, taken from the first spawned spawner.")]
    [SerializeField, Min(0.1f)] private float spawnInterval = 10f;
    [Tooltip("Seconds the player has to hold Interact with money on a repair point.")]
    [SerializeField, Min(0f)] private float repairDuration = 1.5f;

    [Header("Runtime Variables")]
    [SerializeField] private float spawnTimer;

    // Bit i is set when spawn point i is active (supports up to 64 spawn points).
    private readonly NetworkVariable<ulong> activeMask = new(0);

    private Transform[] _repairSpawnPoints = System.Array.Empty<Transform>();
    private GameObject[] _indicators = System.Array.Empty<GameObject>();
    private GameObject[] _smokes = System.Array.Empty<GameObject>();

    // Server only. All spawned spawners share one limit and one timer, driven by the first one.
    private static readonly List<RepairSpawner> AllSpawners = new();

    public int ActiveCount => CountBits(activeMask.Value);
    public float RepairDuration => repairDuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AllSpawners.Clear();

    private void Awake()
    {
        if (repairSpawns == null) return;

        var parent = repairSpawns.transform;
        int count = Mathf.Min(parent.childCount, 64);
        _repairSpawnPoints = new Transform[count];
        _indicators = new GameObject[count];
        _smokes = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            var point = parent.GetChild(i);
            _repairSpawnPoints[i] = point;

            if (point.childCount > 0)
                _indicators[i] = point.GetChild(0).gameObject;

            if (smokeVFX != null)
                _smokes[i] = Instantiate(smokeVFX, point.position, point.rotation, point);
        }

        UpdateVisuals(0);
    }

    public override void OnNetworkSpawn()
    {
        activeMask.OnValueChanged += OnActiveMaskChanged;
        UpdateVisuals(activeMask.Value);

        if (IsServer) AllSpawners.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        activeMask.OnValueChanged -= OnActiveMaskChanged;
        AllSpawners.Remove(this);
    }

    private void Update()
    {
        // Only the first spawner runs the shared timer for all of them.
        if (!IsSpawned || !IsServer || AllSpawners.Count == 0 || AllSpawners[0] != this) return;

        int totalActive = 0;
        foreach (var spawner in AllSpawners) totalActive += spawner.ActiveCount;

        if (totalActive >= maxActive)
        {
            spawnTimer = 0f;
            return;
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            ActivateRandomGlobal();
        }
    }

    /// <summary>Returns true if the given spawn point (or its indicator) is currently active.</summary>
    public bool IsActive(Transform spawnPoint)
    {
        int index = IndexOf(spawnPoint);
        return index >= 0 && (activeMask.Value & (1UL << index)) != 0;
    }

    /// <summary>True if <paramref name="heldItem"/> is money and <paramref name="target"/> belongs to an active spawn point.</summary>
    public bool CanRepair(Transform target, PickupItem heldItem)
    {
        return heldItem != null && heldItem.TryGetComponent<MoneyBehaviour>(out _) && IsActive(target);
    }

    /// <summary>
    /// Repairs the spawn point that <paramref name="target"/> belongs to, consuming the held money.
    /// Call from the owning client once the repair time has passed.
    /// </summary>
    public void Repair(Transform target, PickupItem heldItem)
    {
        if (!CanRepair(target, heldItem)) return;
        RepairRpc(IndexOf(target), heldItem.NetworkObject);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RepairRpc(int index, NetworkObjectReference moneyRef, RpcParams rpcParams = default)
    {
        if (index < 0 || index >= _repairSpawnPoints.Length) return;
        if ((activeMask.Value & (1UL << index)) == 0) return;
        if (!moneyRef.TryGet(out var moneyObj)) return;

        // Only the player actually holding the money may use it.
        var pickup = moneyObj.GetComponent<PickupItem>();
        var money = moneyObj.GetComponent<MoneyBehaviour>();
        if (pickup == null || money == null || pickup.HeldBy.Value != rpcParams.Receive.SenderClientId) return;

        Deactivate(index);
        money.ReduceCashAmount();
        moneyObj.Despawn();
    }

    /// <summary>Server only. Activates a random inactive spawn point out of all spawners.</summary>
    private static void ActivateRandomGlobal()
    {
        var inactive = new List<(RepairSpawner spawner, int index)>();
        foreach (var spawner in AllSpawners)
        {
            for (int i = 0; i < spawner._repairSpawnPoints.Length; i++)
            {
                if ((spawner.activeMask.Value & (1UL << i)) == 0) inactive.Add((spawner, i));
            }
        }
        if (inactive.Count == 0) return;

        var (target, index) = inactive[Random.Range(0, inactive.Count)];
        target.activeMask.Value |= 1UL << index;
    }

    /// <summary>Server only.</summary>
    private void Deactivate(int index)
    {
        if (index < 0 || index >= _repairSpawnPoints.Length) return;
        activeMask.Value &= ~(1UL << index);
    }

    /// <summary>Finds the spawn point index for a spawn point or any of its children.</summary>
    private int IndexOf(Transform t)
    {
        for (int i = 0; i < _repairSpawnPoints.Length; i++)
        {
            if (t != null && t.IsChildOf(_repairSpawnPoints[i])) return i;
        }
        return -1;
    }

    private void OnActiveMaskChanged(ulong previous, ulong current)
    {
        UpdateVisuals(current);
    }

    private void UpdateVisuals(ulong mask)
    {
        for (int i = 0; i < _repairSpawnPoints.Length; i++)
        {
            bool active = (mask & (1UL << i)) != 0;
            if (_indicators[i] != null) _indicators[i].SetActive(active);
            if (_smokes[i] != null) _smokes[i].SetActive(active);
        }
    }

    private static int CountBits(ulong value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }
        return count;
    }
}
