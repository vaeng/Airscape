using System.Collections;
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

    public float minWindDuration = 2f; // Minimum duration of wind
    public float maxWindDuration = 5f; // Maximum duration of wind
    public float minWindInterval = 30f; // Minimum interval between wind events
    public float maxWindInterval = 70f; // Maximum interval between wind events

    [SerializeField] private ParticleSystem windEffect;

    private void OnTriggerStay(Collider other)
    {
        if (!IsWindy.Value) return;

        var item = other.GetComponent<AffectedByWindTag>();
        if (item == null) return; // Not affected by wind, ignore

        other.gameObject.GetComponent<CharacterController>().Move(Vector3.forward * windForce * Time.deltaTime);
    }

    public override void OnNetworkSpawn()
    {
        IsWindy.OnValueChanged += (_, won) =>
        {
            if (IsWindy.Value)
            {
                windEffect.Play();
            }
            else
            {
                windEffect.Stop();
            }
        };
        StartCoroutine(RandomWindStartUp());
    }

    IEnumerator RandomWindStartUp()
    {
        if(!IsServer) yield break;
        float duration = Random.Range(minWindDuration, maxWindDuration);
        Debug.Log($"WindyArea: Wind will be active for {duration} seconds.");
        IsWindy.Value = true;
        yield return new WaitForSeconds(duration);
        IsWindy.Value = false;
        float interval = Random.Range(minWindInterval, maxWindInterval);
        Debug.Log($"WindyArea: Wind will be inactive for {interval} seconds.");
        yield return new WaitForSeconds(interval);
        StartCoroutine(RandomWindStartUp());
    }


}
