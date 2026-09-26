using UnityEngine;

public class MoneyBehaviour : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Ofen"))
        {
            Debug.Log("Burned da money");
            Destroy(gameObject);
        }
    }
}
