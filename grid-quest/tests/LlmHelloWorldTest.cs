using Godot;
using GridQuest.GameGenerator.Llm;
using System;
using System.Threading.Tasks;

public partial class LlmHelloWorldTest : Node
{
	
	public override async void _Ready()
	{
		int chunkCount = 0;
		try
		{
			var settings = LlmSettingsStore.Load();
			GD.Print($"[LLM] Provider: {settings.Provider}");
			GD.Print($"[LLM] Base URL: {settings.BaseUrl}");
			GD.Print($"[LLM] Model: {settings.Model}");
			GD.Print($"[LLM] EnableThinking: {settings.EnableThinking}");
			GD.Print("[LLM] Starting streaming hello world test...");

			var api = new LlmApi();
			AddChild(api);

			void OnChunk(string chunk)
			{
				chunkCount++;
			}

			api.StreamChunkReceived += OnChunk;
			string response;
			try
			{
				GD.Print("[LLM] --- streamed output start ---");
				response = await api.ChatAsync(
					"Say hello world.",
					"You are a helpful assistant. Reply with a story",
					settings,
					OnChunk);
				GD.Print("");
				GD.Print("[LLM] --- streamed output end ---");
			}
			finally
			{
				api.StreamChunkReceived -= OnChunk;
			}

			GD.Print($"[LLM] Chunks received: {chunkCount}");
			GD.Print($"[LLM] Final response: {response ?? "(null)"}");

			if (chunkCount <= 0)
			{
				GD.PushError("[LLM] No streamed chunks were received.");
				api.QueueFree();
				GetTree().Quit(1);
				return;
			}

			api.QueueFree();
			GetTree().Quit();
		}
		catch (Exception e)
		{
			GD.PushError($"[LLM] Test failed: {e}");
			GD.PrintErr($"[LLM] Test failed: {e.Message}");
			GD.PrintErr($"[LLM] Chunks received before failure: {chunkCount}");
			GetTree().Quit(1);
		}
	}
}
