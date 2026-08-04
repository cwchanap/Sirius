using Godot;
using System.Collections.Generic;

public partial class SiriusUiShowcase : Control
{
    private const float MaximumContentWidth = 1600f;
    private static readonly Vector2 EntryTranslation = new(0, 12);
    private static readonly Vector2 ExitTranslation = new(0, -8);
    private static readonly StringName KeyboardAction = "hpa377_showcase_keyboard";
    private static readonly StringName MouseAction = "hpa377_showcase_mouse";
    private static readonly StringName GamepadAction = "hpa377_showcase_gamepad";
    private static readonly StringName UnboundAction = "hpa377_showcase_unbound";

    private readonly List<Button> _previewButtons = new();
    private readonly List<StringName> _createdInputActions = new();

    private OptionButton _viewportSizeSelector = null!;
    private CheckBox _reducedMotionToggle = null!;
    private MarginContainer _safeFrame = null!;
    private VBoxContainer _showcaseContent = null!;
    private Button _motionPlayButton = null!;
    private Button _focusFirst = null!;
    private Button _focusLast = null!;
    private Button _selectedFocused = null!;
    private TabContainer _nativeTabs = null!;
    private Label _displayLabel = null!;
    private Label _titleLabel = null!;
    private Label _sectionLabel = null!;
    private Label _bodyLabel = null!;
    private Label _metadataLabel = null!;
    private Label _stressBody = null!;
    private Button _stressAction = null!;
    private SiriusModalShell _mediumModal = null!;
    private ScrollContainer _mediumBodyScroll = null!;
    private Control _motionModalWrapper = null!;
    private Control _motionToastWrapper = null!;
    private Vector2 _motionModalBasePosition;
    private Vector2 _motionToastBasePosition;
    private Tween? _motionTween;

    private SiriusStatBar[] _statBars = null!;
    private SiriusInputHint[] _inputHints = null!;
    private SiriusContextPrompt[] _contextPrompts = null!;
    private SiriusToastShell[] _toasts = null!;
    private SiriusModalShell[] _modals = null!;

    public SubViewport PreviewViewport { get; private set; } = null!;
    public Control PreviewRoot { get; private set; } = null!;
    public bool Compact { get; private set; }
    public bool ReducedMotion { get; private set; }

    public override void _Ready()
    {
        _viewportSizeSelector = GetNode<OptionButton>("%ViewportSizeSelector");
        _reducedMotionToggle = GetNode<CheckBox>("%ReducedMotionToggle");
        PreviewViewport = GetNode<SubViewport>("%PreviewViewport");
        PreviewRoot = GetNode<Control>("%PreviewRoot");
        _safeFrame = GetNode<MarginContainer>("%SafeFrame");
        _showcaseContent = GetNode<VBoxContainer>("%ShowcaseContent");
        _motionPlayButton = GetNode<Button>("%MotionPlayButton");
        _focusFirst = GetNode<Button>("%FocusFirstFixture");
        _selectedFocused = GetNode<Button>("%SelectedFocusedFixture");
        _focusLast = GetNode<Button>("%FocusLastFixture");
        _nativeTabs = GetNode<TabContainer>("%NativeTabs");
        _displayLabel = GetNode<Label>("%TypographyDisplay");
        _titleLabel = GetNode<Label>("%TypographyTitle");
        _sectionLabel = GetNode<Label>("%TypographySectionLabel");
        _bodyLabel = GetNode<Label>("%TypographyBody");
        _metadataLabel = GetNode<Label>("%StressMetadata");
        _mediumModal = GetNode<SiriusModalShell>("%MediumModalFixture");
        _mediumBodyScroll = _mediumModal.GetNode<ScrollContainer>("%BodyScroll");
        _motionModalWrapper = GetNode<Control>("%MotionModalWrapper");
        _motionToastWrapper = GetNode<Control>("%MotionToastWrapper");
        _motionModalBasePosition = _motionModalWrapper.Position;
        _motionToastBasePosition = _motionToastWrapper.Position;

        _statBars =
        [
            GetNode<SiriusStatBar>("%HealthStat"),
            GetNode<SiriusStatBar>("%ManaStat"),
            GetNode<SiriusStatBar>("%ExperienceStat"),
            GetNode<SiriusStatBar>("%InvalidStat")
        ];
        _inputHints =
        [
            GetNode<SiriusInputHint>("%KeyboardHint"),
            GetNode<SiriusInputHint>("%MouseHint"),
            GetNode<SiriusInputHint>("%GamepadHint"),
            GetNode<SiriusInputHint>("%FallbackHint"),
            GetNode<SiriusInputHint>("%UnboundHint")
        ];
        _contextPrompts = [GetNode<SiriusContextPrompt>("%TalkPrompt")];
        _toasts =
        [
            GetNode<SiriusToastShell>("%InfoToast"),
            GetNode<SiriusToastShell>("%SuccessToast"),
            GetNode<SiriusToastShell>("%WarningToast"),
            GetNode<SiriusToastShell>("%ErrorToast"),
            GetNode<SiriusToastShell>("%MotionToast")
        ];
        _modals =
        [
            GetNode<SiriusModalShell>("%SmallModalFixture"),
            _mediumModal,
            GetNode<SiriusModalShell>("%LargeModalFixture"),
            GetNode<SiriusModalShell>("%MotionModal")
        ];

        ConfigureComponentFixtures();
        CreateStressModalFixtures();
        ConfigureHintFixtures();
        CollectButtons(PreviewRoot, _previewButtons);
        ConfigureFocusLoop();
        PopulateViewportSelector();

        _viewportSizeSelector.ItemSelected += OnViewportSizeSelected;
        _reducedMotionToggle.Toggled += SetReducedMotion;
        _motionPlayButton.Pressed += PlayMotionDemo;

        SetReducedMotion(false);
        SetPreviewSize(new Vector2I(1280, 720));
    }

