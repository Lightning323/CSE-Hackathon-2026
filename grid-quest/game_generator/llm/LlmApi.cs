using Godot;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace GridQuest.GameGenerator.Llm;

public partial class LlmApi : Node
{
	public static LlmApi Instance { get; private set; }

	private static readonly System.Net.Http.HttpClient Http = new();

	public event Action<string> StreamChunkReceived;

	public override void _Ready()
	{
		Instance = this;
	}

	public async Task<string> ChatAsync(string prompt, string systemPrompt = null, LlmSettings settingsOverride = null, Action<string> onChunk = null)
	{
		var settings = LlmSettingsStore.Normalize(settingsOverride ?? LlmSettingsStore.Load());
		if (string.IsNullOrWhiteSpace(prompt))
			throw new ArgumentException("Prompt is empty.", nameof(prompt));

		if (string.Equals(settings.Provider, "Anthropic", StringComparison.OrdinalIgnoreCase))
			return await ChatAnthropicAsync(settings, prompt, systemPrompt, onChunk);

		return await ChatOpenAiCompatibleAsync(settings, prompt, systemPrompt, onChunk);
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

	private async Task<string> ChatOpenAiCompatibleAsync(LlmSettings settings, string prompt, string systemPrompt, Action<string> onChunk)
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
			["stream"] = true,
			["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = settings.EnableThinking },
		};
		if (!settings.EnableThinking)
			payload["reasoning_effort"] = "none";

		req.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
		using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
		if (!res.IsSuccessStatusCode)
		{
			var errBody = await res.Content.ReadAsStringAsync();
			throw new Exception($"HTTP {(int)res.StatusCode}: {TrimForStatus(errBody)}");
		}

		await using var stream = await res.Content.ReadAsStreamAsync();
		using var reader = new StreamReader(stream);
		var full = new StringBuilder();
		string line;
		while ((line = await reader.ReadLineAsync()) != null)
		{
			if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
				continue;
			var data = line.Substring("data:".Length).Trim();
			if (data == "[DONE]")
				break;
			try
			{
				using var doc = JsonDocument.Parse(data);
				if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
				{
					var choice = choices[0];
					if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var contentEl))
					{
						var chunk = contentEl.GetString();
						if (!string.IsNullOrEmpty(chunk))
							EmitChunk(chunk, full, onChunk);
					}
				}
			}
			catch
			{
			}
		}

		GD.Print("");
		if (full.Length == 0)
			throw new Exception("Empty streamed response.");
		return full.ToString();
	}

	private async Task<string> ChatAnthropicAsync(LlmSettings settings, string prompt, string systemPrompt, Action<string> onChunk)
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
			["stream"] = true,
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
		using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
		if (!res.IsSuccessStatusCode)
		{
			var errBody = await res.Content.ReadAsStringAsync();
			throw new Exception($"HTTP {(int)res.StatusCode}: {TrimForStatus(errBody)}");
		}

		await using var stream = await res.Content.ReadAsStreamAsync();
		using var reader = new StreamReader(stream);
		var full = new StringBuilder();
		string line;
		while ((line = await reader.ReadLineAsync()) != null)
		{
			if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
				continue;
			var data = line.Substring("data:".Length).Trim();
			if (data == "[DONE]")
				break;
			try
			{
				using var doc = JsonDocument.Parse(data);
				var root = doc.RootElement;
				if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "content_block_delta" &&
					root.TryGetProperty("delta", out var delta) && delta.TryGetProperty("text", out var textEl))
				{
					var chunk = textEl.GetString();
					if (!string.IsNullOrEmpty(chunk))
						EmitChunk(chunk, full, onChunk);
				}
			}
			catch
			{
			}
		}

		GD.Print("");
		if (full.Length == 0)
			throw new Exception("Empty streamed response.");
		return full.ToString();
	}

	private void EmitChunk(string chunk, StringBuilder full, Action<string> onChunk)
	{
		full.Append(chunk);
		try { GD.PrintRaw(chunk); } catch { }
		try { StreamChunkReceived?.Invoke(chunk); } catch { }
		try { onChunk?.Invoke(chunk); } catch { }
	}

	private static string TrimForStatus(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return "(empty)";
		text = text.Replace("\r", " ").Replace("\n", " ").Trim();
		return text.Length <= 240 ? text : text[..240] + "...";
	}
}
