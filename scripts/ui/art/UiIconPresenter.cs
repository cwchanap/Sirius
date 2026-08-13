using Godot;

public static class UiIconPresenter
{
	public static bool Apply(TextureRect target, UiIconId id, UiIconSize size)
	{
		var texture = UiArtCatalog.LoadIcon(id, size);
		target.Texture = texture;
		target.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		target.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		return texture != null;
	}

	public static bool ApplyGlyph(TextureRect target, UiIconId id, UiIconSize size)
	{
		var texture = UiArtCatalog.LoadIcon(id, size);
		target.Texture = texture;
		target.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		target.StretchMode = TextureRect.StretchModeEnum.KeepCentered;
		return texture != null;
	}

	public static void ApplyItem(TextureRect target, Texture2D? texture)
	{
		target.Texture = texture;
		target.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		target.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
	}

	public static bool Apply(Button target, UiIconId id, UiIconSize size)
	{
		target.Icon = UiArtCatalog.LoadIcon(id, size);
		target.ExpandIcon = false;
		target.AddThemeConstantOverride("icon_max_width", (int)size);
		return target.Icon != null;
	}
}
