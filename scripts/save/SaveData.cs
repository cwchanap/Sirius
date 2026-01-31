using System;

/// <summary>
/// Root DTO for save file structure. JSON-serializable.
/// </summary>
public class SaveData
{
    public int Version { get; set; } = 1;
    public CharacterSaveData Character { get; set; }
    public int CurrentFloorIndex { get; set; }
    public Vector2IDto PlayerPosition { get; set; }
    public DateTime SaveTimestamp { get; set; }
}

/// <summary>
/// Helper DTO for Vector2I since System.Text.Json can't serialize Godot types directly.
/// </summary>
public class Vector2IDto
{
    public int X { get; set; }
    public int Y { get; set; }

    public Vector2IDto() { }

    public Vector2IDto(Godot.Vector2I v)
    {
        X = v.X;
        Y = v.Y;
    }

    public Godot.Vector2I ToVector2I() => new Godot.Vector2I(X, Y);
}
