using Godot;
using System.Collections.Generic;

namespace GridQuest.Items;

public sealed class ItemDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string IconPath { get; init; }
	public bool Stackable { get; init; }
	public int MaxStack { get; init; } = 99;
	public bool IsWeapon { get; init; }

	public Texture2D LoadIcon() => string.IsNullOrEmpty(IconPath) ? null : GD.Load<Texture2D>(IconPath);
}

public sealed class ItemStack
{
	public string ItemId { get; set; }
	public int Count { get; set; } = 1;

	public bool IsEmpty => string.IsNullOrEmpty(ItemId) || Count <= 0;

	public ItemStack Clone() => new() { ItemId = ItemId, Count = Count };
}

public static class ItemCatalog
{
	public static readonly Dictionary<string, ItemDef> Items = new()
	{
		["coin"] = new ItemDef { Id = "coin", Name = "Coin", IconPath = "res://assets/images/items/coin/meta_coin1.png", Stackable = true, MaxStack = 999 },
		["base_sword"] = new ItemDef { Id = "base_sword", Name = "Sword", IconPath = "res://assets/images/items/base_sword.png", Stackable = false, MaxStack = 1, IsWeapon = true },
		["fire_staff"] = new ItemDef { Id = "fire_staff", Name = "Fire Staff", IconPath = "res://assets/images/items/fire-staff.png", Stackable = false, MaxStack = 1, IsWeapon = true },
		["scythe"] = new ItemDef { Id = "scythe", Name = "Scythe", IconPath = "res://assets/images/items/scythe_Icon.png", Stackable = false, MaxStack = 1, IsWeapon = true },
		["crossbow"] = new ItemDef { Id = "crossbow", Name = "Crossbow", IconPath = "res://assets/images/items/crossbow_Icon.png", Stackable = false, MaxStack = 1, IsWeapon = true },
		["key"] = new ItemDef { Id = "key", Name = "Key", IconPath = "res://assets/images/items/meta_key.png", Stackable = true, MaxStack = 20 },
		["boots"] = new ItemDef { Id = "boots", Name = "Boots", IconPath = "res://assets/images/items/meta_boots.png", Stackable = false, MaxStack = 1 },
		["bomb"] = new ItemDef { Id = "bomb", Name = "Bomb", IconPath = "res://assets/images/items/meta_bomb.png", Stackable = true, MaxStack = 10 },
		["blue_orb"] = new ItemDef { Id = "blue_orb", Name = "Blue Orb", IconPath = "res://assets/images/items/meta_orb-blue1.png", Stackable = true, MaxStack = 20 },
		["red_orb"] = new ItemDef { Id = "red_orb", Name = "Red Orb", IconPath = "res://assets/images/items/meta_orb-red1.png", Stackable = true, MaxStack = 20 },
		["green_orb"] = new ItemDef { Id = "green_orb", Name = "Green Orb", IconPath = "res://assets/images/items/meta_orb-green1.png", Stackable = true, MaxStack = 20 },
		["letter"] = new ItemDef { Id = "letter", Name = "Letter", IconPath = "res://assets/images/items/meta_letter.png", Stackable = true, MaxStack = 20 },
	};

	public static ItemDef Get(string id) => id != null && Items.TryGetValue(id, out var def) ? def : null;

	public static Texture2D GetIcon(string id) => Get(id)?.LoadIcon();
}
