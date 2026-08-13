using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusUiMetricsTest : Node
{
    [TestCase]
    public void SharedSafeFrameInsetsMatchApprovedSizes()
    {
        var compact = SiriusUiMetrics.SafeFrameInsets(new Vector2(640, 360));
        AssertThat(compact.Compact).IsTrue();
        AssertThat(compact.Margin).IsEqual(12f);
        AssertThat(compact.SideInset).IsEqual(12f);

        var standard = SiriusUiMetrics.SafeFrameInsets(new Vector2(1280, 720));
        AssertThat(standard.Compact).IsFalse();
        AssertThat(standard.Margin).IsEqual(24f);
        AssertThat(standard.SideInset).IsEqual(24f);

        var ultrawide = SiriusUiMetrics.SafeFrameInsets(new Vector2(2560, 1080));
        AssertThat(ultrawide.Compact).IsFalse();
        AssertThat(ultrawide.Margin).IsEqual(24f);
        AssertThat(ultrawide.SideInset).IsEqual(480f);
    }

    [TestCase]
    public void ItemSlotSize_UsesApprovedGeometry()
    {
        AssertThat(SiriusUiMetrics.ItemSlotSize(false)).IsEqual(new Vector2(56, 56));
        AssertThat(SiriusUiMetrics.ItemSlotSize(true)).IsEqual(new Vector2(48, 48));
    }
}
