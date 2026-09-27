using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked item that can be picked up, held, and dropped by players.
/// </summary>
public class PickupItem : NetworkBehaviour
{
    [SerializeField]
    private float _holdDistance = 3f;

    // Must clear the holder's world-scale capsule radius (~1.6 m at scale 3.19).
    [SerializeField]
    private float _minHoldDistance = 1.8f;

    // Drop the item automatically if it is jammed at minimum distance for this long.
    [SerializeField]
    private float _stuckDropTime = 0.5f;

    // Impulse applied on a plain drop (charge = 0).
    [SerializeField]
    private float _dropForce = 4f;

    // Impulse applied on a fully charged throw (charge = 1).
    [SerializeField]
    private float _maxThrowForce = 25f;

    public float MaxThrowForce => _maxThrowForce;

    /// <summary>
    /// Impulse applied for the given throw charge (0 = plain drop, 1 = full throw).
    /// </summary>
    public float GetThrowForce(float throwCharge01) =>
        Mathf.Lerp(_dropForce, _maxThrowForce, Mathf.Clamp01(throwCharge01));

    public NetworkVariable<ulong> HeldBy = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Held rotation offset, replicated so non-holding clients can pose the item locally.
    private readonly NetworkVariable<Quaternion> _syncedHeldRotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Rigidbody _rb;
    private Collider _col;
    private Quaternion _heldRotation = Quaternion.identity;
    private Transform _holderCamPoint;

    // Root of the holding player — used to exclude the holder from surface raycasts.
    private GameObject _holderRoot;

