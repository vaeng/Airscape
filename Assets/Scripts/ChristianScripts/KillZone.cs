using UnityEngine;
using Unity.Netcode;

public class KillZone : NetworkBehaviour
{
    private void OnTriggerExit(Collider other)
    {
        var pc = other.GetComponent<PlayerColour>();
        if (pc != null) {
            var id = pc.PlayerIndex.Value;
            var spawnPoint = FindFirstObjectByType<PlayerSpawnManager>().GetSpawnPoint(id);
            if (spawnPoint != null) {
                var cc = other.gameObject.GetComponent<CharacterController>();
                cc.enabled = false;
                other.gameObject.transform.position = spawnPoint.position +  Vector3.up * 1f;
                other.gameObject.transform.rotation = spawnPoint.rotation;
                cc.enabled = true;
                cc.SimpleMove(Vector3.zero);
                pc.Apply(id);
                pc.PlayRespawnEffect();
            }
        }

        var money = other.GetComponent<MoneyBehaviour>();
        if (money != null) {
            if(money.NetworkObject.IsSpawned)
            {
                money.ReduceCashAmount();
                money.NetworkObject.Despawn(true);
            }
        }

    }
}
