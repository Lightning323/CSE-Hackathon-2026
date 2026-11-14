using Godot;

public partial class DayNightCycle : CanvasModulate
{
	[Signal] public delegate void SunriseEventHandler();

	[Export] public float CycleDurationSeconds { get; set; } = 300.0f; // full day-night-day cycle
	[Export] public float StartTimeSeconds { get; set; } = 66.0f; // begin at sunrise by default

	// 0 = deep night, 1 = full day
	public float DaylightFactor { get; private set; } = 1.0f;
	public float NightFactor => 1.0f - DaylightFactor;
	public bool IsNight => NightFactor >= 0.55f;

	private float _time;
	private bool _wasDaytime;

	public override void _Ready()
	{
		AddToGroup("day_night_cycle");
		_time = StartTimeSeconds;
		UpdateCycle(0.0f);
		_wasDaytime = DaylightFactor >= 0.45f;
	}

	public override void _Process(double delta)
	{
		UpdateCycle((float)delta);
	}

	private void UpdateCycle(float delta)
	{
		float duration = Mathf.Max(1.0f, CycleDurationSeconds);
		_time = Mathf.PosMod(_time + delta, duration);
		float t = _time / duration; // 0..1 over full cycle

		Color = EvaluateColor(t, out float daylight);
		DaylightFactor = daylight;

		bool isDaytime = DaylightFactor >= 0.45f;
		if (isDaytime && !_wasDaytime)
		{
			WorldProgress.RegisterWorldDay(WorldProgress.CurrentWorldDay + 1);
			EmitSignal(SignalName.Sunrise);
		}
		_wasDaytime = isDaytime;
	}

	public int GetCurrentWorldDay() => WorldProgress.CurrentWorldDay;

	public void SetToSunrise(bool emitSunriseEvent = false)
	{
		SetNormalizedTime(0.22f, emitSunriseEvent);
	}

	public void SetToMidday() => SetNormalizedTime(0.50f, emitSunriseEvent: false);
	public void SetToSunset() => SetNormalizedTime(0.80f, emitSunriseEvent: false);
	public void SetToMidnight() => SetNormalizedTime(0.00f, emitSunriseEvent: false);

	public void SetNormalizedTime(float normalized, bool emitSunriseEvent = false)
	{
		float duration = Mathf.Max(1.0f, CycleDurationSeconds);
		_time = Mathf.PosMod(normalized, 1.0f) * duration;
		float t = _time / duration;
		Color = EvaluateColor(t, out float daylight);
		DaylightFactor = daylight;
		bool isDaytime = DaylightFactor >= 0.45f;
		if (emitSunriseEvent && isDaytime && !_wasDaytime)
		{
			WorldProgress.RegisterWorldDay(WorldProgress.CurrentWorldDay + 1);
			EmitSignal(SignalName.Sunrise);
		}
		_wasDaytime = isDaytime;
	}

	public float GetEnemySpawnMultiplier()
	{
		// Slightly more enemies during night, smooth ramp.
		return Mathf.Lerp(1.0f, 1.35f, NightFactor);
	}

	public int GetAdjustedEnemyCount(int baseCount, bool minimum = false)
	{
		float mult = GetEnemySpawnMultiplier();
		if (minimum)
			mult = Mathf.Lerp(1.0f, 1.2f, NightFactor);
		return Mathf.Max(1, Mathf.CeilToInt(baseCount * mult));
	}

	private static Color EvaluateColor(float t, out float daylight)
	{
		// Keyframes over full cycle:
		// 0.00 deep night, 0.20 sunrise, 0.35 morning/day, 0.50 noon, 0.65 afternoon, 0.80 sunset, 1.00 deep night
		Color night = new(0.16f, 0.18f, 0.34f, 1.0f);
		Color preDawn = new(0.22f, 0.22f, 0.38f, 1.0f);
		Color sunrise = new(1.00f, 0.84f, 0.45f, 1.0f); // brighter yellow sunrise
		Color morning = new(1.00f, 0.95f, 0.78f, 1.0f);
		Color day = new(1.00f, 1.00f, 1.00f, 1.0f);
		Color lateAfternoon = new(1.00f, 0.93f, 0.76f, 1.0f);
		Color sunset = new(1.00f, 0.58f, 0.28f, 1.0f); // orangish sunset
		Color dusk = new(0.42f, 0.30f, 0.38f, 1.0f);

		if (t < 0.12f)
		{
			float k = Smooth01(t / 0.12f);
			daylight = Mathf.Lerp(0.05f, 0.08f, k);
			return night.Lerp(preDawn, k);
		}
		if (t < 0.22f)
		{
			float k = Smooth01((t - 0.12f) / 0.10f);
			daylight = Mathf.Lerp(0.08f, 0.55f, k);
			return preDawn.Lerp(sunrise, k);
		}
		if (t < 0.32f)
		{
			float k = Smooth01((t - 0.22f) / 0.10f);
			daylight = Mathf.Lerp(0.55f, 0.90f, k);
			return sunrise.Lerp(morning, k);
		}
		if (t < 0.50f)
		{
			float k = Smooth01((t - 0.32f) / 0.18f);
			daylight = Mathf.Lerp(0.90f, 1.00f, k);
			return morning.Lerp(day, k);
		}
		if (t < 0.68f)
		{
			float k = Smooth01((t - 0.50f) / 0.18f);
			daylight = Mathf.Lerp(1.00f, 0.88f, k);
			return day.Lerp(lateAfternoon, k);
		}
		if (t < 0.80f)
		{
			float k = Smooth01((t - 0.68f) / 0.12f);
			daylight = Mathf.Lerp(0.88f, 0.45f, k);
			return lateAfternoon.Lerp(sunset, k);
		}
		if (t < 0.90f)
		{
			float k = Smooth01((t - 0.80f) / 0.10f);
			daylight = Mathf.Lerp(0.45f, 0.12f, k);
			return sunset.Lerp(dusk, k);
		}
		{
			float k = Smooth01((t - 0.90f) / 0.10f);
			daylight = Mathf.Lerp(0.12f, 0.05f, k);
			return dusk.Lerp(night, k);
		}
	}

	private static float Smooth01(float x)
	{
		x = Mathf.Clamp(x, 0.0f, 1.0f);
		return x * x * (3.0f - 2.0f * x);
	}
}