    private float _currentDist;
    private float _distVelocity;
    private float _stuckTimer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _currentDist = _holdDistance;
    }

    public override void OnNetworkSpawn()
    {
        HeldBy.OnValueChanged += OnHeldByChanged;
        OnHeldByChanged(ulong.MaxValue, HeldBy.Value);
    }

    public override void OnNetworkDespawn()
    {
        HeldBy.OnValueChanged -= OnHeldByChanged;
    }

    private void OnHeldByChanged(ulong previous, ulong current)
    {
        bool held = current != ulong.MaxValue;

        _col.enabled = !held;

        if (held)
        {
            // Clear velocities before making kinematic — Unity 6 blocks writes on kinematic bodies.
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.isKinematic = held;
        _rb.interpolation = held ? RigidbodyInterpolation.None
                                 : RigidbodyInterpolation.Interpolate;

        // Clients pose the held item themselves from the holder's transform — see LateUpdate.
        if (!IsServer)
        {
            var holder = held ? FindPlayerObject(current) : null;
            _holderRoot = holder != null ? holder.gameObject : null;
            _holderCamPoint = holder != null ? holder.transform.Find("CameraPoint") : null;
            _currentDist = _holdDistance;
            _distVelocity = 0f;
            if (!held) _heldRotation = Quaternion.identity;
        }

        // Keep the local player's held-item state in sync with the server's authority.
        var local = PlayerInteraction.Local;
        if (local == null) return;

        ulong me = NetworkManager.Singleton.LocalClientId;
        if (current == me) local.ReceiveHeldItem(this);
        else if (previous == me) local.ReleaseHeldItem(this);
    }

    /// <summary>
    /// Player object of the given client. SpawnManager.GetPlayerNetworkObject only returns the
    /// local player on clients, so search the spawned objects instead.
    /// </summary>
    private NetworkObject FindPlayerObject(ulong clientId)
    {
        foreach (var obj in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if (obj.IsPlayerObject && obj.OwnerClientId == clientId) return obj;
        }
        return null;
    }

    private void LateUpdate()
    {
        if (!IsSpawned || HeldBy.Value == ulong.MaxValue) return;

        if (IsServer) ServerUpdateHeld();
        else ClientUpdateHeld();
    }

    /// <summary>
    /// Server: authoritative held pose, plus auto-drop when stuck or the holder is gone.
    /// </summary>
    private void ServerUpdateHeld()
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(HeldBy.Value, out var client)
            || client.PlayerObject == null)
        {
            HeldBy.Value = ulong.MaxValue;
            return;
        }

        var camPoint = _holderCamPoint != null ? _holderCamPoint : client.PlayerObject.transform;
        float rawTarget = ComputeHoldTarget(camPoint);

        // Auto-drop when jammed at minimum distance — item is stuck against geometry.
        if (rawTarget <= _minHoldDistance + 0.01f)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= _stuckDropTime)
            {
                _holderCamPoint = null;
                _holderRoot = null;
                _heldRotation = Quaternion.identity;
                _syncedHeldRotation.Value = Quaternion.identity;
                _stuckTimer = 0f;
                HeldBy.Value = ulong.MaxValue;
                return;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }

        ApplyHeldPose(camPoint, client.PlayerObject.transform, rawTarget, _heldRotation);
    }

    /// <summary>
    /// Clients: pose the item from the holder's local transform instead of waiting for the server's
    /// NetworkTransform. NGO applies synced transforms before script LateUpdate, so this overrides
    /// them while held — the holder sees no round-trip lag, others see it glued to the holder.
    /// </summary>
    private void ClientUpdateHeld()
    {
        if (_holderRoot == null) return;

        var camPoint = _holderCamPoint != null ? _holderCamPoint : _holderRoot.transform;
        bool isMine = HeldBy.Value == NetworkManager.Singleton.LocalClientId;
        var rotation = isMine ? _heldRotation : _syncedHeldRotation.Value;

        ApplyHeldPose(camPoint, _holderRoot.transform, ComputeHoldTarget(camPoint), rotation);
    }

    /// <summary>
    /// Hold distance along the camera ray, pulled in when geometry is in the way.
    /// </summary>
    private float ComputeHoldTarget(Transform camPoint)
    {
        // RaycastAll so we can skip hits on the holder's own body before checking geometry.
        var hits = Physics.RaycastAll(camPoint.position, camPoint.forward, _holdDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (_holderRoot != null && h.collider.transform.IsChildOf(_holderRoot.transform))
                continue;

            return Mathf.Max(h.distance - 0.15f, _minHoldDistance);
        }

        return _holdDistance;
    }

    private void ApplyHeldPose(Transform camPoint, Transform playerRoot, float rawTarget, Quaternion heldRotation)
    {
        // Shrink quickly when approaching a surface, expand slowly when clearing one.
        float smoothTime = rawTarget < _currentDist ? 0.04f : 0.3f;
        _currentDist = Mathf.SmoothDamp(_currentDist, rawTarget, ref _distVelocity, smoothTime);

        transform.position = camPoint.position + camPoint.forward * _currentDist;
        transform.rotation = playerRoot.rotation * heldRotation;
    }

    /// <summary>
    /// Holder: applies the rotation locally right away and forwards it to the server.
    /// </summary>
    public void SetHeldRotation(Quaternion rotation)
    {
        _heldRotation = rotation;
        SetHeldRotationRpc(rotation);
    }

    [Rpc(SendTo.Server)]
    private void SetHeldRotationRpc(Quaternion rotation)
    {
        _heldRotation = rotation;
        _syncedHeldRotation.Value = rotation;
    }

    [Rpc(SendTo.Server)]
    public void PickUpRpc(ulong clientId) => ServerPickUp(clientId);

    /// <summary>
    /// World pose this item would have when held by the given player — safe to call on a prefab.
    /// </summary>
    public Pose GetHoldPose(Transform playerRoot)
    {
        Transform camPoint = playerRoot.Find("CameraPoint");
        if (camPoint == null) camPoint = playerRoot;

        return new Pose(camPoint.position + camPoint.forward * _holdDistance, playerRoot.rotation);
    }

    /// <summary>
    /// Server-only: attaches the item to the given client's player.
    /// </summary>
    public void ServerPickUp(ulong clientId)
    {
        if (!IsServer || HeldBy.Value != ulong.MaxValue) return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)
            || client.PlayerObject == null) return;

        _holderCamPoint = client.PlayerObject.transform.Find("CameraPoint");
        _holderRoot = client.PlayerObject.gameObject;
        _currentDist = _holdDistance;
        _distVelocity = 0f;
        _stuckTimer = 0f;
        HeldBy.Value = clientId;


        // Trigger the grab animation if the player has an AnimationSystem component.
        if (_holderRoot.TryGetComponent<AnimationSystem>(out AnimationSystem animationSystem))
        {
            if (transform.childCount > 0)
            {
                if (transform.childCount > 1)
                {
                    animationSystem.Grab(transform.GetChild(0), transform.GetChild(1));
                }
                else
                {
                    animationSystem.Grab(transform.GetChild(0));
                }
            }
        }
    }

    /// <summary>
    /// Releases the item. throwCharge01 = 0 is a plain drop, 1 is a full-strength throw.
    /// Sends the holder's local pose and aim, so the item leaves where the holder saw it.
    /// </summary>
    public void Drop(float throwCharge01)
    {
        Vector3 throwDir = _holderCamPoint != null ? _holderCamPoint.forward : transform.forward;
        DropRpc(throwCharge01, transform.position, transform.rotation, throwDir);
    }

    [Rpc(SendTo.Server)]
    private void DropRpc(float throwCharge01, Vector3 position, Quaternion rotation, Vector3 throwDir)
    {
        if (HeldBy.Value == ulong.MaxValue) return;

        // The server sees the holder slightly in the past — trust the holder's pose within reason.
        if ((position - transform.position).sqrMagnitude < 4f * _holdDistance * _holdDistance)
            transform.SetPositionAndRotation(position, rotation);

        throwDir = throwDir.sqrMagnitude > 0.0001f ? throwDir.normalized : transform.forward;

        // Trigger the grab animation if the player has an AnimationSystem component.
        if (_holderRoot != null && _holderRoot.TryGetComponent<AnimationSystem>(out AnimationSystem animationSystem))
        {
            animationSystem.Release();
        }

        _holderCamPoint = null;
        _holderRoot = null;
        _heldRotation = Quaternion.identity;
        _syncedHeldRotation.Value = Quaternion.identity;
        _stuckTimer = 0f;
        HeldBy.Value = ulong.MaxValue;
        Physics.SyncTransforms();
        _rb.AddForce(throwDir * GetThrowForce(throwCharge01), ForceMode.Impulse);
    }
}
