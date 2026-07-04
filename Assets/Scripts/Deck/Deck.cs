using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Deck : MonoBehaviour
{
    [SerializeField] private List<CardData> drawPile = new List<CardData>();
    [SerializeField] private GameObject cardBackPrefab;
    [SerializeField] private DiscardPile discardPile;

    private const float VERTICAL_SPACING = 0.1f;
    private void Start()
    {
        Shuffle(); 
        DeckDrawVisuals();
    }

    public CardData DrawCard()
    {
        if (drawPile.Count > 0)
        {
            int topIndex = drawPile.Count - 1;
            CardData drawnCard = drawPile[topIndex];
            drawPile.RemoveAt(topIndex);
            DeckDrawVisuals();
            return drawnCard;
        }

        return null;
    }

    public void ReshuffleFromDiscardPile()
    {
        discardPile.MoveCardsToDeck(drawPile);
        Shuffle();
        DeckDrawVisuals();
    }

    private void DeckDrawVisuals()
    {
        foreach (Transform discardedCard in transform)
        {
            Destroy(discardedCard.gameObject);
        }
        for (int i = 0; i < drawPile.Count; i++)
        {
            GameObject newCardBack = Instantiate(cardBackPrefab, transform);
            newCardBack.GetComponent<SpriteRenderer>().sortingOrder = i;
            newCardBack.transform.localPosition = new Vector3(0f, -i * VERTICAL_SPACING, 0f);
        }
    }

    public void Shuffle()
    {
        for (int i = 0; i < drawPile.Count; i++)
        {
            CardData card = drawPile[i];
            int randomIndex = Random.Range(i, drawPile.Count);
            drawPile[i] = drawPile[randomIndex];
            drawPile[randomIndex] = card;
        }
    }

    private void OnMouseDown()
    {
        if (drawPile.Count <= 0)
        {
            return;
        }
        if(TurnSystem.Instance.HasActionsRemaining())
        {
             PlayerEvents.DrawCardRequested();
        }
    }
}
