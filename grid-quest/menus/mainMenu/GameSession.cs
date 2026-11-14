using Godot;

namespace GridQuest.Menus.MainMenu;

public partial class GameSession : Node
{
	public static GameSession Instance { get; private set; }

	public SaveInfo CurrentSave { get; private set; }
	public string PendingNewSaveTitle { get; set; }
	public string PendingNewSaveDescription { get; set; }
	public string LastCreatedSaveId { get; set; }
	public bool ShowControlsOnNextLevelLoad { get; set; }

	public bool ConsumeShowControlsOnNextLevelLoad()
	{
		if (!ShowControlsOnNextLevelLoad)
			return false;
		ShowControlsOnNextLevelLoad = false;
		return true;
	}

	public override void _Ready()
	{
		Instance = this;
	}

	public void SetCurrentSave(SaveInfo save)
	{
		CurrentSave = save;
	}

	public void Clear()
	{
		CurrentSave = null;
	}
}
