using System;
using NUnit.Framework.Constraints;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class Card : MonoBehaviour
{
    [SerializeField] private SpriteRenderer illustrationRenderer;
    [SerializeField] private TextMeshPro cardNameText;
    [SerializeField] private TextMeshPro descriptionText;
    [SerializeField] private TextMeshPro actionsText;

    [SerializeField] private float hoverScale = 2f;
    [SerializeField] private float hoverOffset = 2f;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    
    private SortingGroup sortingGroup;
    private int originalSortingOrder;
    private static bool isBeingDragged = false;
    private CardData cardData;
    private Collider2D cardCollider;

    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
        cardCollider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        originalPosition = transform.localPosition;
        originalScale = transform.localScale;
        originalSortingOrder = sortingGroup.sortingOrder;
    }

    public void LoadCardData(CardData data)
    {
        this.cardData = data;
        illustrationRenderer.sprite = data.illustration;
        cardNameText.text = data.cardName;
        descriptionText.text = data.description;
        actionsText.text = data.actionCost.ToString();
    }

    private void OnMouseEnter()
    {
        if (isBeingDragged)
        {
            return;
        }
        transform.localScale = originalScale * hoverScale;
        transform.localPosition += new Vector3(0f, hoverOffset, 0f);
        sortingGroup.sortingOrder += 1;
    }
    
    private void OnMouseExit()
    {
        if (isBeingDragged)
        {
            return;
        }
        transform.localScale = originalScale;
        transform.localPosition = originalPosition;
        sortingGroup.sortingOrder = originalSortingOrder;
    }

    private void OnMouseDrag()
    {
        isBeingDragged = true;
        gameObject.transform.position = GetMousePosition();
    }

    void OnMouseUp()
    {
        isBeingDragged = false;
        transform.localScale = originalScale;
        transform.localPosition = originalPosition;
        sortingGroup.sortingOrder = originalSortingOrder;
    }

    private Vector3 GetMousePosition()
    {
        Vector3 mousePosition = Mouse.current.position.ReadValue();
        mousePosition.z = transform.position.z - Camera.main.transform.position.z;
        return Camera.main.ScreenToWorldPoint(mousePosition);
    }

    public CardData GetCardData() => cardData;

    private void OnDestroy()
    {
        isBeingDragged = false;        
    }

    public void SetInteractable(bool interactable)
    {
        cardCollider.enabled = interactable;
    }
}