    public override void _ExitTree()
    {
        if (_motionTween is not null && _motionTween.IsValid())
            _motionTween.Kill();

        foreach (var action in _createdInputActions)
            InputMap.EraseAction(action);

        if (IsNodeReady())
        {
            _viewportSizeSelector.ItemSelected -= OnViewportSizeSelected;
            _reducedMotionToggle.Toggled -= SetReducedMotion;
            _motionPlayButton.Pressed -= PlayMotionDemo;
        }
    }

    public void SetPreviewSize(Vector2I size)
    {
        var previewSize = new Vector2I(Mathf.Max(1, size.X), Mathf.Max(1, size.Y));
        PreviewViewport.Size = previewSize;
        Compact = SiriusUiMetrics.IsCompact(previewSize);
        ApplyCompactState();
    }

    public void SetReducedMotion(bool reducedMotion)
    {
        ReducedMotion = reducedMotion;
        if (IsNodeReady())
            _reducedMotionToggle.SetPressedNoSignal(reducedMotion);
    }

    public void PlayMotionDemo()
    {
        if (_motionTween is not null && _motionTween.IsValid())
            _motionTween.Kill();

        ResetMotionWrapper(_motionModalWrapper, _motionModalBasePosition, ReducedMotion);
        ResetMotionWrapper(_motionToastWrapper, _motionToastBasePosition, ReducedMotion);

        _motionTween = CreateTween();
        _motionTween.SetParallel();
        AnimateEntry(_motionModalWrapper, _motionModalBasePosition);
        AnimateEntry(_motionToastWrapper, _motionToastBasePosition);
        _motionTween.Chain().SetParallel();
        AnimateExit(_motionModalWrapper, _motionModalBasePosition);
        AnimateExit(_motionToastWrapper, _motionToastBasePosition);
    }

    private void ApplyCompactState()
    {
        var safeMargin = SiriusUiMetrics.SafeMargin(Compact);
        var contentWidth = Mathf.Min(
            MaximumContentWidth,
            PreviewViewport.Size.X - safeMargin * 2f);
        var target = SiriusUiMetrics.MinimumTarget(Compact);

        _safeFrame.AddThemeConstantOverride("margin_left", safeMargin);
        _safeFrame.AddThemeConstantOverride("margin_top", safeMargin);
        _safeFrame.AddThemeConstantOverride("margin_right", safeMargin);
        _safeFrame.AddThemeConstantOverride("margin_bottom", safeMargin);
        _showcaseContent.CustomMinimumSize = new Vector2(Mathf.Max(0, contentWidth), 0);
        _mediumBodyScroll.CustomMinimumSize = new Vector2(0, Compact ? 88 : 112);

        _displayLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.DisplayCompact
            : SiriusThemeTypes.Display;
        _titleLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.TitleCompact
            : SiriusThemeTypes.Title;
        _sectionLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.SectionCompact
            : SiriusThemeTypes.Section;
        _bodyLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.BodyCompact
            : SiriusThemeTypes.Body;
        _stressBody.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.BodyCompact
            : SiriusThemeTypes.Body;
        _metadataLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.MetadataCompact
            : SiriusThemeTypes.Metadata;
        _metadataLabel.CustomMinimumSize = new Vector2(
            SiriusUiMetrics.TooltipMaximum(Compact),
            0);

        foreach (var statBar in _statBars)
            statBar.Compact = Compact;
        foreach (var inputHint in _inputHints)
            inputHint.Compact = Compact;
        foreach (var contextPrompt in _contextPrompts)
            contextPrompt.Compact = Compact;
        foreach (var toast in _toasts)
            toast.Compact = Compact;
        foreach (var modal in _modals)
        {
            modal.Compact = Compact;
            modal.RefreshPresentation(PreviewViewport.Size);
        }

        foreach (var button in _previewButtons)
        {
            button.CustomMinimumSize = button.ThemeTypeVariation == SiriusThemeTypes.IgnitionButton
                ? SiriusUiMetrics.IgnitionSize(Compact)
                : target;
        }
    }

