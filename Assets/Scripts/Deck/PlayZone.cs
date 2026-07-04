using UnityEngine;

public class PlayZone : MonoBehaviour
{
    [SerializeField] private PlayerHand playerHand;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent(out Card card))
        {
            Debug.Log("Card entered");
            playerHand.PlayCard(card);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out Card card))
        {
            Debug.Log("Card left");
        }   
    }
}
