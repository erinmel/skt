using System;
using System.Collections.Generic;

namespace skt.IDE.Services;

public class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    public void Publish<T>(T eventData) where T : class
    {
        if (eventData == null) return;

        var eventType = typeof(T);
        if (_subscribers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers.ToArray())
            {
                try
                {
                    ((Action<T>)handler).Invoke(eventData);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in event handler: {ex.Message}");
                }
            }
        }
    }

    public void Subscribe<T>(Action<T> handler) where T : class
    {
        if (handler == null) return;

        var eventType = typeof(T);
        if (!_subscribers.ContainsKey(eventType))
        {
            _subscribers[eventType] = new List<Delegate>();
        }

        _subscribers[eventType].Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler) where T : class
    {
        if (handler == null) return;

        var eventType = typeof(T);
        if (_subscribers.TryGetValue(eventType, out var handlers))
        {
            handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                _subscribers.Remove(eventType);
            }
        }
    }
}
