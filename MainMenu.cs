using Godot;

public partial class MainMenu : Control
{
	public override void _Ready()
	{
		// Initialize the main menu
		GD.Print("Main Menu loaded");
	}

	private void _on_start_button_pressed()
	{
		GD.Print("Start Game button pressed");
		// Load the game scene
		GetTree().ChangeSceneToFile("res://scenes/game/Game.tscn");
	}

	private void _on_settings_button_pressed()
	{
		GD.Print("Settings button pressed");
		// TODO: Load settings scene or show settings panel
		ShowMessage("Settings menu coming soon!");
	}

	private void _on_quit_button_pressed()
	{
		GD.Print("Quit button pressed");
		GetTree().Quit();
	}

	private void ShowMessage(string message)
	{
		// Create a simple popup to show messages
		var popup = new AcceptDialog();
		popup.DialogText = message;
		AddChild(popup);
		popup.PopupCentered();
		
		// Auto-close the popup after 2 seconds for non-quit messages
		if (message != "Quitting game...")
		{
			GetTree().CreateTimer(2.0).Timeout += () => {
				if (IsInstanceValid(popup))
				{
					popup.QueueFree();
				}
			};
		}
	}
}
