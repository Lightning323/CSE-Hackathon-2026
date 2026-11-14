using Godot;

public partial class SurvivalTimerUi : Label
{
	private SurvivalStats _stats;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.TopLeft);
		OffsetLeft = 12;
		OffsetTop = 10;
		OffsetRight = 140;
		OffsetBottom = 34;
		HorizontalAlignment = HorizontalAlignment.Left;
		VerticalAlignment = VerticalAlignment.Center;
		MouseFilter = MouseFilterEnum.Ignore;
		AddThemeFontSizeOverride("font_size", 14);
		AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.9f, 0.95f));
		AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
		AddThemeConstantOverride("outline_size", 3);
		Text = "0s";
	}

	public override void _Process(double delta)
	{
		if (_stats == null || !IsInstanceValid(_stats))
			_stats = GetTree().GetFirstNodeInGroup("survival_stats") as SurvivalStats;

		Text = _stats != null ? _stats.GetFormattedElapsedTime() : "0s";
	}
}
