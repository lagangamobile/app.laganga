using System.Collections.Generic;

namespace com.laganga.app.Shared.Services;

public class ParameterService
{
    private readonly Dictionary<string, Dictionary<string, object>> _contexts = new();
    private readonly object _lock = new();

    public void SetParameter(string context, string key, object value)
    {
        if (string.IsNullOrWhiteSpace(context))
            throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        lock (_lock)
        {
            if (!_contexts.ContainsKey(context))
            {
                _contexts[context] = new Dictionary<string, object>();
            }
            _contexts[context][key] = value;
        }
    }

    public T GetParameter<T>(string context, string key)
    {
        if (string.IsNullOrWhiteSpace(context) || string.IsNullOrWhiteSpace(key))
            return default!;

        lock (_lock)
        {
            if (_contexts.TryGetValue(context, out var parameters) &&
                parameters.TryGetValue(key, out var value) &&
                value is T typedValue)
            {
                return typedValue;
            }
        }

        return default!;
    }

    public bool TryGetParameter<T>(string context, string key, out T value)
    {
        lock (_lock)
        {
            if (_contexts.TryGetValue(context, out var parameters) &&
                parameters.TryGetValue(key, out var rawValue) &&
                rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }
        }

        value = default!;
        return false;
    }

    public IReadOnlyDictionary<string, object> GetContextParameters(string context)
    {
        lock (_lock)
        {
            if (_contexts.ContainsKey(context))
                return new Dictionary<string, object>(_contexts[context]);
            return new Dictionary<string, object>();
        }
    }

    public void ClearParameters(string context)
    {
        lock (_lock)
        {
            _contexts.Remove(context);
        }
    }
}
