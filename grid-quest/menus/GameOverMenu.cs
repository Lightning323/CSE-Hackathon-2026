using Godot;

public partial class GameOverMenu : Control
{
	private Button _exitButton;
	private Label _titleLabel;
	private bool _isGameOver;

	public override void _Ready()
	{
		AddToGroup("game_over_menu");
		Visible = false;
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);

		_exitButton = GetNode<Button>("%ExitButton");
		_titleLabel = GetNode<Label>("%TitleLabel");
		_exitButton.Pressed += OnExitPressed;

		CallDeferred(nameof(ConnectToPlayer));
	}

	private void ConnectToPlayer()
	{
		var player = GetTree().GetFirstNodeInGroup("player") as Player;
		if (player != null && !player.IsConnected(Player.SignalName.Died, Callable.From(OnPlayerDied)))
			player.Died += OnPlayerDied;
	}

	private void OnPlayerDied()
	{
		_isGameOver = true;
		ShowMenu("Game Over");
	}

	public override void _Input(InputEvent @event)
	{
		bool pausePressed = @event.IsActionPressed("pause_menu") || @event.IsActionPressed("ui_cancel");
		if (!pausePressed && @event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
			pausePressed = true;
		if (!pausePressed)
			return;
		if (_isGameOver)
			return;

		var inv = GetTree().GetFirstNodeInGroup("inventory_ui") as Control;
		var skills = GetTree().GetFirstNodeInGroup("skills_ui") as Control;
		var cheat = GetTree().GetFirstNodeInGroup("cheat_ui") as Control;
		var villagerChat = GetTree().GetFirstNodeInGroup("villager_chat_ui") as Control;
		if ((inv != null && inv.Visible) || (skills != null && skills.Visible) || (cheat != null && cheat.Visible) || (villagerChat != null && villagerChat.Visible))
			return;

		if (Visible)
			HideMenu();
		else
			ShowMenu("Paused");

		GetViewport().SetInputAsHandled();
	}

	private void ShowMenu(string title)
	{
		_titleLabel.Text = title;
		Visible = true;
		GetTree().Paused = true;
		_exitButton.GrabFocus();
	}

	private void HideMenu()
	{
		Visible = false;
		GetTree().Paused = false;
	}

	private void OnExitPressed()
	{
		GetTree().Paused = false;
		var err = GetTree().ChangeSceneToFile("res://menus/mainMenu/MainMenu.tscn");
		if (err != Error.Ok)
			GD.PushError($"Failed to open main menu: {err}");
	}
}
