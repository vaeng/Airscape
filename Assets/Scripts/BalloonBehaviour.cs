using UnityEngine;

public class BalloonBehaviour : MonoBehaviour
{
    [SerializeField] private OfenManager leftOfen, rightOfen;

    [SerializeField] private float leftLiftForce;
    [SerializeField] private float rightLiftForce;
    
    private void Update()
    {
        // RPCs can only be sent once the NetworkManager is running and the GameManager is spawned.
        if (GameManager.Instance == null || !GameManager.Instance.IsSpawned) return;
        if (GameManager.Instance.CurrentState.Value != GameState.Playing) return;

        leftLiftForce = leftOfen != null ? leftOfen.GetEnergy() : 0f;
        rightLiftForce = rightOfen != null ? rightOfen.GetEnergy() : 0f;

        GameManager.Instance.HandleShipPhysicsRPC(leftLiftForce, rightLiftForce);
        //Debug.Log("Trylift: L: " + leftLiftForce + ", R: " + rightLiftForce);
    }
}
