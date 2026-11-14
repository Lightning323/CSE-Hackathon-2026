using Godot;
using System.Collections.Generic;

namespace GridQuest.Menus.MainMenu;

public partial class MainMenu : Control
{
	private const string LevelScenePath = "res://level/character_dev/main_level.tscn";
	private const string SettingsScenePath = "res://menus/mainMenu/SettingsMenu.tscn";
	private const string GenerationScenePath = "res://game_generator/GameGenerator.tscn";

	private ItemList _saveList;
	private Button _playButton;
	private Button _deleteButton;
	private Button _newSaveButton;
	private Button _settingsButton;
	private Button _openFolderButton;
	private Label _titleLabel;
	private Label _descriptionLabel;
	private Label _pathLabel;
	private Label _appDataLabel;
	private Label _statusLabel;
	private ConfirmationDialog _createDialog;
	private LineEdit _newTitleEdit;
	private TextEdit _newDescriptionEdit;

	private List<SaveInfo> _saves = new();

	public override void _Ready()
	{
		_saveList = GetNode<ItemList>("%SaveList");
		_playButton = GetNode<Button>("%PlayButton");
		_deleteButton = GetNode<Button>("%DeleteButton");
		_newSaveButton = GetNode<Button>("%NewSaveButton");
		_settingsButton = GetNode<Button>("%SettingsButton");
		_openFolderButton = GetNode<Button>("%OpenFolderButton");
		_titleLabel = GetNode<Label>("%TitleLabel");
		_descriptionLabel = GetNode<Label>("%DescriptionLabel");
		_pathLabel = GetNode<Label>("%PathLabel");
		_appDataLabel = GetNode<Label>("%AppDataLabel");
		_statusLabel = GetNode<Label>("%StatusLabel");
		_createDialog = GetNode<ConfirmationDialog>("%CreateDialog");
		_newTitleEdit = GetNode<LineEdit>("%NewTitleEdit");
		_newDescriptionEdit = GetNode<TextEdit>("%NewDescriptionEdit");

		_saveList.ItemSelected += OnSaveSelected;
		_saveList.ItemActivated += _ => OnPlayPressed();
		_newSaveButton.Pressed += OnNewSavePressed;
		_deleteButton.Pressed += OnDeletePressed;
		_playButton.Pressed += OnPlayPressed;
		_settingsButton.Pressed += OnSettingsPressed;
		_openFolderButton.Pressed += OnOpenSavesFolderPressed;
		_createDialog.Confirmed += OnCreateSaveConfirmed;

		_appDataLabel.Text = $"App data: {OS.GetUserDataDir()}";

		var session = GameSession.Instance ?? GetNodeOrNull<GameSession>("/root/GameSession");
		RefreshSaves();
		if (session != null && !string.IsNullOrWhiteSpace(session.LastCreatedSaveId))
		{
			SelectSaveById(session.LastCreatedSaveId);
			_statusLabel.Text = "New save created with story.json (not started).";
			session.LastCreatedSaveId = null;
		}
		else
		{
			_statusLabel.Text = "";
		}
	}

	private void RefreshSaves()
	{
		_saves = SaveManager.ListSaves();
		_saveList.Clear();
		foreach (var save in _saves)
		{
			var label = string.IsNullOrWhiteSpace(save.Title) ? save.Id : save.Title;
			_saveList.AddItem(label);
		}

		if (_saves.Count > 0)
		{
			_saveList.Select(0);
			OnSaveSelected(0);
		}
		else
		{
			UpdateDetails(null);
		}

		UpdateButtons();
	}

	private void UpdateButtons()
	{
		bool hasSelection = GetSelectedSave() != null;
		_playButton.Disabled = !hasSelection;
		_deleteButton.Disabled = !hasSelection;
	}

	private SaveInfo GetSelectedSave()
	{
		var selected = _saveList.GetSelectedItems();
		if (selected == null || selected.Length == 0)
			return null;
		int idx = selected[0];
		if (idx < 0 || idx >= _saves.Count)
			return null;
		return _saves[idx];
	}

	private void OnSaveSelected(long index)
	{
		int idx = (int)index;
		if (idx < 0 || idx >= _saves.Count)
			UpdateDetails(null);
		else
			UpdateDetails(_saves[idx]);
		UpdateButtons();
	}

	private void UpdateDetails(SaveInfo save)
	{
		if (save == null)
		{
			_titleLabel.Text = "No save selected";
			_descriptionLabel.Text = "Create a new save to begin.";
			_pathLabel.Text = $"Saves folder: {SaveManager.GetSavesRootPath()}";
			return;
		}

		_titleLabel.Text = string.IsNullOrWhiteSpace(save.Title) ? save.Id : save.Title;
		var desc = string.IsNullOrWhiteSpace(save.Description) ? "(No description)" : save.Description;
		var created = string.IsNullOrWhiteSpace(save.CreatedUtc) ? "Unknown" : save.CreatedUtc;
		_descriptionLabel.Text = $"{desc}\n\nCreated (UTC): {created}\nID: {save.Id}";
		_pathLabel.Text = $"Folder: {save.FolderPath}";
	}

	private void OnNewSavePressed()
	{
		_newTitleEdit.Text = "";
		_newDescriptionEdit.Text = "";
		_createDialog.PopupCentered();
		_newTitleEdit.GrabFocus();
	}

	private void OnCreateSaveConfirmed()
	{
		var session = GameSession.Instance ?? GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
		{
			GD.PushError("GameSession autoload not found; cannot start game generation.");
			return;
		}

		session.PendingNewSaveTitle = _newTitleEdit.Text;
		session.PendingNewSaveDescription = _newDescriptionEdit.Text;
		var err = GetTree().ChangeSceneToFile(GenerationScenePath);
		if (err != Error.Ok)
			GD.PushError($"Failed to open generation scene '{GenerationScenePath}': {err}");
	}

	private void SelectSaveById(string id)
	{
		for (int i = 0; i < _saves.Count; i++)
		{
			if (_saves[i].Id == id)
			{
				_saveList.Select(i);
				OnSaveSelected(i);
				return;
			}
		}
	}

	private void OnDeletePressed()
	{
		var save = GetSelectedSave();
		if (save == null)
			return;

		SaveManager.DeleteSave(save);
		RefreshSaves();
		_statusLabel.Text = "";
	}

	private void OnPlayPressed()
	{
		var save = GetSelectedSave();
		if (save == null)
			return;
		StartSave(save);
	}

	private void StartSave(SaveInfo save)
	{
		var session = GameSession.Instance ?? GetNodeOrNull<GameSession>("/root/GameSession");
		if (session == null)
			GD.PushWarning("GameSession autoload not found; starting without session metadata.");
		else
			session.SetCurrentSave(save);

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

	private void OnOpenSavesFolderPressed()
	{
		SaveManager.OpenSavesFolder();
	}
}
