using UnityEngine;

public class BalloonBehaviour : MonoBehaviour
{
    [SerializeField] private float liftForce = 100f;
    
    private void Update()
    {
        if (GameManager.Instance.CurrentState.Value != GameState.Playing) return;

        GameManager.Instance.HandleShipPhysicsRPC(liftForce, liftForce);
        Debug.Log("Trylift");
    }
}
