using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WFCObject
{
    public GameObject Prefab; 
    public int UpSocketID;      
    public int DownSocketID;   
    public int LeftSocketID;   
    public int RightSocketID;   
    public int maxInstances;  
}

[Serializable]
public class WFCObjectInstance
{
    public int Index; 
    public Vector3 Position; 
}

[Serializable]
public class MapData
{
    public List<CellWFCData> Grid; 
    public WFCObjectInstance[] Objects;
}