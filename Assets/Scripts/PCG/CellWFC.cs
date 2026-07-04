using System;
using UnityEngine;

public class CellWFC
{
    [Header("Tiles")]
    public TileWFC[] tileOptions;
    public TileWFC selectedTile;

    [Header("Position")]
    public int row;
    public int col;

    [Header("State")]
    public bool collapsed;
    public bool instantiated;
}

[Serializable]
public class CellWFCData
{
    public int row;
    public int col;
    public bool collapsed;
    public bool instantiated;
    public int selectedTileIndex;
}