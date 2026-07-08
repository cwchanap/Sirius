using Godot;

// Lives in the data layer (global namespace) so Floor1/2Layout.cs do not depend
// on the Sirius.FloorTools logic namespace. Mirrors the FloorPaths precedent.
// The SupplementalEnemyPlanner (logic) and FloorGenerationService consume this;
// both can see global-namespace types without a using.
public record EnemySpec(Vector2I Position, string EnemyType);
