using System;
using UnityEngine;

public enum TriOrientation { Up, Down }

public enum CellType { Normal, Combat, Item, Event, Shop }

[Serializable]
public class TileWFC : MonoBehaviour
{
    [Header("Orientación")]
    public TriOrientation orientation = TriOrientation.Up;

    [Header("Tipo de celda")]
    public CellType type = CellType.Normal;

    [Header("Weight (Frequency)")] [Range(1, 100)]
    public int Weight = 1;
}