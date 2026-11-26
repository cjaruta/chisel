namespace Chisel.Core.Focus;

// The focus timer's options and its state, saved together.
public sealed record FocusTimerData(FocusSettings Settings, FocusTimerState State)
{
    public static FocusTimerData Default { get; } = new(FocusSettings.Default, FocusTimerState.Initial);
}

public interface IFocusTimerRepository
{
    FocusTimerData Get();
    void Save(FocusTimerData data);
}
