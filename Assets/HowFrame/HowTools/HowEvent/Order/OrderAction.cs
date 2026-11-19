using System;
using System.Collections.Generic;
namespace HowFrame
{

public class OrderAction
{
    private readonly SortedDictionary<int, Action> _actions = new();
    private readonly object _lock = new();
    // [ZombieGame] 快照缓存：只有增删时才重建，Invoke 不再每次 new List（原来每 tick 都分配，空跑也产生 GC）
    private Action[] _snapshot = Array.Empty<Action>();
    private bool _dirty;

    public int Count { get { lock (_lock) return _actions.Count; } }

    public static OrderAction operator +(OrderAction order, (Action action, int index) entry)
    {
        lock (order._lock)
        {
            if (order._actions.ContainsKey(entry.index))
            {
                Console.WriteLine($"[OrderAction] Index {entry.index} �Ѵ��ڣ����Ǿɵ� Action");
                order._actions[entry.index] = entry.action;
            }
            else
            {
                order._actions.Add(entry.index, entry.action);
            }
            order._dirty = true;
        }
        return order;
    }

    public static OrderAction operator -(OrderAction order, int index)
    {
        lock (order._lock)
        {
            order._actions.Remove(index);
            order._dirty = true;
        }
        return order;
    }

    public void Invoke()
    {
        Action[] toInvoke;
        lock (_lock)
        {
            if (_dirty)
            {
                _snapshot = new Action[_actions.Count];
                _actions.Values.CopyTo(_snapshot, 0);
                _dirty = false;
            }
            toInvoke = _snapshot;
        }

        for (var i = 0; i < toInvoke.Length; i++)
            toInvoke[i]?.Invoke();
    }
    
    public void Clear()
    {
        lock (_lock)
        {
            _actions.Clear();
            _dirty = true;
        }
    }
}
}

