using Godot;

public partial class SkillsUi : Control
{
	public override void _Ready()
	{
		AddToGroup("skills_ui");
		Visible = false;
	}

	public void Open()
	{
		Visible = true;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible) return;
		if (@event is InputEventKey key && key.Pressed && !key.Echo && (key.Keycode == Key.F || key.Keycode == Key.Escape))
		{
			Visible = false;
			GetViewport().SetInputAsHandled();
		}
	}
}
