using Godot;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace GridQuest.Menus.MainMenu;

public partial class LlmApi : Node
{
	public static LlmApi Instance { get; private set; }

	private static readonly System.Net.Http.HttpClient Http = new();

	public override void _Ready()
	{
		Instance = this;
	}

	public async Task<string> ChatAsync(string prompt, string systemPrompt = null, LlmSettings settingsOverride = null)
	{
		var settings = LlmSettingsStore.Normalize(settingsOverride ?? LlmSettingsStore.Load());
		if (string.IsNullOrWhiteSpace(prompt))
			throw new ArgumentException("Prompt is empty.", nameof(prompt));

		if (string.Equals(settings.Provider, "Anthropic", StringComparison.OrdinalIgnoreCase))
			return await ChatAnthropicAsync(settings, prompt, systemPrompt);

		return await ChatOpenAiCompatibleAsync(settings, prompt, systemPrompt);
	}

	public async Task<string> TestConnectionAsync(LlmSettings settings)
	{
		try
		{
			var response = await ChatAsync("Reply with the single word: pong", "You are a connectivity test.", settings);
			return string.IsNullOrWhiteSpace(response) ? "Connected, but model returned an empty response." : $"Success: {TrimForStatus(response)}";
		}
		catch (Exception e)
		{
			return $"Failed: {e.Message}";
		}
	}

	private static async Task<string> ChatOpenAiCompatibleAsync(LlmSettings settings, string prompt, string systemPrompt)
	{
		var url = settings.BaseUrl.TrimEnd('/') + "/chat/completions";
		using var req = new HttpRequestMessage(HttpMethod.Post, url);

		if (!string.IsNullOrWhiteSpace(settings.ApiKey))
			req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey);

		var messages = new JsonArray();
		if (!string.IsNullOrWhiteSpace(systemPrompt))
			messages.Add(new JsonObject { ["role"] = "system", ["content"] = systemPrompt });
		messages.Add(new JsonObject { ["role"] = "user", ["content"] = prompt });

		var payload = new JsonObject
		{
			["model"] = settings.Model,
			["messages"] = messages,
			["temperature"] = 0.2,
			["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = settings.EnableThinking },
		};
		if (!settings.EnableThinking)
		{
			// Best-effort hints for common OpenAI-compatible reasoning models/endpoints.
			payload["reasoning_effort"] = "none";
		}

		req.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
		using var res = await Http.SendAsync(req);
		var body = await res.Content.ReadAsStringAsync();
		if (!res.IsSuccessStatusCode)
			throw new Exception($"HTTP {(int)res.StatusCode}: {TrimForStatus(body)}");

		using var doc = JsonDocument.Parse(body);
		if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
		{
			var msg = choices[0].GetProperty("message");
			if (msg.TryGetProperty("content", out var content))
				return content.GetString() ?? "";
		}

		throw new Exception($"Unexpected response: {TrimForStatus(body)}");
	}

	private static async Task<string> ChatAnthropicAsync(LlmSettings settings, string prompt, string systemPrompt)
	{
		var url = settings.BaseUrl.TrimEnd('/') + "/messages";
		using var req = new HttpRequestMessage(HttpMethod.Post, url);

		if (!string.IsNullOrWhiteSpace(settings.ApiKey))
			req.Headers.TryAddWithoutValidation("x-api-key", settings.ApiKey);
		req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

		var payload = new JsonObject
		{
			["model"] = settings.Model,
			["max_tokens"] = settings.EnableThinking ? 1024 : 256,
			["system"] = systemPrompt ?? "",
			["messages"] = new JsonArray
			{
				new JsonObject { ["role"] = "user", ["content"] = prompt }
			}
		};
		if (settings.EnableThinking)
		{
			payload["thinking"] = new JsonObject
			{
				["type"] = "enabled",
				["budget_tokens"] = 512,
			};
		}

		req.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
		using var res = await Http.SendAsync(req);
		var body = await res.Content.ReadAsStringAsync();
		if (!res.IsSuccessStatusCode)
			throw new Exception($"HTTP {(int)res.StatusCode}: {TrimForStatus(body)}");

		using var doc = JsonDocument.Parse(body);
		if (doc.RootElement.TryGetProperty("content", out var contentArr) && contentArr.GetArrayLength() > 0)
		{
			var sb = new StringBuilder();
			foreach (var part in contentArr.EnumerateArray())
			{
				if (part.TryGetProperty("type", out var type) && type.GetString() == "text" && part.TryGetProperty("text", out var text))
					sb.Append(text.GetString());
			}
			if (sb.Length > 0)
				return sb.ToString();
		}

		throw new Exception($"Unexpected response: {TrimForStatus(body)}");
	}

	private static string TrimForStatus(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return "(empty)";
		text = text.Replace("\r", " ").Replace("\n", " ").Trim();
		return text.Length <= 240 ? text : text[..240] + "...";
	}
}
