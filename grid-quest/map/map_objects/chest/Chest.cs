using Godot;
using GridQuest.Entities;
using GridQuest.Items;

namespace GridQuest.Map.MapObjects;

public partial class Chest : MapObjectFactory
{
	public const string ClosedTexturePath = "res://assets/images/items/chest/Chest_closed.png";
	public const string OpenTexturePath = "res://assets/images/items/chest/Chest_open.png";

	private Sprite2D _sprite;
	public bool IsOpen { get; private set; }
	public bool LootGenerated { get; private set; }
	[Export] public bool RestocksDaily { get; set; } = false;
	public InventoryContainer Contents { get; } = new InventoryContainer(12);

	public override void _Ready()
	{
		base._Ready();
		BlocksMovement = true;
		YSortEnabled = true;
		AddToGroup("chests");
		AddToGroup("interactables");
		if (RestocksDaily) AddToGroup("daily_restock_chests");
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		UpdateVisual();
		CallDeferred(nameof(ConnectSunriseRestock));
	}

	private void ConnectSunriseRestock()
	{
		if (!RestocksDaily) return;
		var cycle = GetTree().GetFirstNodeInGroup("day_night_cycle");
		if (cycle == null) return;
		var callable = new Callable(this, MethodName.OnSunriseRestock);
		if (!cycle.IsConnected("Sunrise", callable))
			cycle.Connect("Sunrise", callable);
	}

	private void OnSunriseRestock()
	{
		if (!RestocksDaily) return;
		RestockLoot();
	}

	public void Interact(Entity player)
	{
		if (!IsOpen)
		{
			OpenChest();
		}
		if (player.AiControlled)
		{
			foreach (var slot in Contents.Slots)
			{
				if (slot == null || slot.IsEmpty || !player.AddItem(slot.ItemId, slot.Count)) continue;
				slot.ItemId = null;
				slot.Count = 0;
			}
			return;
		}
		var ui = GetTree().GetFirstNodeInGroup("inventory_ui") as InventoryUi;
		ui?.OpenChest(this);
	}

	public void OpenChest()
	{
		if (!LootGenerated)
		{
			GenerateLoot();
			LootGenerated = true;
		}
		IsOpen = true;
		UpdateVisual();
	}

	public void RestockLoot()
	{
		ClearContents();
		GenerateLoot();
		LootGenerated = true;
		IsOpen = false;
		UpdateVisual();
	}

	private void ClearContents()
	{
		for (int i = 0; i < Contents.Slots.Length; i++)
		{
			var s = Contents.Slots[i];
			if (s == null) continue;
			s.ItemId = null;
			s.Count = 0;
		}
	}

	private void GenerateLoot()
	{
		// Increased base loot, but rewards fall off rapidly to nothing after the third day of the current world spawn.
		float mult = WorldProgress.GetChestLootMultiplier();
		if (mult <= 0.001f)
			return;

		int baseRolls = RestocksDaily ? (int)GD.RandRange(3, 6) : (int)GD.RandRange(4, 7);
		int rolls = Mathf.Max(1, Mathf.RoundToInt(baseRolls * mult));
		if (mult < 0.99f && GD.Randf() > Mathf.Clamp(mult + 0.15f, 0.1f, 1.0f))
			rolls = Mathf.Max(0, rolls - 1);

		string[] pool = { "coin", "coin", "coin", "key", "bomb", "blue_orb", "red_orb", "green_orb", "letter", "boots" };
		for (int i = 0; i < rolls; i++)
		{
			string id = pool[GD.RandRange(0, pool.Length - 1)];
			int count = 1;
			if (id == "coin")
			{
				int baseCoin = (int)GD.RandRange(RestocksDaily ? 4 : 6, RestocksDaily ? 12 : 16);
				count = Mathf.Max(1, Mathf.RoundToInt(baseCoin * mult));
			}
			else if (id == "bomb" || id.EndsWith("_orb") || id == "letter")
			{
				count = Mathf.Max(1, Mathf.RoundToInt((int)GD.RandRange(1, 3) * Mathf.Clamp(mult, 0.4f, 1.0f)));
			}
			Contents.AddItem(id, count);
		}

		float weaponChance = (RestocksDaily ? 0.14f : 0.2f) * Mathf.Clamp(mult, 0.25f, 1.0f);
		if (GD.Randf() < weaponChance)
		{
			Contents.AddItem(GD.Randf() < 0.5f ? "base_sword" : "fire_staff", 1);
		}
	}

	private void UpdateVisual()
	{
		if (_sprite == null) return;
		_sprite.Texture = GD.Load<Texture2D>(IsOpen ? OpenTexturePath : ClosedTexturePath);
	}
}
