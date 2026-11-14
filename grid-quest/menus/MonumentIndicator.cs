using Godot;

public partial class MonumentIndicator : TextureRect
{
	[Export] public float EdgePadding { get; set; } = 28.0f;

	private Monument _monument;
	private Node2D _player;

	public override void _Ready()
	{
		Texture = GD.Load<Texture2D>("res://assets/images/items/meta_pickaxe.png");
		StretchMode = StretchModeEnum.KeepAspectCentered;
		CustomMinimumSize = new Vector2(24, 24);
		Size = new Vector2(24, 24);
		PivotOffset = Size / 2.0f;
		MouseFilter = MouseFilterEnum.Ignore;
		Visible = false;
		SetAnchorsPreset(LayoutPreset.TopLeft);
	}

	public override void _Process(double delta)
	{
		if (IsBossFightActive())
		{
			Visible = false;
			return;
		}

		_monument ??= GetTree().GetFirstNodeInGroup("monument") as Monument;
		_player ??= GetTree().GetFirstNodeInGroup("player") as Node2D;

		if (_monument == null || _player == null)
		{
			Visible = false;
			return;
		}

		Vector2 toTarget = _monument.GlobalPosition - _player.GlobalPosition;
		if (toTarget.LengthSquared() < 1.0f)
		{
			Visible = false;
			return;
		}

		Visible = true;
		Vector2 dir = toTarget.Normalized();
		Rotation = dir.Angle() + Mathf.Pi / 2.0f;

		Vector2 viewportSize = GetViewportRect().Size;
		Vector2 center = viewportSize / 2.0f;
		// Point toward target around screen edge/center area; keep indicator near player-center as compass.
		Position = center + dir * Mathf.Min(viewportSize.X, viewportSize.Y) * 0.22f - Size / 2.0f;
	}

	private bool IsBossFightActive()
	{
		if (GetTree().GetFirstNodeInGroup("hellhound_swarm_boss_manager") is HellhoundSwarmBoss h && h.IsActive) return true;
		if (GetTree().GetFirstNodeInGroup("executioner_boss_manager") is ExecutionerBoss e && e.IsActive) return true;
		return false;
	}
}
