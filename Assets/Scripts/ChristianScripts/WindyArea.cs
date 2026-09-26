using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Provides a windy area that affects players with the AffectedByWindTag component. When a player enters the area, they will be pushed forward by a specified wind force.
/// The wind effect is only applied if the IsWindy NetworkVariable is set to true
/// </summary>
public class WindyArea : NetworkBehaviour
{
    public NetworkVariable<bool> IsWindy = new NetworkVariable<bool>(
        false, //init as false
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public float windForce = 10f; // The force of the wind


    private void OnTriggerStay(Collider other)
    {
        if (!IsWindy.Value) return;

        var item = other.GetComponent<AffectedByWindTag>();
        if (item == null) return; // Not affected by wind, ignore

        other.gameObject.GetComponent<CharacterController>().Move(Vector3.forward * windForce * Time.deltaTime);
    }

    

}