    private void CreateStressModalFixtures()
    {
        _stressBody = new Label
        {
            Name = "StressBody",
            Text = "The observatory records every celestial route before committing the next action. This representative paragraph is intentionally long enough to wrap across multiple lines at the minimum supported viewport while preserving readable body text, fixed modal actions, and vertical scrolling.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _mediumModal.BodyHost.AddChild(_stressBody);
        _stressBody.Owner = this;
        _stressBody.UniqueNameInOwner = true;

        _stressAction = new Button
        {
            Name = "StressAction",
            Text = "Bestätigungsaktion mit ausführlicher Beschreibung",
            ThemeTypeVariation = SiriusThemeTypes.PrimaryButton,
            FocusMode = FocusModeEnum.None,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _mediumModal.ActionsHost.AddChild(_stressAction);
        _stressAction.Owner = this;
        _stressAction.UniqueNameInOwner = true;
    }

    private void ConfigureComponentFixtures()
    {
        ConfigureStat(_statBars[0], SiriusStatBarKind.Health, 20, 100, "Health");
        ConfigureStat(_statBars[1], SiriusStatBarKind.Mana, 120, 100, "Mana");
        ConfigureStat(_statBars[2], SiriusStatBarKind.Experience, -5, 100, "Experience");
        ConfigureStat(_statBars[3], SiriusStatBarKind.Health, 10, 0, "Invalid maximum");

        ConfigureToast(_toasts[0], SiriusUiSeverity.Info, "Observatory", "Route record opened.");
        ConfigureToast(_toasts[1], SiriusUiSeverity.Success, "Calibration", "Celestial route confirmed.");
        ConfigureToast(_toasts[2], SiriusUiSeverity.Warning, "Signal drift", "Adjust the observation array.");
        ConfigureToast(_toasts[3], SiriusUiSeverity.Error, "Transmission lost", "No constellation link is available.");
        ConfigureToast(_toasts[4], SiriusUiSeverity.Success, "Motion demo", "Local wrapper transition.");

        ConfigureModal(_modals[0], "Small modal", SiriusUiSeverity.Info, SiriusModalSizeClass.Small);
        ConfigureModal(_mediumModal, "Medium modal", SiriusUiSeverity.Warning, SiriusModalSizeClass.Medium);
        ConfigureModal(_modals[2], "Large modal", SiriusUiSeverity.Error, SiriusModalSizeClass.Large);
        ConfigureModal(_modals[3], "Motion modal", SiriusUiSeverity.Success, SiriusModalSizeClass.Small);
    }

    private static void ConfigureStat(
        SiriusStatBar statBar,
        SiriusStatBarKind kind,
        double current,
        double maximum,
        string label)
    {
        statBar.Kind = kind;
        statBar.Current = current;
        statBar.Maximum = maximum;
        statBar.Label = label;
        statBar.RefreshPresentation();
    }

    private static void ConfigureToast(
        SiriusToastShell toast,
        SiriusUiSeverity severity,
        string title,
        string message)
    {
        toast.Severity = severity;
        toast.Title = title;
        toast.Message = message;
        toast.RefreshPresentation();
    }

    private static void ConfigureModal(
        SiriusModalShell modal,
        string title,
        SiriusUiSeverity severity,
        SiriusModalSizeClass sizeClass)
    {
        modal.Title = title;
        modal.Severity = severity;
        modal.SizeClass = sizeClass;
    }

    private void ConfigureHintFixtures()
    {
        AddShowcaseInputAction(KeyboardAction, new InputEventKey
        {
            PhysicalKeycode = Key.E
        });
        AddShowcaseInputAction(MouseAction, new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left
        });
        AddShowcaseInputAction(GamepadAction, new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.A
        });
        AddShowcaseInputAction(UnboundAction, null);

        ConfigureHint(_inputHints[0], "Keyboard", KeyboardAction,
            new InputEventKey { PhysicalKeycode = Key.E, Pressed = true });
        ConfigureHint(_inputHints[1], "Mouse", MouseAction,
            new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        ConfigureHint(_inputHints[2], "Gamepad", GamepadAction,
            new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true });
        ConfigureHint(_inputHints[3], "Fallback", KeyboardAction,
            new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true });
        ConfigureHint(_inputHints[4], "Unbound", UnboundAction,
            new InputEventKey { PhysicalKeycode = Key.E, Pressed = true });

