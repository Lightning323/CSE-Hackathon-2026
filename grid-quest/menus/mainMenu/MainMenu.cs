using Godot;

namespace GridQuest.Menus.MainMenu;

public partial class MainMenu : Control
{
	private const string LevelScenePath = "res://level/main_level.tscn";
	private const string SettingsScenePath = "res://menus/mainMenu/SettingsMenu.tscn";

	private Button _startGameButton;
	private Button _settingsButton;
	private Label _worldResetHighScoreLabel;

	public override void _Ready()
	{
		_startGameButton = GetNode<Button>("%StartGameButton");
		_settingsButton = GetNode<Button>("%SettingsButton");
		_worldResetHighScoreLabel = GetNodeOrNull<Label>("%WorldResetHighScoreLabel");

		_startGameButton.Pressed += OnStartGamePressed;
		_settingsButton.Pressed += OnSettingsPressed;
		RefreshWorldResetHighScore();
		_startGameButton.GrabFocus();
	}

	private void RefreshWorldResetHighScore()
	{
		if (_worldResetHighScoreLabel != null)
			_worldResetHighScoreLabel.Text = $"World Reset High Score: {WorldProgress.WorldResetHighScore}";
	}

	private void OnStartGamePressed()
	{
		var session = GameSession.Instance ?? GetNodeOrNull<GameSession>("/root/GameSession");
		if (session != null)
		{
			session.SetCurrentSave(SaveManager.GetOrCreateTemporarySave());
			session.ShowControlsOnNextLevelLoad = true;
		}
		else
			GD.PushWarning("GameSession autoload not found; starting without session metadata.");

		var err = GetTree().ChangeSceneToFile(LevelScenePath);
		if (err != Error.Ok)
			GD.PushError($"Failed to load level scene '{LevelScenePath}': {err}");
	}

	private void OnSettingsPressed()
	{
		var err = GetTree().ChangeSceneToFile(SettingsScenePath);
		if (err != Error.Ok)
			GD.PushError($"Failed to open settings scene '{SettingsScenePath}': {err}");
	}
}
