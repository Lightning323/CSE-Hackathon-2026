using Godot;
using System;
using System.IO;
using System.Text.Json;

namespace GridQuest.GameGenerator.Llm;

public sealed class LlmSettings
{
	public string Provider { get; set; } = "OpenAI";
	public string BaseUrl { get; set; } = "https://api.openai.com/v1";
	public string ApiKey { get; set; } = "";
	public string Model { get; set; } = "gpt-4o-mini";
	public bool EnableThinking { get; set; } = false;
}

public static class LlmSettingsStore
{
	private static string SettingsPath => Path.Combine("llm_settings.json");

	public static LlmSettings Load()
	{
		try
		{
			if (File.Exists(SettingsPath))
			{
				var json = File.ReadAllText(SettingsPath);
				var settings = JsonSerializer.Deserialize<LlmSettings>(json);
				if (settings != null)
					return Normalize(settings);
			}
		}
		catch (Exception e)
		{
			GD.PushWarning($"Failed to load LLM settings: {e.Message}");
		}
		return Normalize(new LlmSettings());
	}

	public static void Save(LlmSettings settings)
	{
		try
		{
			settings = Normalize(settings ?? new LlmSettings());
			Directory.CreateDirectory(OS.GetUserDataDir());
			var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
			File.WriteAllText(SettingsPath, json);
		}
		catch (Exception e)
		{
			GD.PushError($"Failed to save LLM settings: {e.Message}");
		}
	}

	public static LlmSettings Normalize(LlmSettings settings)
	{
		settings.Provider ??= "OpenAI";
		settings.BaseUrl ??= "";
		settings.ApiKey ??= "";
		settings.Model ??= "";

		if (string.IsNullOrWhiteSpace(settings.BaseUrl))
		{
			settings.BaseUrl = settings.Provider switch
			{
				"Anthropic" => "https://api.anthropic.com/v1",
				"LM Studio" => "http://localhost:1234/v1",
				"Ollama" => "http://localhost:11434",
				_ => "https://api.openai.com/v1",
			};
		}

		if (string.IsNullOrWhiteSpace(settings.Model))
		{
			settings.Model = settings.Provider switch
			{
				"Anthropic" => "claude-3-5-haiku-latest",
				"LM Studio" => "local-model",
				"Ollama" => "qwen3:8b",
				_ => "gpt-4o-mini",
			};
		}

		settings.BaseUrl = settings.BaseUrl.Trim().TrimEnd('/');
		return settings;
	}
}
