using UnityEngine;
using Unity.Netcode;

public class MoneyBehaviour : NetworkBehaviour
{
    [SerializeField] private AudioClip moneyPickupSFX;
    [SerializeField] private AudioClip moneyDropSFX;
    private AudioSource audioSource;
    private PickupItem pickupItem;

    [SerializeField] private int value = 1000;
    [SerializeField] private float energyAmount = 20f;

    public int Value => value;
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        pickupItem = GetComponent<PickupItem>();
    }

    public override void OnNetworkSpawn()
    {
        if (pickupItem != null)
        {
            pickupItem.HeldBy.OnValueChanged += OnHeldByChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (pickupItem != null)
        {
            pickupItem.HeldBy.OnValueChanged -= OnHeldByChanged;
        }
    }

    private void OnHeldByChanged(ulong previous, ulong current)
    {
        // Runs on every peer, so all players hear the pickup.
        if (previous == ulong.MaxValue && current != ulong.MaxValue)
        {
            audioSource.PlayOneShot(moneyPickupSFX);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {

        if (!IsServer)
        {
            return;
        }

        var ofenManager = collision.collider.gameObject.GetComponentInParent<OfenManager>();
        if (ofenManager == null)
        {
            audioSource.PlayOneShot(moneyDropSFX);
        }
        else
        {
            ofenManager.PlayMoneyBurnSFX();
            ofenManager.AddEnergy(energyAmount);
            ofenManager.InsertMoney();
            Debug.Log($"Added {energyAmount} energy to the oven.");
            ReduceCashAmount();
            NetworkObject.Despawn();
        }

        
    }

    public void ReduceCashAmount()
    {
        if (!IsServer)
        {
            return;
        }

        GameManager.Instance.LoseCashRPC(value);
        EventManager.CashAmountChanged();
    }
}
