using Godot;
using System.Threading.Tasks;

using System;

public partial class BossFightUi : Control
{
	private Label _eventLabel;
	private ColorRect _redTint;
	private ColorRect _blackFlash;
	private Control _bossBarContainer;
	private Label _bossNameLabel;
	private ProgressBar _bossHealthBar;

	public override void _Ready()
	{
		AddToGroup("boss_fight_ui");
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);

		_redTint = GetNodeOrNull<ColorRect>("RedTint");
		_blackFlash = GetNodeOrNull<ColorRect>("BlackFlash");
		_eventLabel = GetNodeOrNull<Label>("CenterText");
		_bossBarContainer = GetNodeOrNull<Control>("BossBarContainer");
		_bossNameLabel = GetNodeOrNull<Label>("BossBarContainer/VBox/BossNameLabel");
		_bossHealthBar = GetNodeOrNull<ProgressBar>("BossBarContainer/VBox/BossHealthBar");

		if (_redTint != null) { _redTint.Visible = false; _redTint.Modulate = new Color(1, 1, 1, 0); }
		if (_blackFlash != null) { _blackFlash.Visible = false; _blackFlash.Modulate = new Color(1, 1, 1, 0); }
		if (_eventLabel != null) { _eventLabel.Visible = false; _eventLabel.Modulate = new Color(1, 1, 1, 0); }
		if (_bossBarContainer != null) { _bossBarContainer.Visible = false; _bossBarContainer.Modulate = new Color(1, 1, 1, 1); }
		if (_bossHealthBar != null) { _bossHealthBar.Value = 0; _bossHealthBar.MaxValue = 1; }
	}

	public async Task ShowHellIsComingSequenceAsync(Action duringBlackFlash = null)
	{
		await ShowIntroSequenceAsync("Hell is coming", Colors.White, 0.5f, 1.0f, 0.5f, onBlackoutStart: null, duringBlackFlash: duringBlackFlash, onBlackoutEnd: null);
	}

	public async Task ShowExecutionerIntroSequenceAsync(Action onBlackoutStart = null, Action duringBlackFlash = null)
	{
		await ShowExecutionerIntroSequenceAsync(onBlackoutStart, duringBlackFlash, onBlackoutEnd: null, blackoutHoldSeconds: 1.2f);
	}

	public async Task ShowExecutionerIntroSequenceAsync(
		Action onBlackoutStart = null,
		Action duringBlackFlash = null,
		Action onBlackoutEnd = null,
		float blackoutHoldSeconds = 6.0f)
	{
		await ShowIntroSequenceAsync(
			"Judge... Jury...",
			Colors.White,
			0.4f,
			blackoutHoldSeconds,
			0.0f,
			onBlackoutStart,
			duringBlackFlash,
			onBlackoutEnd,
			keepBlackoutAtEnd: false);
	}

	private async Task ShowIntroSequenceAsync(
		string text,
		Color textColor,
		float fadeInSeconds,
		float holdSeconds,
		float fadeOutSeconds,
		Action onBlackoutStart,
		Action duringBlackFlash,
		Action onBlackoutEnd = null,
		bool keepBlackoutAtEnd = false)
	{
		if (_eventLabel == null) return;
		_eventLabel.Text = text;
		_eventLabel.Modulate = new Color(textColor.R, textColor.G, textColor.B, 0.0f);
		_eventLabel.Visible = true;
		_eventLabel.AddThemeColorOverride("font_color", textColor);

		if (_blackFlash != null)
		{
			_blackFlash.Visible = true;
			_blackFlash.Modulate = new Color(1, 1, 1, 1);
		}
		onBlackoutStart?.Invoke();

		if (fadeInSeconds > 0.001f)
		{
			var tweenIn = CreateTween();
			tweenIn.TweenProperty(_eventLabel, "modulate:a", 1.0f, fadeInSeconds);
			await ToSignal(tweenIn, Tween.SignalName.Finished);
		}
		else
		{
			_eventLabel.Modulate = new Color(textColor.R, textColor.G, textColor.B, 1.0f);
		}

		duringBlackFlash?.Invoke();
		if (holdSeconds > 0.001f)
			await ToSignal(GetTree().CreateTimer(holdSeconds, true, false, true), SceneTreeTimer.SignalName.Timeout);

		_eventLabel.Visible = false;
		_eventLabel.Modulate = new Color(textColor.R, textColor.G, textColor.B, 0.0f);

		if (fadeOutSeconds > 0.001f)
		{
			if (_blackFlash != null)
			{
				var tweenOut = CreateTween();
				tweenOut.TweenProperty(_blackFlash, "modulate:a", 0.0f, fadeOutSeconds);
				await ToSignal(tweenOut, Tween.SignalName.Finished);
			}
		}
		else if (_blackFlash != null && !keepBlackoutAtEnd)
		{
			_blackFlash.Visible = false;
			_blackFlash.Modulate = new Color(1, 1, 1, 0);
		}

		if (_blackFlash != null && !keepBlackoutAtEnd)
		{
			_blackFlash.Visible = false;
			_blackFlash.Modulate = new Color(1, 1, 1, 0);
		}

		onBlackoutEnd?.Invoke();
	}

	public async Task ShowBossBarAsync(string bossName, int totalHealth)
	{
		if (_bossBarContainer == null || _bossHealthBar == null) return;
		if (_bossNameLabel != null) _bossNameLabel.Text = bossName;
		_bossHealthBar.MaxValue = Mathf.Max(1, totalHealth);
		_bossHealthBar.Value = 0;
		_bossBarContainer.Visible = true;

		// Reveal/expand bar from left to right.
		_bossBarContainer.Scale = new Vector2(0.0f, 1.0f);
		_bossBarContainer.PivotOffset = new Vector2(0, _bossBarContainer.Size.Y / 2.0f);
		var tween = CreateTween();
		tween.TweenProperty(_bossBarContainer, "scale:x", 1.0f, 0.8f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		await ToSignal(tween, Tween.SignalName.Finished);

		var fillTween = CreateTween();
		fillTween.TweenProperty(_bossHealthBar, "value", (double)totalHealth, 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		await ToSignal(fillTween, Tween.SignalName.Finished);
	}

	public void UpdateBossHealth(int current, int max)
	{
		if (_bossHealthBar == null) return;
		_bossHealthBar.MaxValue = Mathf.Max(1, max);
		_bossHealthBar.Value = Mathf.Clamp(current, 0, max);
	}

	public async Task FadeRedTintAsync(bool on, float duration = 0.8f)
	{
		if (_redTint == null) return;
		_redTint.Visible = true;
		float target = on ? 0.18f : 0.0f;
		var tween = CreateTween();
		tween.TweenProperty(_redTint, "modulate:a", target, duration);
		await ToSignal(tween, Tween.SignalName.Finished);
		if (!on) _redTint.Visible = false;
	}

	public void HideBossBar()
	{
		if (_bossBarContainer != null) _bossBarContainer.Visible = false;
	}

	public void ResetUi()
	{
		if (_eventLabel != null) { _eventLabel.Visible = false; _eventLabel.Modulate = new Color(1, 1, 1, 0); }
		if (_redTint != null) { _redTint.Visible = false; _redTint.Modulate = new Color(1, 1, 1, 0); }
		if (_blackFlash != null) { _blackFlash.Visible = false; _blackFlash.Modulate = new Color(1, 1, 1, 0); }
		HideBossBar();
	}
}
