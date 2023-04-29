using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Consumable", menuName = "Items/Consumable")]
public class ConsumableItem : Item
{
    [Header("Consumable Properties")]
    public int stackSize;
    public float hungerRestoration;
    public float healthRestoration;
    public float staminaRestoration;

    public override bool stackable { get => true; }
    public override object StackSize { get => stackSize; }
}