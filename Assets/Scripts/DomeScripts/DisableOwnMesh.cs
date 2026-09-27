using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Hides the mesh for the local player once the game has started. Other players still see it.
/// </summary>
public class DisableOwnMesh : NetworkBehaviour
{
    [SerializeField] private SkinnedMeshRenderer _meshRenderer;

    public override void OnNetworkSpawn()
    {
        GameManager.OnGameStarted += UpdateVisibility;
        UpdateVisibility();
    }

    public override void OnNetworkDespawn()
    {
        GameManager.OnGameStarted -= UpdateVisibility;
    }

    public override void OnDestroy()
    {
        GameManager.OnGameStarted -= UpdateVisibility;
        base.OnDestroy();
    }

    public override void OnGainedOwnership() => UpdateVisibility();
    public override void OnLostOwnership() => UpdateVisibility();

    private void UpdateVisibility()
    {
        if (_meshRenderer == null) return;

        // Late joiners: the game may already be running when we spawn.
        bool isPlaying = GameManager.Instance != null
            && GameManager.Instance.CurrentState.Value == GameState.Playing;

        _meshRenderer.enabled = !(IsOwner && isPlaying);
    }
}
