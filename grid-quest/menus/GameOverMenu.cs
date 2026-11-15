using Godot;
using GridQuest.Entities.Player;

public partial class GameOverMenu : Control
{
	private Button _exitButton;
	private Button _restartButton;
	private Button _controlsButton;
	private Button _controlsBackButton;
	private Label _titleLabel;
	private PanelContainer _controlsPanel;
	private RichTextLabel _controlsLabel;
	private PanelContainer _mainPanel;
	private bool _isGameOver;
	private bool _openedAsStartupControls;

	public override void _Ready()
	{
		AddToGroup("game_over_menu");
		Visible = false;
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);

		_exitButton = GetNode<Button>("%ExitButton");
		_restartButton = GetNodeOrNull<Button>("%RestartButton");
		_controlsButton = GetNodeOrNull<Button>("%ControlsButton");
		_controlsBackButton = GetNodeOrNull<Button>("%ControlsBackButton");
		_titleLabel = GetNode<Label>("%TitleLabel");
		_controlsPanel = GetNodeOrNull<PanelContainer>("%ControlsPanel");
		_controlsLabel = GetNodeOrNull<RichTextLabel>("%ControlsLabel");
		_mainPanel = GetNodeOrNull<PanelContainer>("CenterContainer/Panel");

		_exitButton.Pressed += OnExitPressed;
		if (_restartButton != null) _restartButton.Pressed += OnRestartPressed;
		if (_controlsButton != null) _controlsButton.Pressed += OnControlsPressed;
		if (_controlsBackButton != null) _controlsBackButton.Pressed += OnControlsBackPressed;
		PopulateControlsText();

		CallDeferred(nameof(ConnectToPlayer));
		CallDeferred(nameof(ShowStartupControlsIfRequested));
	}

	private void ConnectToPlayer()
	{
		var player = GetTree().GetFirstNodeInGroup("player") as PlayerEntity;
		if (player != null && !player.IsConnected(PlayerEntity.SignalName.Died, Callable.From(OnPlayerDied)))
			player.Died += OnPlayerDied;
	}

	private void OnPlayerDied()
	{
		WorldProgress.RegisterResetHighScoreProgress();
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
		{
			// Allow closing controls view during game over too.
			if (_controlsPanel != null && _controlsPanel.Visible)
			{
				HideControlsView();
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		var inv = GetTree().GetFirstNodeInGroup("inventory_ui") as Control;
		var skills = GetTree().GetFirstNodeInGroup("skills_ui") as Control;
		var cheat = GetTree().GetFirstNodeInGroup("cheat_ui") as Control;
		var villagerChat = GetTree().GetFirstNodeInGroup("villager_chat_ui") as Control;
		if ((inv != null && inv.Visible) || (skills != null && skills.Visible) || (cheat != null && cheat.Visible) || (villagerChat != null && villagerChat.Visible))
			return;

		if (Visible)
		{
			if (_controlsPanel != null && _controlsPanel.Visible)
				HideControlsView();
			else
				HideMenu();
		}
		else
		{
			ShowMenu("Paused");
		}

		GetViewport().SetInputAsHandled();
	}

	private void ShowMenu(string title)
	{
		_titleLabel.Text = title;
		if (_restartButton != null)
			_restartButton.Visible = true;
		if (_controlsButton != null)
			_controlsButton.Visible = true;
		if (_exitButton != null)
			_exitButton.Visible = true;
		if (_mainPanel != null)
			_mainPanel.Visible = true;
		HideControlsView();
		Visible = true;
		GetTree().Paused = true;
		(_isGameOver ? _restartButton : _exitButton ?? _restartButton)?.GrabFocus();
	}

	private void HideMenu()
	{
		HideControlsView();
		Visible = false;
		GetTree().Paused = false;
	}

	private void OnRestartPressed()
	{
		GetTree().Paused = false;
		var err = GetTree().ReloadCurrentScene();
		if (err != Error.Ok)
			GD.PushError($"Failed to restart scene: {err}");
	}

	private void OnExitPressed()
	{
		GetTree().Paused = false;
		var err = GetTree().ChangeSceneToFile("res://menus/mainMenu/MainMenu.tscn");
		if (err != Error.Ok)
			GD.PushError($"Failed to open main menu: {err}");
	}

	private void ShowStartupControlsIfRequested()
	{
		var session = GetNodeOrNull<GridQuest.Menus.MainMenu.GameSession>("/root/GameSession");
		if (session == null || !session.ConsumeShowControlsOnNextLevelLoad())
			return;

		_isGameOver = false;
		_openedAsStartupControls = true;
		_titleLabel.Text = "Controls";
		if (_restartButton != null) _restartButton.Visible = false;
		if (_controlsButton != null) _controlsButton.Visible = false;
		if (_exitButton != null) _exitButton.Visible = false;
		if (_mainPanel != null) _mainPanel.Visible = false;
		Visible = true;
		GetTree().Paused = true;
		ShowControlsView(focusBackButton: true);
	}

	private void ShowControlsView(bool focusBackButton)
	{
		if (_controlsPanel == null) return;
		_controlsPanel.Visible = true;
		if (focusBackButton)
			_controlsBackButton?.GrabFocus();
	}

	private void OnControlsPressed()
	{
		ShowControlsView(focusBackButton: true);
	}

	private void OnControlsBackPressed()
	{
		HideControlsView();
	}

	private void HideControlsView()
	{
		if (_controlsPanel != null)
			_controlsPanel.Visible = false;

		if (_openedAsStartupControls)
		{
			_openedAsStartupControls = false;
			if (_restartButton != null) _restartButton.Visible = true;
			if (_controlsButton != null) _controlsButton.Visible = true;
			if (_exitButton != null) _exitButton.Visible = true;
			if (_mainPanel != null) _mainPanel.Visible = true;
			Visible = false;
			GetTree().Paused = false;
			return;
		}

		if (Visible && !_isGameOver)
			_controlsButton?.GrabFocus();
		else if (Visible && _isGameOver)
			_restartButton?.GrabFocus();
	}

	private void PopulateControlsText()
	{
		if (_controlsLabel == null) return;
		_controlsLabel.Text =
			"[b]Movement[/b]\n" +
			"  Move: WASD / Arrow Keys / Controller Left Stick / D-pad\n\n" +
			"[b]Combat & Interaction[/b]\n" +
			"  Attack: Space / J / Controller A\n" +
			"  Interact (chest, villager, monument): F / Controller X\n\n" +
			"[b]Inventory & UI[/b]\n" +
			"  Toggle Inventory: E / Tab / I / Controller Y\n" +
			"  UI Navigate: Arrow Keys / D-pad / Left Stick\n" +
			"  UI Confirm/Select: Enter / Space / Controller A\n" +
			"  UI Back/Close (contextual): Esc / Controller B / Start\n\n" +
			"[b]Menus[/b]\n" +
			"  Pause Menu: Esc / Controller Start\n" +
			"  Close Open Menus/UI (contextual): Esc / B / Start / interact/toggle keys as applicable\n\n" +
			"[b]Camera[/b]\n" +
			"  Zoom In/Out: Mouse Wheel Up/Down, + / - keys (next to Backspace), Keypad + / -\n\n" +
			"[b]Debug/Cheats[/b]\n" +
			"  Toggle Cheat Menu: ` (backtick/quote key)\n\n" +
			"[b]Chat (Villager)[/b]\n" +
			"  Type response: Keyboard (text input)\n" +
			"  Send: Enter / Send button\n" +
			"  Close chat: Esc / B / Start / Close button";
	}
}
