using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Tool", menuName = "Items/Tool")]
public class ToolItem : Item
{
    [Header("Tool Properties")]
    public float mineSpeed;
    public float mineRadius;

    public Voxel.Material mineableSubstances;

    public override bool stackable { get => false;}
}
