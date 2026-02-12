using Godot;

public partial class MainMenu : Control
{
	private TextureRect _backgroundRect;
	private AudioStreamPlayer _backgroundMusic;
	private SaveLoadDialog _loadDialog;

	public override void _Ready()
	{
		// Initialize the main menu
		GD.Print("Main Menu loaded");
		
		// Load and set the background image
		LoadBackgroundImage();
		SetupBackgroundMusic();
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

	private void SetupBackgroundMusic()
	{
		_backgroundMusic = GetNodeOrNull<AudioStreamPlayer>("BackgroundMusic");
		if (_backgroundMusic == null)
		{
			GD.PrintErr("MainMenu: BackgroundMusic node not found");
			return;
		}

		var stream = _backgroundMusic.Stream;
		switch (stream)
		{
			case AudioStreamMP3 mp3Stream:
				mp3Stream.Loop = true;
				break;
			case AudioStreamOggVorbis oggStream:
				oggStream.Loop = true;
				break;
			case AudioStreamWav wavStream:
				wavStream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
				break;
		}

		if (!_backgroundMusic.Playing)
		{
			_backgroundMusic.Play();
		}
	}

	private void _on_start_button_pressed()
	{
		GD.Print("Start Game button pressed");
		// Ensure no pending load data (start fresh)
		if (SaveManager.Instance != null)
		{
			SaveManager.Instance.PendingLoadData = null;
		}
		// Load the game scene
		GetTree().ChangeSceneToFile("res://scenes/game/Game.tscn");
	}

	private void _on_load_button_pressed()
	{
		GD.Print("Load Game button pressed");

		// Check if any saves exist
		bool anySaveExists = false;
		for (int i = 0; i <= 3; i++)
		{
			if (SaveManager.Instance?.SaveExists(i) == true)
			{
				anySaveExists = true;
				break;
			}
		}

		if (!anySaveExists)
		{
			ShowMessage("No save files found!");
			return;
		}

		// Show load dialog
		if (_loadDialog != null)
		{
			CleanupLoadDialog();
		}

		_loadDialog = new SaveLoadDialog();
		_loadDialog.LoadSlotSelected += OnLoadSlotSelected;
		_loadDialog.DialogClosed += OnLoadDialogClosed;
		AddChild(_loadDialog);
		_loadDialog.ShowDialog(SaveLoadDialog.DialogMode.Load);
	}

	private void OnLoadSlotSelected(int slot)
	{
		GD.Print($"Loading from slot {slot}");

		var saveData = slot == 3
			? SaveManager.Instance?.LoadAutosave()
			: SaveManager.Instance?.LoadGame(slot);

		var mgr = SaveManager.Instance;
		if (saveData != null && mgr != null)
		{
			mgr.PendingLoadData = saveData;
			CleanupLoadDialog();
			GetTree().ChangeSceneToFile("res://scenes/game/Game.tscn");
		}
		else
		{
			ShowMessage("Failed to load save file!");
			CleanupLoadDialog();
		}
	}

	private void OnLoadDialogClosed()
	{
		CleanupLoadDialog();
	}

	private void CleanupLoadDialog()
	{
		if (_loadDialog != null)
		{
			_loadDialog.LoadSlotSelected -= OnLoadSlotSelected;
			_loadDialog.DialogClosed -= OnLoadDialogClosed;
			_loadDialog.QueueFree();
			_loadDialog = null;
		}
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
