using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Aion2Assistant.App.Localization;
using Aion2Assistant.App.Services;
using Aion2Assistant.Capture;
using Aion2Assistant.Core.Events;
using Aion2Assistant.Core.Localization;
using Aion2Assistant.Protocol;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Assistant.App.ViewModels;

public partial class CaptureViewModel(Action toggleRecording, Action openFolder) : ObservableObject
{
    [ObservableProperty] private Brush _indicatorBrush = Brushes.Gray;
    [ObservableProperty] private string _statusText = Strings.CaptureStatusStarting;
    [ObservableProperty] private string _adapter = "—";
    [ObservableProperty] private string _server = "—";
    [ObservableProperty] private string _selfText = UiStrings.CaptureSelfUnknown;
    [ObservableProperty] private string _countersText = "";
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _recordButtonText = UiStrings.CaptureRecordStart;

    public ObservableCollection<string> TopOpcodes { get; } = new();

    [RelayCommand]
    private void ToggleRecording() => toggleRecording();

    [RelayCommand]
    private void OpenCapturesFolder() => openFolder();

    private CaptureIndicator _indicator;
    private bool _themeHooked;

    public void SetIndicator(CaptureIndicator indicator, string reason)
    {
        if (!_themeHooked)
        {
            // Les brosses de thème sont remplacées à chaque changement : on relit celle de l'indicateur courant.
            // Abonnement jamais retiré (vie = celle de l'app).
            _themeHooked = true;
            ThemeManager.ThemeChanged += () => IndicatorBrush = BrushFor(_indicator);
        }
        _indicator = indicator;
        IndicatorBrush = BrushFor(indicator);
        StatusText = reason;
    }

    private static Brush BrushFor(CaptureIndicator indicator) => indicator switch
    {
        CaptureIndicator.Green => Resource("HealBrush"),
        CaptureIndicator.Amber => Resource("AlertBrush"),
        CaptureIndicator.Red => Resource("DpsBrush"),
        _ => Resource("TextMutedBrush"),
    };

    public void SetStatus(CaptureStatus status)
    {
        Adapter = status.Adapter ?? "—";
        Server = status.Server ?? "—";
    }

    public void SetSelf(SelfIdentifiedEvent self)
    {
        var name = self.Name.Length == 0 ? UiStrings.CaptureNoName : self.Name;
        var details = self.ServerId != 0
            ? UiStrings.Format(nameof(UiStrings.CaptureSelfDetails), self.ServerId, self.Class, self.Level)
            : "";
        _selfBase = name + details;
        ComposeSelfText();
    }

    private string _selfBase = UiStrings.CaptureSelfUnknown;
    private long? _selfCombatPower;

    /// <summary>Puissance de combat de soi (null = inconnue) ajoutée au texte du personnage.</summary>
    public void SetSelfCombatPower(long? value)
    {
        _selfCombatPower = value;
        ComposeSelfText();
    }

    private void ComposeSelfText() =>
        SelfText = _selfCombatPower is { } power
            ? _selfBase + UiStrings.Format(nameof(UiStrings.CaptureSelfPower), power.ToString("N0", AppCulture.Current))
            : _selfBase;

    public void SetStats(PipelineStats stats, long dropped)
    {
        var total = stats.Parsed + stats.Rejected;
        var failure = total == 0 ? 0 : 100.0 * stats.Rejected / total;
        CountersText = UiStrings.Format(nameof(UiStrings.CaptureCounters),
            stats.Segments, stats.Frames, stats.Events, stats.Resyncs, stats.Gaps, failure, dropped);
        var top = stats.Opcodes.OrderByDescending(o => o.Received).Take(6)
            .Select(o => UiStrings.Format(nameof(UiStrings.CaptureOpcodeLine), Opcodes.Name(o.Opcode), o.Received)
                + (o.Rejected > 0 ? UiStrings.Format(nameof(UiStrings.CaptureOpcodeRejected), o.Rejected) : ""))
            .ToList();
        if (!top.SequenceEqual(TopOpcodes))
        {
            TopOpcodes.Clear();
            foreach (var line in top) TopOpcodes.Add(line);
        }
    }

    public void SetRecording(bool recording)
    {
        IsRecording = recording;
        RecordButtonText = recording ? UiStrings.CaptureRecordStop : UiStrings.CaptureRecordStart;
    }

    private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];
}
