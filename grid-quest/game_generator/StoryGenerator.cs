using Godot;
using GridQuest.Menus.MainMenu;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GridQuest.GameGenerator;

public sealed class StoryCampaign
{
	[JsonPropertyName("campaign_id")] public string CampaignId { get; set; } = "act_1_exploration";
	[JsonPropertyName("title")] public string Title { get; set; } = "The Outer Rim Protocol";
	[JsonPropertyName("description")] public string Description { get; set; } = "A campaign tracking the descent into the automated sector core.";
	[JsonPropertyName("setup_story")] public string SetupStory { get; set; } = "Communications have gone dark across Sector 4. You must bootstrap local telemetry nodes to trace the signal.";
	[JsonPropertyName("goal")] public string Goal { get; set; } = "Restore the central uplink before total system purge.";
	[JsonPropertyName("progression")] public List<StoryStage> Progression { get; set; } = new();
}

public sealed class StoryStage
{
	[JsonPropertyName("stage_id")] public string StageId { get; set; } = "stage_01_bootstrap";
	[JsonPropertyName("title")] public string Title { get; set; } = "Cold Boot";
	[JsonPropertyName("description")] public string Description { get; set; } = "Initialize local power grids and bypass legacy security locks.";
	[JsonPropertyName("difficulty")] public string Difficulty { get; set; } = "medium";
	[JsonPropertyName("map")] public string Map { get; set; } = "sector_4_sublevel_a";
	[JsonPropertyName("objectives")] public List<string> Objectives { get; set; } = new();
	[JsonPropertyName("rewards")] public StoryRewards Rewards { get; set; } = new();
	[JsonPropertyName("modifiers")] public StoryModifiers Modifiers { get; set; } = new();
}

public sealed class StoryRewards
{
	[JsonPropertyName("unlocks")] public List<string> Unlocks { get; set; } = new();
	[JsonPropertyName("items")] public List<string> Items { get; set; } = new();
}

public sealed class StoryModifiers
{
	[JsonPropertyName("time_limit_seconds")] public int? TimeLimitSeconds { get; set; }
	[JsonPropertyName("stealth_enabled")] public bool? StealthEnabled { get; set; }
}

public static class StoryGenerator
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
	};

	public static async Task<StoryCampaign> GenerateCampaignAsync(string title, string description, LlmApi llmApi = null)
	{
		title = string.IsNullOrWhiteSpace(title) ? "New Adventure" : title.Trim();
		description = string.IsNullOrWhiteSpace(description) ? "A new Grid Quest campaign." : description.Trim();

		llmApi ??= LlmApi.Instance;
		if (llmApi != null)
		{
			try
			{
				var prompt =
					"Generate a game campaign story JSON for a grid-based adventure game. " +
					"Return ONLY valid JSON with this exact schema and keys: " +
					"{\"campaign_id\":string,\"title\":string,\"description\":string,\"setup_story\":string,\"goal\":string,\"progression\":[{\"stage_id\":string,\"title\":string,\"description\":string,\"difficulty\":string,\"map\":string,\"objectives\":[string],\"rewards\":{\"unlocks\":[string],\"items\":[string]},\"modifiers\":{\"time_limit_seconds\":number,\"stealth_enabled\":boolean}}]}. " +
					"Include at least 1 progression stage. " +
					$"Use this save title: {title}. Use this save description/context: {description}.";
				var system = "You generate structured game narrative data. Output JSON only, no markdown fences, no commentary.";
				var response = await llmApi.ChatAsync(prompt, system);
				var parsed = TryParseCampaign(response);
				if (parsed != null)
				{
					NormalizeCampaign(parsed, title, description);
					return parsed;
				}
			}
			catch (Exception e)
			{
				GD.PushWarning($"Story LLM generation failed, using fallback: {e.Message}");
			}
		}

		var fallback = CreateFallbackCampaign(title, description);
		NormalizeCampaign(fallback, title, description);
		return fallback;
	}

	public static string SerializeCampaign(StoryCampaign campaign)
	{
		return JsonSerializer.Serialize(campaign, JsonOptions);
	}

	private static StoryCampaign TryParseCampaign(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return null;
		text = text.Trim();

		var fence = Regex.Match(text, "```(?:json)?\\s*(\\{.*\\})\\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
		if (fence.Success) text = fence.Groups[1].Value.Trim();

		if (!text.StartsWith("{"))
		{
			int start = text.IndexOf('{');
			int end = text.LastIndexOf('}');
			if (start >= 0 && end > start) text = text.Substring(start, end - start + 1);
		}

		try
		{
			return JsonSerializer.Deserialize<StoryCampaign>(text, JsonOptions);
		}
		catch
		{
			return null;
		}
	}

	private static StoryCampaign CreateFallbackCampaign(string title, string description)
	{
		return new StoryCampaign
		{
			CampaignId = "act_1_exploration",
			Title = title,
			Description = description,
			SetupStory = $"A new journey begins: {description}",
			Goal = "Complete the first expedition and uncover what lies ahead.",
			Progression = new List<StoryStage>
			{
				new StoryStage
				{
					StageId = "stage_01_bootstrap",
					Title = "Cold Boot",
					Description = "Initialize local power grids and bypass legacy security locks.",
					Difficulty = "medium",
					Map = "sector_4_sublevel_a",
					Objectives = new List<string> { "Activate backup generator", "Decrypt local terminal" },
					Rewards = new StoryRewards
					{
						Unlocks = new List<string> { "stage_02_routing" },
						Items = new List<string> { "optic_cable", "data_slate" }
					},
					Modifiers = new StoryModifiers { TimeLimitSeconds = 600, StealthEnabled = true }
				}
			}
		};
	}

	private static void NormalizeCampaign(StoryCampaign campaign, string title, string description)
	{
		campaign.CampaignId = string.IsNullOrWhiteSpace(campaign.CampaignId) ? "act_1_exploration" : campaign.CampaignId.Trim();
		campaign.Title = string.IsNullOrWhiteSpace(campaign.Title) ? title : campaign.Title.Trim();
		campaign.Description = string.IsNullOrWhiteSpace(campaign.Description) ? description : campaign.Description.Trim();
		campaign.SetupStory ??= "";
		campaign.Goal ??= "";
		campaign.Progression ??= new List<StoryStage>();
		if (campaign.Progression.Count == 0)
			campaign.Progression.Add(CreateFallbackCampaign(title, description).Progression[0]);

		foreach (var stage in campaign.Progression)
		{
			if (stage == null) continue;
			stage.StageId = string.IsNullOrWhiteSpace(stage.StageId) ? "stage_01_bootstrap" : stage.StageId;
			stage.Title ??= "";
			stage.Description ??= "";
			stage.Difficulty = string.IsNullOrWhiteSpace(stage.Difficulty) ? "medium" : stage.Difficulty;
			stage.Map ??= "sector_4_sublevel_a";
			stage.Objectives ??= new List<string>();
			stage.Rewards ??= new StoryRewards();
			stage.Rewards.Unlocks ??= new List<string>();
			stage.Rewards.Items ??= new List<string>();
			stage.Modifiers ??= new StoryModifiers();
		}
	}
}
