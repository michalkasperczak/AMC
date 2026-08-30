namespace AccessibleMediaController.Windows.Services;

internal interface IRadioRecorder : IDisposable
{
    string FinalPath { get; }
    bool CanPause { get; }
    bool IsPaused { get; }
    TimeSpan RecordedDuration { get; }
    void Write(byte[] buffer, int offset, int count);
    void Pause();
    void Resume();
    string Stop();
    void Abort();
}
