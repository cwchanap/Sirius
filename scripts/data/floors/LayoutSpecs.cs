using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;

public interface IHasPosition { Vector2I Position { get; } }

public record TreasureSpec(Vector2I Position, int Gold, Dictionary<string, int> Items) : IHasPosition;
public record TrapSpec(Vector2I Position, int Damage, string StatusEffect, int Magnitude, int Turns) : IHasPosition;
public record SwitchSpec(Vector2I Position, string Prompt, string Activated) : IHasPosition;
public record GateSpec(Vector2I Position, bool StartsClosed) : IHasPosition;
public record RiddleSpec(Vector2I Position, string Prompt, List<PuzzleRiddleChoiceData> Choices, string CorrectChoiceId, int WrongDamage) : IHasPosition;
