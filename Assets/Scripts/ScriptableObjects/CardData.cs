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

    [Header("Live combat metadata (definition only)")]
    public DungeonRun.Combat.TargetMode targetMode = DungeonRun.Combat.TargetMode.SingleOpponent;
    public DungeonRun.Combat.ActionIconKind presentationKind = DungeonRun.Combat.ActionIconKind.Attack;
    [Min(1)] public int hitCount = 1;
    [Min(0)] public int dodgeCount = 0;
    public DungeonRun.Combat.PiercingMode piercingMode = DungeonRun.Combat.PiercingMode.Normal;
    [Header("Bounded delayed modifier; one means no charge")]
    [Range(1, 8)] public int chargeMultiplier = 1;
    [Range(1, 3)] public int chargeTurns = 1;
}
