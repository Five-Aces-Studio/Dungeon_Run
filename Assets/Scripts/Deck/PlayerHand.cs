using System.Collections.Generic;
using UnityEngine;

public class PlayerHand : MonoBehaviour
{
    [SerializeField] private Deck deck;
    [SerializeField] private Transform[] cardSlots;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private int startingHandSize = 2;
    [SerializeField] private DiscardPile discardPile;
    
    private List<Card> cardsInHand = new List<Card>();

    private void OnEnable()
    {
        TurnEvents.OnPlayerTurnStart += EnableHand;
        TurnEvents.OnPlayerTurnEnd += DisableHand;
        PlayerEvents.OnDrawCardRequested += DrawNextCard;
    }    

    private void OnDisable()
    {
        TurnEvents.OnPlayerTurnStart -= EnableHand;
        TurnEvents.OnPlayerTurnEnd -= DisableHand;
        PlayerEvents.OnDrawCardRequested -= DrawNextCard;
    }    

    private void Start()
    {
        for (int i = 0; i < startingHandSize; i++)
        {
            DrawNextCard();
        }
    }

    private void EnableHand()
    {
        foreach(Card card in cardsInHand)
        {
            card.SetInteractable(true);
        }
    }

    private void DisableHand()
    {
        foreach(Card card in cardsInHand)
        {
            card.SetInteractable(false);
        }
    }

    public void DrawNextCard()
    {
        if (cardSlots == null || cardsInHand.Count >= cardSlots.Length)
        {
            return;
        }
        
        CardData cardData = deck.DrawCard();
        
        if(cardData == null)
        {
            return;
        }
        
        int slotIndex = cardsInHand.Count;
        GameObject newCard = Instantiate(cardPrefab, cardSlots[slotIndex].position, Quaternion.identity);
        Card cardComponent = newCard.GetComponent<Card>();
        cardComponent.LoadCardData(cardData);
        cardsInHand.Add(cardComponent);
        cardsInHand[slotIndex].transform.SetParent(cardSlots[slotIndex]);
        if (!TurnSystem.Instance.HasActionsRemaining())
        {
            cardComponent.SetInteractable(false);
        }
    }

    public void PlayCard(Card card)
    {
        cardsInHand.Remove(card);
        discardPile.DiscardCard(card.GetCardData());
        Destroy(card.gameObject);
        RepositionCards();
        PlayerEvents.CardPlayed(card.GetCardData());
    }

    private void RepositionCards()
    {
        //Unparent each card from current slot
        for (int i = 0; i < cardsInHand.Count; i++)
        {
            cardsInHand[i].transform.SetParent(null);
        }

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            cardsInHand[i].transform.SetParent(cardSlots[i]);
            cardsInHand[i].transform.position = cardSlots[i].position;
        }
    }
}
