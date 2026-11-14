using System.Collections.Generic;

namespace GridQuest.Items;

public class InventoryContainer
{
	public ItemStack[] Slots { get; }

	public InventoryContainer(int size)
	{
		Slots = new ItemStack[size];
		for (int i = 0; i < size; i++) Slots[i] = new ItemStack();
	}

	public ItemStack Get(int index) => (index >= 0 && index < Slots.Length) ? Slots[index] : null;

	public bool AddItem(string itemId, int count = 1)
	{
		if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
		var def = ItemCatalog.Get(itemId);
		if (def == null) return false;

		if (def.Stackable)
		{
			for (int i = 0; i < Slots.Length && count > 0; i++)
			{
				var s = Slots[i];
				if (!s.IsEmpty && s.ItemId == itemId && s.Count < def.MaxStack)
				{
					int add = System.Math.Min(count, def.MaxStack - s.Count);
					s.Count += add;
					count -= add;
				}
			}
		}

		for (int i = 0; i < Slots.Length && count > 0; i++)
		{
			var s = Slots[i];
			if (s.IsEmpty)
			{
				int put = def.Stackable ? System.Math.Min(count, def.MaxStack) : 1;
				s.ItemId = itemId;
				s.Count = put;
				count -= put;
				if (!def.Stackable) break;
			}
		}

		return count <= 0;
	}

	public static void SwapOrMove(InventoryContainer a, int ai, InventoryContainer b, int bi)
	{
		var sa = a.Get(ai);
		var sb = b.Get(bi);
		if (sa == null || sb == null) return;

		if (!sa.IsEmpty && !sb.IsEmpty)
		{
			var da = ItemCatalog.Get(sa.ItemId);
			var db = ItemCatalog.Get(sb.ItemId);
			if (da != null && db != null && da.Stackable && sa.ItemId == sb.ItemId && sb.Count < db.MaxStack)
			{
				int move = System.Math.Min(sa.Count, db.MaxStack - sb.Count);
				sb.Count += move;
				sa.Count -= move;
				if (sa.Count <= 0) { sa.ItemId = null; sa.Count = 0; }
				return;
			}
		}

		a.Slots[ai] = sb;
		b.Slots[bi] = sa;
	}
}
