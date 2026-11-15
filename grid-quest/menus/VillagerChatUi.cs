using Godot;
using GridQuest.Entities;
using System;
using System.Threading.Tasks;

public partial class VillagerChatUi : Control
{
	private LlmEntity _entity;
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

	public void Open(LlmEntity entity)
	{
		if (entity == null || !IsInstanceValid(entity)) return;
		_entity = entity;
		_busy = false;
		Visible = true;
		GetTree().Paused = true;
		if (_titleLabel != null) _titleLabel.Text = entity.Name.ToString();
		if (_transcript != null)
		{
			_transcript.Clear();
			_transcript.AppendText($"[i]{EscapeBb(entity.Name)} is here.[/i]\n");
		}
		if (_input != null)
		{
			_input.Text = "";
			_input.Editable = true;
			_input.GrabFocus();
		}
		if (_sendButton != null) _sendButton.Disabled = false;
		if (_statusLabel != null) _statusLabel.Text = "Talk to this character. Its choices and memories belong to the entity.";
	}

	public void Close()
	{
		if (!Visible) return;
		Visible = false;
		_busy = false;
		_entity = null;
		GetTree().Paused = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;
		if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause_menu") ||
			(@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private async void OnSendPressed()
	{
		if (_busy || !Visible) return;
		if (_entity == null || !IsInstanceValid(_entity))
		{
			if (_statusLabel != null) _statusLabel.Text = "This character is no longer available.";
			return;
		}

		string message = _input?.Text?.Trim() ?? string.Empty;
		if (message.Length == 0) return;
		_busy = true;
		if (_input != null) { _input.Text = ""; _input.Editable = false; }
		if (_sendButton != null) _sendButton.Disabled = true;
		if (_statusLabel != null) _statusLabel.Text = "Thinking...";
		if (_transcript != null)
			_transcript.AppendText($"\n[b]You[/b]: {EscapeBb(message)}\n[b]{EscapeBb(_entity.Name)}[/b]: ");

		try
		{
			string reply = await _entity.RespondToPlayerAsync(message);
			AppendReply(reply);
		}
		catch (Exception exception)
		{
			GD.PushWarning($"Chat request failed for '{_entity.Name}': {exception.Message}");
			AppendReply($"(Unable to respond: {exception.Message})");
		}
		finally
		{
			_busy = false;
			if (Visible)
			{
				if (_input != null) { _input.Editable = true; _input.GrabFocus(); }
				if (_sendButton != null) _sendButton.Disabled = false;
				if (_statusLabel != null) _statusLabel.Text = "Press Esc to close.";
			}
		}
	}

	private void AppendReply(string reply)
	{
		if (_transcript == null) return;
		_transcript.AppendText(EscapeBb(reply) + "\n");
		_transcript.ScrollToLine(_transcript.GetLineCount());
	}

	private static string EscapeBb(string text) => (text ?? string.Empty).Replace("[", "[lb]");
}
