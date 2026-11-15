#nullable enable
using Godot;
using System.Collections.Generic;

namespace GridQuest.Entities;

public partial class PlayerController : Node
{
	[Export] public NodePath CameraPath { get; set; } = "Camera2D";

	private Camera2D? _camera;
	private Entity? _controlled;
	private Entity? _observed;
	private float _zoom = 2.0f;

	public Entity? ControlledEntity => _controlled;
	public Entity? ObservedEntity => _observed != null && IsInstanceValid(_observed) ? _observed : (_controlled != null && IsInstanceValid(_controlled) ? _controlled : null);

	public override void _Ready()
	{
		AddToGroup("player_controller");
		_camera = GetNodeOrNull<Camera2D>(CameraPath) ?? GetNodeOrNull<Camera2D>("Camera2D");
		if (_camera == null)
		{
			_camera = new Camera2D { Name = "Camera2D", Enabled = true };
			AddChild(_camera);
		}
		ApplyCameraDefaults();
		CallDeferred(nameof(EnsureObservedEntity));
	}

	private void ApplyCameraDefaults()
	{
		if (_camera == null) return;
		_camera.Zoom = new Vector2(_zoom, _zoom);
		_camera.LimitLeft = 0;
		_camera.LimitTop = 0;
		_camera.LimitRight = 4800;
		_camera.LimitBottom = 4800;
		_camera.PositionSmoothingEnabled = true;
		_camera.PositionSmoothingSpeed = 8.0f;
	}

	public void Control(Entity? entity)
	{
		if (_controlled == entity) return;
		if (_controlled != null && IsInstanceValid(_controlled))
			_controlled.SetControlled(false);
		_controlled = entity;
		if (_controlled != null && IsInstanceValid(_controlled))
		{
			_controlled.SetControlled(true);
			_observed = _controlled;
		}
		else
		{
			EnsureObservedEntity();
		}
	}

	public void ReleaseControl()
	{
		Control(null);
		EnsureObservedEntity();
	}

	public void CycleObservedEntity()
	{
		List<Entity> entities = new();
		foreach (Node node in GetTree().GetNodesInGroup("entities"))
		{
			if (node is Entity entity && IsInstanceValid(entity))
				entities.Add(entity);
		}
		if (entities.Count == 0)
		{
			_observed = null;
			return;
		}
		entities.Sort((a, b) => string.CompareOrdinal(a.Name.ToString(), b.Name.ToString()));
		Entity? current = ObservedEntity;
		int currentIndex = -1;
		for (int i = 0; i < entities.Count; i++)
		{
			if (entities[i] == current) { currentIndex = i; break; }
		}
		_observed = entities[(currentIndex + 1 + entities.Count) % entities.Count];
	}

	private void EnsureObservedEntity()
	{
		if (_observed != null && IsInstanceValid(_observed))
			return;
		if (_controlled != null && IsInstanceValid(_controlled))
		{
			_observed = _controlled;
			return;
		}
		foreach (Node node in GetTree().GetNodesInGroup("entities"))
		{
			if (node is Entity entity && IsInstanceValid(entity))
			{
				_observed = entity;
				return;
			}
		}
		_observed = null;
	}

	public override void _Process(double delta)
	{
		if (_controlled != null && !IsInstanceValid(_controlled))
			_controlled = null;
		if (_observed != null && !IsInstanceValid(_observed))
			_observed = null;

		EnsureObservedEntity();
		Entity? target = ObservedEntity;
		if (target != null && IsInstanceValid(target) && _camera != null && IsInstanceValid(_camera))
			_camera.GlobalPosition = target.GlobalPosition;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_controlled == null || !IsInstanceValid(_controlled))
			return;

		var inv = GetTree().GetFirstNodeInGroup("inventory_ui") as Control;
		var skills = GetTree().GetFirstNodeInGroup("skills_ui") as Control;
		var cheat = GetTree().GetFirstNodeInGroup("cheat_ui") as Control;
		var chat = GetTree().GetFirstNodeInGroup("villager_chat_ui") as Control;
		bool uiBlocking = (inv != null && inv.Visible) || (skills != null && skills.Visible) || (cheat != null && cheat.Visible) || (chat != null && chat.Visible);
		if (uiBlocking)
		{
			_controlled.SetControllerMoveInput(Vector2.Zero);
			_controlled.AttackHeldByController = false;
			return;
		}

		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (input.LengthSquared() < 0.0001f) input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		if (input.Length() > 1.0f) input = input.Normalized();
		_controlled.SetControllerMoveInput(input);
		_controlled.AttackHeldByController = Input.IsActionPressed("attack");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Tab)
			{
				if (!IsUiBlockingForObservationSwitch())
				{
					CycleObservedEntity();
					GetViewport().SetInputAsHandled();
					return;
				}
			}

			if (keyEvent.Keycode == Key.Ctrl || keyEvent.PhysicalKeycode == Key.Ctrl)
			{
				if (!IsUiBlockingForObservationSwitch())
				{
					ToggleControlObservedEntity();
					GetViewport().SetInputAsHandled();
					return;
				}
			}
		}

		if (_camera == null) return;
		float? zoomDelta = null;
		if (@event is InputEventMouseButton mb && mb.Pressed)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp) zoomDelta = 0.15f;
			else if (mb.ButtonIndex == MouseButton.WheelDown) zoomDelta = -0.15f;
		}
		else if (@event is InputEventKey key && key.Pressed && !key.Echo && !key.CtrlPressed && !key.AltPressed && !key.MetaPressed)
		{
			if (key.Keycode == Key.Equal || key.Keycode == Key.KpAdd) zoomDelta = 0.15f;
			else if (key.Keycode == Key.Minus || key.Keycode == Key.KpSubtract) zoomDelta = -0.15f;
		}
		if (zoomDelta == null) return;
		_zoom = Mathf.Clamp(_zoom + zoomDelta.Value, 0.75f, 4.0f);
		_camera.Zoom = new Vector2(_zoom, _zoom);
		GetViewport().SetInputAsHandled();
	}

	private void ToggleControlObservedEntity()
	{
		EnsureObservedEntity();
		Entity? observed = ObservedEntity;
		if (observed == null || !IsInstanceValid(observed))
			return;

		if (_controlled != null && IsInstanceValid(_controlled) && _controlled == observed)
			ReleaseControl();
		else
			Control(observed);
	}

	private bool IsUiBlockingForObservationSwitch()
	{
		var inv = GetTree().GetFirstNodeInGroup("inventory_ui") as Control;
		var skills = GetTree().GetFirstNodeInGroup("skills_ui") as Control;
		var cheat = GetTree().GetFirstNodeInGroup("cheat_ui") as Control;
		var chat = GetTree().GetFirstNodeInGroup("villager_chat_ui") as Control;
		var pause = GetTree().GetFirstNodeInGroup("game_over_menu") as Control;
		return (inv != null && inv.Visible) || (skills != null && skills.Visible) || (cheat != null && cheat.Visible) || (chat != null && chat.Visible) || (pause != null && pause.Visible);
	}
}
