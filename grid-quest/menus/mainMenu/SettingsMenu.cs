using Godot;
using GridQuest.GameGenerator.Llm;

namespace GridQuest.Menus.MainMenu;

public partial class SettingsMenu : Control
{
	private const string MainMenuScenePath = "res://menus/mainMenu/MainMenu.tscn";

	private OptionButton _providerOption;
	private LineEdit _baseUrlEdit;
	private LineEdit _modelEdit;
	private LineEdit _apiKeyEdit;
	private CheckBox _thinkingCheck;
	private Label _statusLabel;
	private Button _backButton;
	private Button _saveButton;
	private Button _testButton;
	private Button _clearDataButton;

	public override void _Ready()
	{
		_providerOption = GetNode<OptionButton>("%ProviderOption");
		_baseUrlEdit = GetNode<LineEdit>("%BaseUrlEdit");
		_modelEdit = GetNode<LineEdit>("%ModelEdit");
		_apiKeyEdit = GetNode<LineEdit>("%ApiKeyEdit");
		_thinkingCheck = GetNode<CheckBox>("%ThinkingCheck");
		_statusLabel = GetNode<Label>("%StatusLabel");
		_backButton = GetNode<Button>("%BackButton");
		_saveButton = GetNode<Button>("%SaveButton");
		_testButton = GetNode<Button>("%TestButton");
		_clearDataButton = GetNode<Button>("%ClearDataButton");

		_providerOption.AddItem("OpenAI");
		_providerOption.AddItem("Anthropic");
		_providerOption.AddItem("LM Studio");
		_providerOption.AddItem("Ollama");
		_providerOption.AddItem("Custom (OpenAI-compatible)");

		_providerOption.ItemSelected += _ => ApplyProviderDefaultsIfEmptyOrMatching();
		_backButton.Pressed += OnBackPressed;
		_saveButton.Pressed += OnSavePressed;
		_testButton.Pressed += OnTestPressed;
		_clearDataButton.Pressed += OnClearDataPressed;

		LoadIntoUi();
		_providerOption.GrabFocus();
	}

	private void LoadIntoUi()
	{
		var settings = LlmSettingsStore.Load();
		SelectProvider(settings.Provider);
		_baseUrlEdit.Text = settings.BaseUrl;
		_modelEdit.Text = settings.Model;
		_apiKeyEdit.Text = settings.ApiKey;
		_thinkingCheck.ButtonPressed = settings.EnableThinking;
		_statusLabel.Text = "Settings loaded.";
	}

	private void SelectProvider(string provider)
	{
		for (int i = 0; i < _providerOption.ItemCount; i++)
		{
			if (_providerOption.GetItemText(i) == provider)
			{
				_providerOption.Select(i);
				return;
			}
		}
		_providerOption.Select(0);
	}

	private LlmSettings ReadFromUi()
	{
		var settings = new LlmSettings
		{
			Provider = _providerOption.GetItemText(_providerOption.Selected),
			BaseUrl = _baseUrlEdit.Text?.Trim() ?? "",
			ApiKey = _apiKeyEdit.Text?.Trim() ?? "",
			Model = _modelEdit.Text?.Trim() ?? "",
			EnableThinking = _thinkingCheck.ButtonPressed,
		};
		return LlmSettingsStore.Normalize(settings);
	}

	private void ApplyProviderDefaultsIfEmptyOrMatching()
	{
		var provider = _providerOption.GetItemText(_providerOption.Selected);
		var current = ReadFromUi();
		var defaults = LlmSettingsStore.Normalize(new LlmSettings { Provider = provider, BaseUrl = "", Model = "" });

		if (string.IsNullOrWhiteSpace(current.BaseUrl) || IsKnownDefaultBaseUrl(current.BaseUrl))
			_baseUrlEdit.Text = defaults.BaseUrl;
		if (string.IsNullOrWhiteSpace(current.Model) || IsKnownDefaultModel(current.Model))
			_modelEdit.Text = defaults.Model;
	}

	private static bool IsKnownDefaultBaseUrl(string url)
	{
		url = (url ?? "").Trim().TrimEnd('/');
		return url is "https://api.openai.com/v1" or "https://api.anthropic.com/v1" or "http://localhost:1234/v1" or "http://localhost:11434";
	}

	private static bool IsKnownDefaultModel(string model)
	{
		return model is "gpt-4o-mini" or "claude-3-5-haiku-latest" or "local-model" or "qwen3:8b";
	}

	private void OnSavePressed()
	{
		var settings = ReadFromUi();
		LlmSettingsStore.Save(settings);
		_baseUrlEdit.Text = settings.BaseUrl;
		_modelEdit.Text = settings.Model;
		_statusLabel.Text = "Settings saved.";
	}

	private async void OnTestPressed()
	{
		var settings = ReadFromUi();
		LlmSettingsStore.Save(settings);
		_testButton.Disabled = true;
		_statusLabel.Text = "Testing model connection...";

		string result;
		var api = LlmApi.Instance ?? GetNodeOrNull<LlmApi>("/root/LlmApi");
		if (api != null)
		{
			result = await api.TestConnectionAsync(settings);
		}
		else
		{
			var temp = new LlmApi();
			AddChild(temp);
			result = await temp.TestConnectionAsync(settings);
			temp.QueueFree();
		}

		_statusLabel.Text = result;
		_testButton.Disabled = false;
	}

	private void OnClearDataPressed()
	{
		WorldProgress.ClearHighScore();
		_statusLabel.Text = "Cleared saved data (world reset high score reset).";
	}

	private void OnBackPressed()
	{
		var err = GetTree().ChangeSceneToFile(MainMenuScenePath);
		if (err != Error.Ok)
			GD.PushError($"Failed to open main menu '{MainMenuScenePath}': {err}");
	}
}
