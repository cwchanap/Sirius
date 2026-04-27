using Godot;

public partial class PauseMenuDialog : AcceptDialog
{
    [Signal] public delegate void ResumeRequestedEventHandler();
    [Signal] public delegate void SaveRequestedEventHandler();
    [Signal] public delegate void LoadRequestedEventHandler();
    [Signal] public delegate void SettingsRequestedEventHandler();
    [Signal] public delegate void QuitToMenuRequestedEventHandler();

    private bool _resumed;

    public override void _Ready()
    {
        Title = "Paused";
        Size = new Vector2I(280, 300);
        Exclusive = true;
        GetOkButton().Visible = false;

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        root.AddChild(MakeBtn("Resume", OnResumePressed));
        root.AddChild(MakeBtn("Save Game", OnSavePressed));
        root.AddChild(MakeBtn("Load Game", OnLoadPressed));
        root.AddChild(MakeBtn("Settings", OnSettingsPressed));
        root.AddChild(MakeBtn("Quit to Main Menu", OnQuitToMenuPressed));

        CloseRequested += OnCloseRequested;
        Canceled += OnCloseRequested;
    }

    private static Button MakeBtn(string text, System.Action handler)
    {
        var btn = new Button { Text = text };
        btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        btn.Pressed += handler;
        return btn;
    }

    private void OnResumePressed()
    {
        Hide();
        EmitSignal(SignalName.ResumeRequested);
    }

    private void OnSavePressed()
    {
        Hide();
        EmitSignal(SignalName.SaveRequested);
    }

    private void OnLoadPressed()
    {
        Hide();
        EmitSignal(SignalName.LoadRequested);
    }

    private void OnSettingsPressed() => EmitSignal(SignalName.SettingsRequested);

    private void OnQuitToMenuPressed()
    {
        Hide();
        EmitSignal(SignalName.QuitToMenuRequested);
    }

    private void OnCloseRequested()
    {
        if (_resumed) return;
        _resumed = true;
        if (Visible) Hide();
        EmitSignal(SignalName.ResumeRequested);
    }

    public override void _ExitTree()
    {
        CloseRequested -= OnCloseRequested;
        Canceled -= OnCloseRequested;
    }
}
