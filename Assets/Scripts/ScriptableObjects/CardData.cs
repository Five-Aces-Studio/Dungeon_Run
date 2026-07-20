using UnityEngine;

[CreateAssetMenu(fileName= "CardData", menuName = "ScriptableObjects/CardData")]
public class CardData : ScriptableObject
{
    [Header("General")]
    public string cardName;
    public string description;
    public int actionCost;
    public Sprite illustration;
    public bool multipleTurns;
    [HideInInspector] public int remainingTurns;

    [Header("Stats")]
    public int attackPower;
    public int healPower;
    public int poisonPower;
    public int poisonTurns;
    public int defensePower;
}
