using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles player input for picking up, dropping, and rotating held items.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField]
    private float _rotateSensitivity = 0.15f;

    // Holding Attack longer than this while carrying an item turns the drop into a throw.
    [SerializeField]
    private float _throwChargeDelay = 0.2f;

    // Time from the charge delay until full throw strength is reached.
    [SerializeField]
    private float _throwChargeTime = 1f;

    private PlayerMovement _movement;
    private PickupItem _heldItem;
    private Quaternion _heldRotation = Quaternion.identity;

    // Per-player input actions — MPPM-safe.
    private InputAction _interactAction;
    private InputAction _attackAction;
    private InputAction _rotateAction;

    private bool _isChargingThrow;
    private float _chargeStartTime;

    // Repair in progress: the targeted repair point collider and when holding Interact started.
    private RepairSpawner _repairSpawner;
    private Collider _repairTarget;
    private float _repairStartTime;

    public bool IsRotatingItem { get; private set; }

    public bool IsRepairing => _repairSpawner != null;

    /// <summary>
    /// Repair progress (0..1) while Interact is held on a repair point.
    /// </summary>
    public float RepairProgress01 => IsRepairing
        ? Mathf.Clamp01((Time.time - _repairStartTime) / Mathf.Max(_repairSpawner.RepairDuration, 0.01f))
        : 0f;

    /// <summary>
    /// Whether the HUD charge bar should be shown (throw charge or repair progress).
    /// </summary>
    public bool IsChargeBarVisible => IsChargingThrow || IsRepairing;

    /// <summary>
    /// Fill of the HUD charge bar (0..1).
    /// </summary>
    public float ChargeBar01 => IsRepairing ? RepairProgress01
        : MaxThrowForce > 0f ? CurrentThrowForce / MaxThrowForce : 0f;

    /// <summary>
    /// Current throw charge (0..1) while Attack is held with an item — usable for HUD feedback.
    /// </summary>
    public float ThrowCharge01 => _isChargingThrow ? ComputeCharge() : 0f;

    public bool IsChargingThrow => _isChargingThrow && _heldItem != null;

    /// <summary>
    /// Impulse the held item would get if released now.
    /// </summary>
    public float CurrentThrowForce => IsChargingThrow ? _heldItem.GetThrowForce(ComputeCharge()) : 0f;

    public float MaxThrowForce => _heldItem != null ? _heldItem.MaxThrowForce : 0f;

    /// <summary>
    /// The locally owned player's interaction component, or null before spawn.
    /// </summary>
    public static PlayerInteraction Local { get; private set; }

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();

        // Right-click item rotation is not in the shared asset — bind inline.
        _rotateAction = new InputAction("RotateItem", InputActionType.Button);
        _rotateAction.AddBinding("<Mouse>/rightButton");
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        Local = this;

        var playerInput = GetComponent<PlayerInput>();
        _interactAction = playerInput.actions["Interact"];
        _attackAction = playerInput.actions["Attack"];
        _rotateAction.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            _rotateAction?.Disable();
        }
        if (Local == this) Local = null;
        _rotateAction?.Dispose();
    }

    private void Update()
    {
        HandleInteract();
        HandleThrow();
        HandleItemRotation();
    }

    /// <summary>
    /// Interact: picks up items with empty hands; held on a repair point while carrying money, it repairs it.
    /// </summary>
    private void HandleInteract()
    {
        if (_interactAction == null) return;

        if (_interactAction.WasPressedThisFrame())
        {
            if (_heldItem == null) TryPickUp();
            else if (!_isChargingThrow) TryStartRepair();
        }

        if (!IsRepairing) return;

        if (!_interactAction.IsPressed() || !IsStillTargetingRepair())
        {
            CancelRepair();
            return;
        }

        if (RepairProgress01 >= 1f)
        {
            // Money is consumed by the server; _heldItem turns null once it is despawned.
            _repairSpawner.Repair(_repairTarget.transform, _heldItem);
            CancelRepair();
        }
    }

    /// <summary>
    /// Attack: press to start charging, release to drop (short press) or throw (held).
    /// </summary>
    private void HandleThrow()
    {
        if (_attackAction == null) return;

        // Item was taken away while charging — cancel the throw.
        if (_isChargingThrow && _heldItem == null)
            _isChargingThrow = false;

        if (_attackAction.WasPressedThisFrame() && _heldItem != null && !IsRepairing)
        {
            // Start charging; the actual drop/throw happens on release.
            _isChargingThrow = true;
            _chargeStartTime = Time.time;
        }

        if (_isChargingThrow && _attackAction.WasReleasedThisFrame())
        {
            float charge = ComputeCharge();
            _isChargingThrow = false;
            DropItem(charge);
        }
    }

    private float ComputeCharge()
    {
        float held = Time.time - _chargeStartTime - _throwChargeDelay;
        if (held <= 0f) return 0f;
        return _throwChargeTime <= 0f ? 1f : Mathf.Clamp01(held / _throwChargeTime);
    }

    private void TryPickUp()
    {
        if (!TryGetInteractHit(out var hit)) return;

        var chest = hit.collider.GetComponentInParent<MoneyChest>();
        if (chest != null)
        {
            chest.TakeCashRpc(OwnerClientId);
            return;
        }

        var item = hit.collider.GetComponentInParent<PickupItem>();
        if (item == null) return;

        // _heldItem is set once the server confirms via PickupItem.HeldBy.
        item.PickUpRpc(OwnerClientId);
    }

    /// <summary>
    /// Starts repairing if the player carries money and looks at an active repair point.
    /// </summary>
    private void TryStartRepair()
    {
        if (!TryGetInteractHit(out var hit)) return;

        var spawner = hit.collider.GetComponentInParent<RepairSpawner>();
        if (spawner == null || !spawner.CanRepair(hit.collider.transform, _heldItem)) return;

        _repairSpawner = spawner;
        _repairTarget = hit.collider;
        _repairStartTime = Time.time;
        _repairSpawner.StartRepairSound(_repairTarget.transform);
    }

    /// <summary>
    /// True while the player still carries the money and looks at the same, still active repair point.
    /// </summary>
    private bool IsStillTargetingRepair()
    {
        if (_repairTarget == null || !TryGetInteractHit(out var hit)) return false;
        return hit.collider == _repairTarget && _repairSpawner.CanRepair(_repairTarget.transform, _heldItem);
    }

    private void CancelRepair()
    {
        if (_repairSpawner != null && _repairTarget != null)
            _repairSpawner.StopRepairSound(_repairTarget.transform);

        _repairSpawner = null;
        _repairTarget = null;
    }

    /// <summary>
    /// First non-trigger hit along the interact ray, ignoring the player's own colliders.
    /// </summary>
    private bool TryGetInteractHit(out RaycastHit result)
    {
        var hits = Physics.RaycastAll(_movement.InteractRay, _movement.InteractRange,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;

            result = h;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Called on the owning client when the server assigns it an item (pickup or MoneyChest).
    /// </summary>
    public void ReceiveHeldItem(PickupItem item)
    {
        if (_heldItem != null || item == null) return;

        _heldItem = item;
        _heldRotation = Quaternion.identity;
    }

    /// <summary>
    /// Called on the owning client when the server released its item (e.g. stuck auto-drop).
    /// </summary>
    public void ReleaseHeldItem(PickupItem item)
    {
        if (_heldItem != item) return;

        _heldItem = null;
        _heldRotation = Quaternion.identity;
        _isChargingThrow = false;
    }

    private void DropItem(float throwCharge01)
    {
        _heldItem.DropRpc(throwCharge01);
        _heldItem = null;
        _heldRotation = Quaternion.identity;
    }

    private void HandleItemRotation()
    {
        IsRotatingItem = _heldItem != null && _rotateAction != null && _rotateAction.IsPressed();

        if (!IsRotatingItem) return;

        // TODO: wire Look delta through if needed.
        var mouseAction = GetComponent<PlayerInput>()?.actions["Look"];
        if (mouseAction == null) return;

        var delta = mouseAction.ReadValue<Vector2>() * _rotateSensitivity;

        _heldRotation = Quaternion.AngleAxis(delta.x, Vector3.up)
                      * Quaternion.AngleAxis(-delta.y, Vector3.right)
                      * _heldRotation;

        _heldItem.SetHeldRotationRpc(_heldRotation);
    }
}
