using Godot;
using GridQuest.Items;

public partial class InventorySlotUi : PanelContainer
{
	[Signal] public delegate void SlotPressedEventHandler(InventorySlotUi slot);

	public string ContainerId { get; set; } = "player";
	public int SlotIndex { get; set; } = -1; // -1 weapon
	public TextureRect Icon { get; private set; }
	public Label CountLabel { get; private set; }
	public Button Button { get; private set; }

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(44, 44);
		Button = new Button { Flat = false, MouseFilter = MouseFilterEnum.Pass, FocusMode = FocusModeEnum.All };
		Button.SetAnchorsPreset(LayoutPreset.FullRect);
		Button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		Button.SizeFlagsVertical = SizeFlags.ExpandFill;
		Button.Pressed += () => EmitSignal(SignalName.SlotPressed, this);
		AddChild(Button);

		Icon = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		Icon.SetAnchorsPreset(LayoutPreset.FullRect);
		Icon.OffsetLeft = 4; Icon.OffsetTop = 4; Icon.OffsetRight = -4; Icon.OffsetBottom = -4;
		Button.AddChild(Icon);

		CountLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		CountLabel.SetAnchorsPreset(LayoutPreset.FullRect);
		CountLabel.OffsetRight = -2; CountLabel.OffsetBottom = -1;
		Button.AddChild(CountLabel);
	}

	public void SetStack(ItemStack stack)
	{
		if (stack == null || stack.IsEmpty)
		{
			Icon.Texture = null;
			Icon.Visible = false;
			CountLabel.Text = "";
			TooltipText = "";
			return;
		}
		var def = ItemCatalog.Get(stack.ItemId);
		Icon.Texture = def?.LoadIcon();
		Icon.Visible = Icon.Texture != null;
		CountLabel.Text = (def != null && def.Stackable && stack.Count > 1) ? stack.Count.ToString() : "";
		TooltipText = def?.Name ?? stack.ItemId;
	}

	public void SetHighlight(bool on)
	{
		Modulate = on ? new Color(1f, 1f, 0.45f) : Colors.White;
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (!Icon.Visible) return default;

		var preview = new TextureRect
		{
			Texture = Icon.Texture,
			CustomMinimumSize = new Vector2(32, 32),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Modulate = new Color(1, 1, 1, 0.9f),
		};
		SetDragPreview(preview);
		var data = new Godot.Collections.Dictionary { { "type", "inv_slot" }, { "container", ContainerId }, { "index", SlotIndex } };
		return data;
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType != Variant.Type.Dictionary) return false;
		var d = data.AsGodotDictionary();
		return d.ContainsKey("type") && (string)d["type"] == "inv_slot";
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType != Variant.Type.Dictionary) return;
		var d = data.AsGodotDictionary();
		if (!d.ContainsKey("container")) return;
		string fromContainer = (string)d["container"];
		int fromIndex = (int)d["index"];
		var ui = GetTree().GetFirstNodeInGroup("inventory_ui") as InventoryUi;
		if (ui == null) return;
		InventorySlotUi from = ui.FindSlot(fromContainer, fromIndex);
		ui.NotifySlotDragDrop(from, this);
	}
}
