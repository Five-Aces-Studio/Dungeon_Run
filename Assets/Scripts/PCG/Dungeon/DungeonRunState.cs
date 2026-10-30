using System;

/// <summary>Exploration progress; health, cards and combat remain owned by the existing player systems.</summary>
[Serializable]
public sealed class DungeonRunState
{
    public int masterSeed, floorsSinceBoss;
    public int floorNumber = 1;
    public bool isBossFloor, hasKey, doorsOpen, nextFloorRolled, nextIsBoss, bossDefeated;
    public bool CanDescend => doorsOpen && nextFloorRolled && (!isBossFloor || bossDefeated);

    public bool CollectKey(float initial, float increase, float maximum)
    {
        if (nextFloorRolled) return false;
        if (float.IsNaN(initial) || float.IsNaN(increase) || float.IsNaN(maximum) || float.IsInfinity(increase) ||
            initial < 0 || increase < 0 || maximum < initial || maximum > 1)
            throw new ArgumentException("Invalid boss probability configuration.");
        double chance = Math.Min(maximum, initial + (double)increase * floorsSinceBoss);
        var random = new Random(unchecked(masterSeed ^ (floorNumber * 486187739) ^ 0x5C39A17));
        nextIsBoss = !isBossFloor && random.NextDouble() < chance;
        hasKey = nextFloorRolled = true;
        return true;
    }
    public DungeonRunState Next(bool resetAfterBoss)
    {
        if (!CanDescend) throw new InvalidOperationException("The exit is not ready.");
        return new DungeonRunState { masterSeed = masterSeed, floorNumber = checked(floorNumber + 1),
            isBossFloor = nextIsBoss, floorsSinceBoss = isBossFloor && resetAfterBoss ? 0 : floorsSinceBoss + 1 };
    }
}
