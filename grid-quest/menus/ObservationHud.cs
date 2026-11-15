#nullable enable
using Godot;
using GridQuest.Entities;

public partial class ObservationHud : Label
{
	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Process(double delta)
	{
		var controller = GetTree().GetFirstNodeInGroup("player_controller") as PlayerController;
		Entity? viewed = controller?.ObservedEntity;
		if (viewed == null || !IsInstanceValid(viewed))
		{
			Text = "Viewing: None";
			return;
		}

		var controlled = controller?.ControlledEntity;
		if (controlled != null && IsInstanceValid(controlled))
		{
			Text = controlled == viewed
				? $"Viewing {viewed.Name} (Controlling)"
				: $"Viewing {viewed.Name} (Controlling {controlled.Name})";
		}
		else
		{
			Text = $"Viewing {viewed.Name} (Observing)";
		}
	}
}
