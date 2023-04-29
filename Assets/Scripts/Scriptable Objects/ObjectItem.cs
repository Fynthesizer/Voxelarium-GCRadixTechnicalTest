using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Object", menuName = "Items/Object")]
public class ObjectItem : Item
{
    [Header("Object Properties")]
    public int stackSize;
    public GameObject objectPrefab;
    public PlacementSettings placementSettings;

    public override bool stackable { get => true;}
    public override object StackSize { get => stackSize; }
}

[Serializable]
public class PlacementSettings
{
    public float maxNormal = 1f;
    public float minNormal = -1f;
    public bool alignToSurface = false;
}