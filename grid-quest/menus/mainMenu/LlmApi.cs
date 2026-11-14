using Godot;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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

		var messages = new System.Collections.Generic.List<object>();
		if (!string.IsNullOrWhiteSpace(systemPrompt))
			messages.Add(new { role = "system", content = systemPrompt });
		messages.Add(new { role = "user", content = prompt });

		var payload = new
		{
			model = settings.Model,
			messages,
			temperature = 0.2,
		};

		req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
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

		var payload = new
		{
			model = settings.Model,
			max_tokens = 256,
			system = systemPrompt ?? "",
			messages = new[]
			{
				new { role = "user", content = prompt }
			}
		};

		req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
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
