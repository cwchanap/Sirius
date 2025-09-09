using Godot;

public partial class MainMenu : Control
{
	private TextureRect _backgroundRect;

	public override void _Ready()
	{
		// Initialize the main menu
		GD.Print("Main Menu loaded");
		
		// Load and set the background image
		LoadBackgroundImage();
	}

	private void LoadBackgroundImage()
	{
		// Get reference to the background TextureRect
		_backgroundRect = GetNode<TextureRect>("Background");
		
		if (_backgroundRect != null)
		{
			// Try to load the main menu background
			var backgroundTexture = GD.Load<Texture2D>("res://assets/sprites/ui/ui_main_menu_background.png");
			
			if (backgroundTexture != null)
			{
				_backgroundRect.Texture = backgroundTexture;
				_backgroundRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
				_backgroundRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
				GD.Print("✅ Main menu background loaded successfully");
			}
			else
			{
				GD.Print("⚠️ Main menu background not found, using default gradient");
			}
		}
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
