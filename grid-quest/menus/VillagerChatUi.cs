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
	private string _activeAdviceQuestion;

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
		_activeAdviceQuestion = "";
		Visible = true;
		GetTree().Paused = true;

		UpdateHeader();
		if (_transcript != null)
		{
			_transcript.Clear();
			_transcript.AppendText($"[b]{villager.DisplayNameForChat()}[/b]: {EscapeBb(villager.CurrentDialogueText)}\n");
		}
		if (_input != null)
		{
			_input.Text = "";
			_input.Editable = true;
			_input.GrabFocus();
		}
		if (_sendButton != null) _sendButton.Disabled = false;

		// Trust/advice system: when calm, villagers may ask for life advice and rate the player's response.
		if (!villager.IsFleeing && villager.CurrentDialogueEmotion != Villager.DialogueEmotion.Chased)
		{
			villager.EnsureAdviceRequestReady();
			if (villager.HasPendingAdviceRequest)
			{
				_activeAdviceQuestion = villager.ConsumeAdviceQuestion();
				if (!string.IsNullOrWhiteSpace(_activeAdviceQuestion) && _transcript != null)
				{
					_transcript.AppendText($"\n[b]{villager.DisplayNameForChat()}[/b]: {EscapeBb(_activeAdviceQuestion)}\n[i](Give life advice to build trust.)[/i]\n");
				}
			}
		}

		UpdateStatusPrompt();
	}

	public void Close()
	{
		if (!Visible) return;
		Visible = false;
		_busy = false;
		_villager = null;
		_activeAdviceQuestion = "";
		GetTree().Paused = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;

		// Close only via explicit cancel/pause actions (Esc / controller B/Start), not interact/F,
		// so players can type freely in the text field. Other systems already ignore input while
		// villager chat is visible, so we must not consume normal typing events here.
		bool closePressed = @event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause_menu");
		if (!closePressed && @event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
			closePressed = true;

		if (closePressed)
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

		string answer;
		if (!string.IsNullOrWhiteSpace(_activeAdviceQuestion))
		{
			answer = await AskForAdviceEvaluationAsync(_activeAdviceQuestion, question);
			_activeAdviceQuestion = ""; // one advice rating per prompt in this chat turn
		}
		else
		{
			answer = await AskVillagerAsync(question);
		}

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
			UpdateHeader();
			UpdateStatusPrompt();
		}
	}

	private void UpdateHeader()
	{
		if (_titleLabel == null) return;
		if (_villager == null || !IsInstanceValid(_villager))
		{
			_titleLabel.Text = "Villager";
			return;
		}
		_titleLabel.Text = $"{_villager.DisplayNameForChat()}  [Trust {_villager.Trust}/100]";
	}

	private void UpdateStatusPrompt()
	{
		if (_statusLabel == null) return;
		if (_villager == null || !IsInstanceValid(_villager))
		{
			_statusLabel.Text = "Ask another question or press Esc/F to close.";
			return;
		}

		if (_villager.IsFleeing || _villager.CurrentDialogueEmotion == Villager.DialogueEmotion.Chased)
		{
			_statusLabel.Text = $"Trust: {_villager.Trust}/100 — This villager is shaken right now. You can still chat.";
			return;
		}

		if (!string.IsNullOrWhiteSpace(_activeAdviceQuestion))
			_statusLabel.Text = $"Trust: {_villager.Trust}/100 — Share your advice to help this villager (bigger trust gain).";
		else
			_statusLabel.Text = $"Trust: {_villager.Trust}/100 — Good conversations build trust. Press Esc/F to close.";
	}

	private async Task<string> AskForAdviceEvaluationAsync(string adviceQuestion, string playerAdvice)
	{
		try
		{
			var api = LlmApi.Instance ?? GetNodeOrNull<LlmApi>("/root/LlmApi");
			if (api == null)
			{
				string reply = _villager.ApplyAdviceEvaluation("", playerAdvice);
				AppendStreamChunk(reply);
				return reply;
			}

			string systemPrompt = _villager.BuildAdviceEvaluationSystemPrompt(adviceQuestion);
			string prompt = _villager.BuildAdviceEvaluationPrompt(adviceQuestion, playerAdvice);
			string result = await api.ChatAsync(prompt, systemPrompt, null, null);
			string replyText = _villager.ApplyAdviceEvaluation(result, playerAdvice);
			AppendStreamChunk(replyText);
			return replyText;
		}
		catch (Exception e)
		{
			string fallback = _villager.ApplyAdviceEvaluation("", playerAdvice);
			string msg = $"{fallback} (rating fallback: {e.Message})";
			AppendStreamChunk(msg);
			return msg;
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
				string fallbackReply = msg;
				_villager.ApplyConversationTrust(question, fallbackReply);
				AppendStreamChunk(fallbackReply);
				return fallbackReply;
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

			string reply = string.IsNullOrWhiteSpace(result) ? sb.ToString() : result;
			_villager.ApplyConversationTrust(question, reply);
			return reply;
		}
		catch (Exception e)
		{
			string msg = $"(Unable to respond: {e.Message})";
			_villager.ApplyConversationTrust(question, msg);
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
