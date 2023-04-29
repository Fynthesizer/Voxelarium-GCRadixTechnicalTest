using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Items/Item")]
public class Item : ScriptableObject
{
    [Header("Item Properties")]
    public string itemID;
    public string itemName;
    public Sprite itemIcon;
    
    public virtual bool stackable
    {
        get => true;
    }

    public virtual object StackSize { get => 1;}
}