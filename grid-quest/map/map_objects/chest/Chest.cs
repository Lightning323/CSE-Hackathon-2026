using Godot;
using GridQuest.Items;

namespace GridQuest.Map.MapObjects;

public partial class Chest : MapObjectFactory
{
	public const string ClosedTexturePath = "res://assets/images/items/chest/Chest_closed.png";
	public const string OpenTexturePath = "res://assets/images/items/chest/Chest_open.png";

	private Sprite2D _sprite;
	public bool IsOpen { get; private set; }
	public bool LootGenerated { get; private set; }
	public InventoryContainer Contents { get; } = new InventoryContainer(12);

	public override void _Ready()
	{
		base._Ready();
		BlocksMovement = true;
		YSortEnabled = true;
		AddToGroup("chests");
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		UpdateVisual();
	}

	public void Interact(Player player)
	{
		if (!IsOpen)
		{
			OpenChest();
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

	private void GenerateLoot()
	{
		int rolls = (int)GD.RandRange(2, 5);
		string[] pool = { "coin", "coin", "coin", "key", "bomb", "blue_orb", "red_orb", "green_orb", "letter", "boots" };
		for (int i = 0; i < rolls; i++)
		{
			string id = pool[GD.RandRange(0, pool.Length - 1)];
			int count = 1;
			if (id == "coin") count = (int)GD.RandRange(2, 12);
			else if (id == "bomb" || id.EndsWith("_orb") || id == "letter") count = (int)GD.RandRange(1, 3);
			Contents.AddItem(id, count);
		}
		if (GD.Randf() < 0.2f)
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
