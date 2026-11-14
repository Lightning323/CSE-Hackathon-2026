using Godot;
using GridQuest.GameGenerator.Llm;
using System;
using System.Text;
using System.Threading.Tasks;

public partial class VillagerChatUi : Control
{
	private Villager _villager;
	private Label _titleLabel;
	private RichTextLabel _transcript;
	private LineEdit _input;
	private Button _sendButton;
	private Button _closeButton;
	private Label _statusLabel;
	private bool _busy;

	public override void _Ready()
	{
		AddToGroup("villager_chat_ui");
		ProcessMode = ProcessModeEnum.Always;
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);

		_titleLabel = GetNodeOrNull<Label>("%TitleLabel");
		_transcript = GetNodeOrNull<RichTextLabel>("%Transcript");
		_input = GetNodeOrNull<LineEdit>("%QuestionInput");
		_sendButton = GetNodeOrNull<Button>("%SendButton");
		_closeButton = GetNodeOrNull<Button>("%CloseButton");
		_statusLabel = GetNodeOrNull<Label>("%StatusLabel");

		if (_closeButton != null) _closeButton.Pressed += Close;
		if (_sendButton != null) _sendButton.Pressed += OnSendPressed;
		if (_input != null) _input.TextSubmitted += _ => OnSendPressed();
	}

	public void Open(Villager villager)
	{
		if (villager == null || !IsInstanceValid(villager))
			return;

		_villager = villager;
		_busy = false;
		Visible = true;
		GetTree().Paused = true;

		if (_titleLabel != null)
			_titleLabel.Text = villager.DisplayNameForChat();
		if (_statusLabel != null)
			_statusLabel.Text = "Ask a question.";
		if (_transcript != null)
		{
			_transcript.Clear();
			_transcript.AppendText($"[b]{villager.DisplayNameForChat()}[/b]: {villager.CurrentDialogueText}\n");
		}
		if (_input != null)
		{
			_input.Text = "";
			_input.Editable = true;
			_input.GrabFocus();
		}
		if (_sendButton != null) _sendButton.Disabled = false;
	}

	public void Close()
	{
		if (!Visible) return;
		Visible = false;
		_busy = false;
		_villager = null;
		GetTree().Paused = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible) return;
		if (@event is InputEventKey key && key.Pressed && !key.Echo && (key.Keycode == Key.Escape || key.Keycode == Key.F))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private async void OnSendPressed()
	{
		if (_busy || !Visible) return;
		if (_villager == null || !IsInstanceValid(_villager))
		{
			if (_statusLabel != null) _statusLabel.Text = "Villager is no longer available.";
			return;
		}

		string question = _input?.Text?.Trim();
		if (string.IsNullOrEmpty(question))
			return;

		_busy = true;
		if (_input != null) { _input.Text = ""; _input.Editable = false; }
		if (_sendButton != null) _sendButton.Disabled = true;
		if (_statusLabel != null) _statusLabel.Text = "Thinking...";

		if (_transcript != null)
		{
			_transcript.AppendText($"\n[b]You[/b]: {EscapeBb(question)}\n[b]{_villager.DisplayNameForChat()}[/b]: ");
		}

		string answer = await AskVillagerAsync(question);
		if (_transcript != null)
		{
			if (string.IsNullOrEmpty(answer))
				_transcript.AppendText("(no response)");
			_transcript.AppendText("\n");
			_transcript.ScrollToLine(_transcript.GetLineCount());
		}

		_busy = false;
		if (Visible)
		{
			if (_input != null) { _input.Editable = true; _input.GrabFocus(); }
			if (_sendButton != null) _sendButton.Disabled = false;
			if (_statusLabel != null) _statusLabel.Text = "Ask another question or press Esc/F to close.";
		}
	}

	private async Task<string> AskVillagerAsync(string question)
	{
		try
		{
			var api = LlmApi.Instance ?? GetNodeOrNull<LlmApi>("/root/LlmApi");
			if (api == null)
			{
				const string msg = "I can't think clearly right now. (LLM unavailable)";
				AppendStreamChunk(msg);
				return msg;
			}

			string systemPrompt = _villager.BuildChatSystemPrompt();
			string prompt = _villager.BuildChatPrompt(question);
			var sb = new StringBuilder();
			string result = await api.ChatAsync(prompt, systemPrompt, null, chunk =>
			{
				if (chunk == null) return;
				sb.Append(chunk);
				CallDeferred(nameof(AppendStreamChunk), chunk);
			});
			if (sb.Length == 0 && !string.IsNullOrEmpty(result))
				AppendStreamChunk(result);
			return string.IsNullOrWhiteSpace(result) ? sb.ToString() : result;
		}
		catch (Exception e)
		{
			string msg = $"(Unable to respond: {e.Message})";
			CallDeferred(nameof(AppendStreamChunk), msg);
			return msg;
		}
	}

	private void AppendStreamChunk(string chunk)
	{
		if (_transcript == null || string.IsNullOrEmpty(chunk)) return;
		_transcript.AppendText(EscapeBb(chunk));
		_transcript.ScrollToLine(_transcript.GetLineCount());
	}

	private static string EscapeBb(string text)
	{
		return (text ?? "").Replace("[", "[lb]");
	}
}
