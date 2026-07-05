using Godot;

namespace Sirius.FloorTools.Addon;

[Tool]
public partial class SiriusFloorToolsDock : Control
{
    private OptionButton _floorOption;
    private RichTextLabel _resultsLabel;

    public override void _Ready()
    {
        _floorOption = GetNodeOrNull<OptionButton>("%FloorOption");
        _resultsLabel = GetNodeOrNull<RichTextLabel>("%ResultsLabel");
        Log("Sirius Floor Tools ready. Buttons wire up in a later task.");
    }

    public void Log(string message)
    {
        if (_resultsLabel != null)
            _resultsLabel.AddText(message + "\n");
    }
}
