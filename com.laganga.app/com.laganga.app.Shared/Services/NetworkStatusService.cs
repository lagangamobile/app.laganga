using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Networking;
using System;
using System.Threading;

namespace com.laganga.app.Shared.Services;

public class NetworkStatusService
{
    private readonly Timer _timer;
    private readonly object _lock = new();
    private string? _lastErrorMessage;

    public event Action? StatusChanged;

    public bool IsOnline { get; private set; } = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    public DateTimeOffset? LastApiFailureUtc { get; private set; }
    public string? LastApiErrorMessage
    {
        get => _lastErrorMessage;
        private set
        {
            _lastErrorMessage = value;
            if (string.IsNullOrWhiteSpace(_lastErrorMessage))
                LastApiFailureUtc = null;
        }
    }

    public NetworkStatusService()
    {
        Connectivity.ConnectivityChanged += OnConnectivityChanged;

        // Opcional: verificación periódica (cada 15s) para forzar refresco de estado
        _timer = new Timer(_ =>
        {
            var current = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            if (current != IsOnline)
            {
                IsOnline = current;
                RaiseChanged();
            }
        }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        IsOnline = e.NetworkAccess == NetworkAccess.Internet;
        if (IsOnline)
        {
            // Al recuperar conexión limpiamos error previo
            LastApiErrorMessage = null;
        }
        RaiseChanged();
    }

    public void ReportApiFailure(string message)
    {
        lock (_lock)
        {
            LastApiFailureUtc = DateTimeOffset.UtcNow;
            LastApiErrorMessage = message;
        }
        RaiseChanged();
    }

    public void ClearApiFailure()
    {
        lock (_lock)
        {
            LastApiErrorMessage = null;
        }
        RaiseChanged();
    }

    private void RaiseChanged() => MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke());
}
