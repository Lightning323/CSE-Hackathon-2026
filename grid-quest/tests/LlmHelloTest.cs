using Godot;
using GridQuest.Menus.MainMenu;
using System;
using System.Threading.Tasks;

public partial class LlmHelloTest : Node
{
	public override async void _Ready()
	{
		try
		{
			var settings = LlmSettingsStore.Load();
			GD.Print($"[LLM] Provider: {settings.Provider}");
			GD.Print($"[LLM] Base URL: {settings.BaseUrl}");
			GD.Print($"[LLM] Model: {settings.Model}");

			var api = new LlmApi();
			AddChild(api);

			GD.Print("[LLM] Sending hello world prompt...");
			var response = await api.ChatAsync("Say hello world.", "You are a helpful assistant. Keep replies brief.");
			GD.Print("[LLM] Response:");
			GD.Print(response ?? "(null)");

			api.QueueFree();
			GetTree().Quit();
		}
		catch (Exception e)
		{
			GD.PushError($"[LLM] Test failed: {e}");
			GD.PrintErr($"[LLM] Test failed: {e.Message}");
			GetTree().Quit(1);
		}
	}
}