        var contextPrompt = _contextPrompts[0];
        contextPrompt.ShowIcon = true;
        contextPrompt.IconId = UiIconId.Dialogue;
        contextPrompt.Prompt = "Talk";
        contextPrompt.Actions = [KeyboardAction];
        contextPrompt.Refresh();
    }

    private void AddShowcaseInputAction(StringName action, InputEvent? inputEvent)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action, 0.5f);
            _createdInputActions.Add(action);
        }

        if (inputEvent is not null)
            InputMap.ActionAddEvent(action, inputEvent);
    }

    private static void ConfigureHint(
        SiriusInputHint hint,
        string prompt,
        StringName action,
        InputEvent observedEvent)
    {
        hint.Prompt = prompt;
        hint.Actions = [action];
        hint.Observe(observedEvent);
        hint.Refresh();
    }

    private void ConfigureFocusLoop()
    {
        DisableFocusWithin(PreviewRoot);
        _nativeTabs.GetTabBar().FocusMode = FocusModeEnum.None;

        _focusFirst.FocusMode = FocusModeEnum.All;
        _selectedFocused.FocusMode = FocusModeEnum.All;
        _focusLast.FocusMode = FocusModeEnum.All;
        _focusFirst.FocusNext = _focusFirst.GetPathTo(_selectedFocused);
        _focusFirst.FocusPrevious = _focusFirst.GetPathTo(_focusLast);
        _selectedFocused.FocusNext = _selectedFocused.GetPathTo(_focusLast);
        _selectedFocused.FocusPrevious = _selectedFocused.GetPathTo(_focusFirst);
        _focusLast.FocusNext = _focusLast.GetPathTo(_focusFirst);
        _focusLast.FocusPrevious = _focusLast.GetPathTo(_selectedFocused);
    }

    private void PopulateViewportSelector()
    {
        _viewportSizeSelector.Clear();
        foreach (var size in SiriusUiMetrics.VerificationViewports)
            _viewportSizeSelector.AddItem($"{size.X} × {size.Y}");

        _viewportSizeSelector.Select(2);
    }

    private void OnViewportSizeSelected(long index)
    {
        if (index >= 0 && index < SiriusUiMetrics.VerificationViewports.Length)
            SetPreviewSize(SiriusUiMetrics.VerificationViewports[index]);
    }

    private void ResetMotionWrapper(Control wrapper, Vector2 basePosition, bool reducedMotion)
    {
        wrapper.Position = reducedMotion ? basePosition : basePosition + EntryTranslation;
        wrapper.Modulate = new Color(1, 1, 1, 0);
    }

    private void AnimateEntry(Control wrapper, Vector2 basePosition)
    {
        _motionTween!.TweenProperty(
                wrapper,
                "modulate:a",
                1f,
                SiriusMotion.Duration(ReducedMotion, true))
            .SetTrans(SiriusMotion.EntryTransition)
            .SetEase(SiriusMotion.EntryEase);
        if (SiriusMotion.UseTransform(ReducedMotion))
        {
            _motionTween.TweenProperty(wrapper, "position", basePosition, SiriusMotion.EntrySeconds)
                .SetTrans(SiriusMotion.EntryTransition)
                .SetEase(SiriusMotion.EntryEase);
        }
    }

    private void AnimateExit(Control wrapper, Vector2 basePosition)
    {
        _motionTween!.TweenProperty(
                wrapper,
                "modulate:a",
                0f,
                SiriusMotion.Duration(ReducedMotion, false))
            .SetTrans(SiriusMotion.ExitTransition)
            .SetEase(SiriusMotion.ExitEase);
        if (SiriusMotion.UseTransform(ReducedMotion))
        {
            _motionTween.TweenProperty(
                    wrapper,
                    "position",
                    basePosition + ExitTranslation,
                    SiriusMotion.ExitSeconds)
                .SetTrans(SiriusMotion.ExitTransition)
                .SetEase(SiriusMotion.ExitEase);
        }
    }

    private static void CollectButtons(Node node, List<Button> buttons)
    {
        if (node is Button button)
            buttons.Add(button);

        foreach (Node child in node.GetChildren())
            CollectButtons(child, buttons);
    }

    private static void DisableFocusWithin(Node node)
    {
        if (node is Control control)
            control.FocusMode = FocusModeEnum.None;

        foreach (Node child in node.GetChildren())
            DisableFocusWithin(child);
    }
}
