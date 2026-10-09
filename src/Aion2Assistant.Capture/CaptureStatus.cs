namespace Aion2Assistant.Capture;

public enum CaptureState { NpcapMissing, GameNotRunning, WaitingForConnection, NoAdapter, Capturing, Error }

public sealed record CaptureStatus(CaptureState State, string Message, string? Adapter = null, string? Server = null);

public enum CaptureIndicator { Grey, Red, Amber, Green }
