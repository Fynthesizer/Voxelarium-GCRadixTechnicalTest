using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Substance", menuName = "Items/Substance")]
public class SubstanceItem : Item
{
    [Header("Substance Properties")]
    public Voxel.Material material;
    public float stackSize;

    public override bool stackable { get => true;}
    public override object StackSize { get => stackSize; }
}
