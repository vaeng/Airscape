using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked chest that hands out money bundles to interacting players.
/// </summary>
public class MoneyChest : NetworkBehaviour
{
    [SerializeField] private GameObject moneyPrefab;

    // Amount left in the chest. Taking money out lowers only this, not GameManager.CurrentCash.
    public NetworkVariable<int> CurrentAmount = new(
        GameManager.StartingCash,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Set at spawn — the field initializer is overridden by the value Unity serialized into the prefab.
    public override void OnNetworkSpawn()
    {
        if (IsServer) CurrentAmount.Value = GameManager.StartingCash;
    }

    [Rpc(SendTo.Server)]
    public void TakeCashRpc(ulong clientId)
    {
        int value = moneyPrefab.GetComponent<MoneyBehaviour>().Value;
        if (CurrentAmount.Value < value) return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client)
            || client.PlayerObject == null) return;

        Pose holdPose = moneyPrefab.GetComponent<PickupItem>().GetHoldPose(client.PlayerObject.transform);
        GameObject money = Instantiate(moneyPrefab, holdPose.position, holdPose.rotation);
        NetworkObject netObj = money.GetComponent<NetworkObject>();
        netObj.Spawn();

        CurrentAmount.Value -= value;

        money.GetComponent<PickupItem>().ServerPickUp(clientId);
        GiveToClientRpc(netObj, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void GiveToClientRpc(NetworkObjectReference moneyRef, RpcParams rpcParams)
    {
        if (!moneyRef.TryGet(out NetworkObject moneyObj)) return;

        NetworkObject player = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (player == null) return;

        player.GetComponent<PlayerInteraction>()?.ReceiveHeldItem(moneyObj.GetComponent<PickupItem>());
    }
}
