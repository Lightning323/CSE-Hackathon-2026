using Godot;
using System;
using System.Threading.Tasks;

namespace GridQuest.Menus.MainMenu;

public partial class GameGenerationScene : Control
{
	private const string MainMenuScenePath = "res://menus/mainMenu/MainMenu.tscn";

	private ProgressBar _progressBar;
	private Label _stepLabel;
	private Label _detailLabel;

	public override async void _Ready()
	{
		_progressBar = GetNode<ProgressBar>("%ProgressBar");
		_stepLabel = GetNode<Label>("%StepLabel");
		_detailLabel = GetNode<Label>("%DetailLabel");

		await RunGenerationAsync();
	}

	private async Task RunGenerationAsync()
	{
		var session = GameSession.Instance ?? GetNodeOrNull<GameSession>("/root/GameSession");
		var title = session?.PendingNewSaveTitle ?? "New Save";
		var description = session?.PendingNewSaveDescription ?? "";

		try
		{
			SetStatus(5, "Preparing save folder...", $"Creating a new save for '{title}'.");
			await Task.Delay(150);

			var info = SaveManager.CreateSave(title, description);
			SetStatus(25, "Save folder created.", $"Folder: {info.FolderPath}");

			SetStatus(40, "Generating campaign story...", "Requesting story data from the configured LLM (fallback used if unavailable).");
			var api = LlmApi.Instance ?? GetNodeOrNull<LlmApi>("/root/LlmApi");
			var campaign = await StoryGenerator.GenerateCampaignAsync(info.Title, info.Description, api);

			SetStatus(75, "Writing story.json...", "Saving generated campaign and progression data.");
			await Task.Delay(100);
			SaveManager.WriteStoryJson(info, StoryGenerator.SerializeCampaign(campaign));

			SetStatus(100, "Generation complete.", "Returning to main menu.");
			if (session != null)
			{
				session.LastCreatedSaveId = info.Id;
				session.PendingNewSaveTitle = null;
				session.PendingNewSaveDescription = null;
			}
			await Task.Delay(300);
		}
		catch (Exception e)
		{
			GD.PushError($"Game generation failed: {e.Message}");
			SetStatus(100, "Generation failed.", "An error occurred. Returning to main menu.");
			if (session != null)
			{
				session.PendingNewSaveTitle = null;
				session.PendingNewSaveDescription = null;
			}
			await Task.Delay(1200);
		}

		var err = GetTree().ChangeSceneToFile(MainMenuScenePath);
		if (err != Error.Ok)
			GD.PushError($"Failed to open main menu '{MainMenuScenePath}': {err}");
	}

	private void SetStatus(double progress, string step, string detail)
	{
		_progressBar.Value = progress;
		_stepLabel.Text = step;
		_detailLabel.Text = detail ?? "";
	}
}
