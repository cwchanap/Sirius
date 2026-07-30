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

	public static bool Apply(TextureButton target, UiIconId id, UiIconSize size)
	{
		var texture = UiArtCatalog.LoadIcon(id, size);
		ApplyGlyphTexture(target, texture);
		return texture != null;
	}

	internal static void ApplyTexture(TextureButton target, Texture2D? texture) =>
		SetSlotTextures(target, texture, TextureButton.StretchModeEnum.KeepAspectCentered);

	internal static void ApplyGlyphTexture(TextureButton target, Texture2D? texture) =>
		SetSlotTextures(target, texture, TextureButton.StretchModeEnum.KeepCentered);

	private static void SetSlotTextures(TextureButton target, Texture2D? texture,
		TextureButton.StretchModeEnum stretchMode)
	{
		target.TextureNormal = texture;
		target.TextureHover = texture;
		target.TexturePressed = texture;
		target.TextureDisabled = texture;
		target.TextureFocused = texture;
		target.StretchMode = stretchMode;
		target.IgnoreTextureSize = true;
	}

	public static bool Apply(Button target, UiIconId id, UiIconSize size)
	{
		target.Icon = UiArtCatalog.LoadIcon(id, size);
		target.ExpandIcon = false;
		target.AddThemeConstantOverride("icon_max_width", (int)size);
		return target.Icon != null;
	}
}
