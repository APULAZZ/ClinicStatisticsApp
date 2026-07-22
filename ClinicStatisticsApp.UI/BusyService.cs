namespace ClinicStatisticsApp.UI;

/// <summary>Единый индикатор длительных операций MANGO.</summary>
public sealed class BusyService
{
    public event EventHandler<BusyChangedEventArgs>? Changed;

    public IDisposable Begin(string message)
    {
        Changed?.Invoke(this, new BusyChangedEventArgs(true, message));
        return new Scope(this);
    }

    public void Report(string message) => Changed?.Invoke(this, new BusyChangedEventArgs(true, message));

    private void End() => Changed?.Invoke(this, new BusyChangedEventArgs(false, string.Empty));

    private sealed class Scope(BusyService service) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            service.End();
        }
    }
}

public sealed record BusyChangedEventArgs(bool IsBusy, string Message);
