using Godot;
using GridQuest.GameGenerator.Llm;
using GridQuest.Menus.MainMenu;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace GridQuest.GameGenerator;

public partial class GameGenerator : Control
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

			SetStatus(40, "Generating campaign story...", "Requesting story data from the configured LLM (streaming output to terminal; fallback used if unavailable).");
			var api = LlmApi.Instance ?? GetNodeOrNull<LlmApi>("/root/LlmApi");
			Action<string> onChunk = chunk =>
			{
				// Hook point for consumers that want streamed data (UI can subscribe/update incrementally here).
				if (_detailLabel != null)
					_detailLabel.Text = "Generating campaign story... (streaming to terminal)";
			};
			if (api != null) api.StreamChunkReceived += onChunk;
			StoryCampaign campaign;
			try
			{
				campaign = await StoryGenerator.GenerateCampaignAsync(info.Title, info.Description, api, onChunk);
			}
			finally
			{
				if (api != null) api.StreamChunkReceived -= onChunk;
			}

			SetStatus(75, "Writing story.json...", "Saving generated campaign and progression data.");
			await Task.Delay(100);
			SaveManager.WriteStoryJson(info, JsonSerializer.Serialize(campaign, JsonUtils.JsonOptions));

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
		GD.Print($"GameGenerator: {progress}% - {step} - {detail}");
		_progressBar.Value = progress;
		_stepLabel.Text = step;
		_detailLabel.Text = detail ?? "";
	}
}
