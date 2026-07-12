using System;
using UnityEngine;

public class CellWFC
{
    [Header("Tiles")]
    public TileWFC[] tileOptions;
    public TileWFC selectedTile;

    [Header("Position")]
    public int q; // column
    public int r; // row

    [Header("State")]
    public bool collapsed;
    public bool instantiated;
}

[Serializable]
public class CellWFCData
{
    public int q;
    public int r;
    public bool collapsed;
    public bool instantiated;
    public int selectedTileIndex;
}