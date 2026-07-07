using Godot;

public static class Floor3Layout
{
    public const int Width = 24;
    public const int Height = 18;

    // +1 x vs Python FLOOR3_PLAYER_START (10,10): the C# PlayerStartOnStair
    // validator (FloorValidationService) rejects spawning on a stair tile, so
    // the spawn is shifted one cell right of DownStair. Save-load is safe: the
    // .tres PlayerStartPosition matches this and the cell is off-stair.
    public static readonly Vector2I PlayerStart = new(11, 10);
    public static readonly Vector2I DownStair = new(10, 10);
}
