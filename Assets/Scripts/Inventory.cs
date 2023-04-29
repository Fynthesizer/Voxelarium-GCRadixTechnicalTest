using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class Inventory
{
    public List<ItemSlot> slots;
    public int capacity;
    public int activeSlot;

    public Inventory(int capacity)
    {
        slots = new List<ItemSlot>();
        for (int i = 0; i < capacity; i++) slots.Add(new ItemSlot());
        this.capacity = capacity;
    }

    public void AddItem(Item item)
    {
        //Check for empty slot
        ItemSlot slot = FindEmptySlot();
        if (slot != null)
        {
            slot.item = item;
            slot.quantity = 1;
        }
        else Debug.LogError("Inventory full");
    }

    public void AddItem(Item item, int quantity)
    {
        //Check for empty slot
        ItemSlot slot = FindEmptySlot();
        if (slot != null)
        {
            slot.item = item;
            slot.quantity = quantity;
        }
        else Debug.LogError("Inventory full");
    }

    public void RemoveItem(Item item)
    {
        ItemSlot slot = FindSlotContaining(item);
        if (slot != null)
        {
            slot.quantity--;
            if (slot.quantity == 0) slot.item = null;
        }
        else Debug.LogError("Inventory does not contain this item");
    }

    public void RemoveItem(Item item, int quantity)
    {
        ItemSlot slot = FindSlotContaining(item);
        if (slot != null && slot.quantity >= quantity)
        {
            slot.quantity -= quantity;
            if (slot.quantity == 0) slot.item = null;
        }
        else Debug.LogError("Inventory does not contain enough of this item");
    }

    public void RemoveActiveItem()
    {
        ItemSlot slot = slots[activeSlot];
        if (slot.item != null)
        {
            slot.quantity--;
            if (slot.quantity == 0) slot.item = null;
        }
        else Debug.LogError("Active slot is empty");
    }

    public ItemSlot FindEmptySlot()
    {
        return slots.FirstOrDefault(i => i.item == null);
    }

    public ItemSlot FindSlotContaining(Item item)
    {
        return slots.FirstOrDefault(i => i.item == item);
    }

    public void IncrementActiveSlot(int increment)
    {
        activeSlot += increment;
        activeSlot = Mathf.Clamp(activeSlot, 0, capacity - 1);
    }

    public void SetActiveSlot(int index)
    {
        activeSlot = index - 1;
        activeSlot = Mathf.Clamp(activeSlot, 0, capacity - 1);
    }

    public Item ActiveItem()
    {
        if (slots[activeSlot].item != null) return slots[activeSlot].item;
        else return null;
    }
}

public class ItemSlot
{
    public Item item;
    public int quantity;
}