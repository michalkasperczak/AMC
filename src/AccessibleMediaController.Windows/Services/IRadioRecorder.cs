namespace AccessibleMediaController.Windows.Services;

internal interface IRadioRecorder : IDisposable
{
    string FinalPath { get; }
    void Write(byte[] buffer, int offset, int count);
    string Stop();
    void Abort();
}
