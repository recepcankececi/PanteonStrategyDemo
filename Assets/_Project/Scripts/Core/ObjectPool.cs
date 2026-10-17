using System;
using System.Collections.Generic;

namespace StrategyDemo.Core
{
    /// <summary>
    /// Keeps released objects for reuse so repeatedly spawned things (menu items, units, previews)
    /// are created once instead of instantiated and destroyed every time (U1).
    /// </summary>
    /// <remarks>
    /// The pool knows nothing about Unity: the owner decides what "create", "get" and "release" mean
    /// (e.g. instantiate under a parent, activate, reset state and clear subscribers).
    /// Pooled Unity objects must not be destroyed by their owner while the pool holds them.
    /// </remarks>
    /// <typeparam name="T">Pooled object type.</typeparam>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Stack<T> _inactive = new Stack<T>();
        // Objects handed out and not yet returned; lets Release reject foreign or already returned objects in O(1).
        private readonly HashSet<T> _active = new HashSet<T>();

        /// <param name="create">Makes a new object when the pool is empty. Must not return null.</param>
        /// <param name="onGet">Runs on every object handed out (new or reused); optional.</param>
        /// <param name="onRelease">Runs on every returned object; reset its state here. Optional.</param>
        /// <exception cref="ArgumentNullException"><paramref name="create"/> is null.</exception>
        public ObjectPool(Func<T> create, Action<T> onGet = null, Action<T> onRelease = null)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onGet = onGet;
            _onRelease = onRelease;
        }

        /// <summary>Objects created by this pool so far, in use or not.</summary>
        public int CountAll { get; private set; }

        /// <summary>Objects waiting in the pool.</summary>
        public int CountInactive => _inactive.Count;

        /// <summary>Objects currently handed out.</summary>
        public int CountActive => _active.Count;

        /// <summary>Returns a pooled object, creating one only when none is waiting.</summary>
        /// <exception cref="InvalidOperationException">The create function returned null.</exception>
        public T Get()
        {
            T item = _inactive.Count > 0 ? _inactive.Pop() : CreateItem();
            _active.Add(item);
            _onGet?.Invoke(item);
            return item;
        }

        /// <summary>Returns <paramref name="item"/> to the pool for later reuse.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="item"/> is not currently handed out by this pool (already released, or never taken from it).</exception>
        public void Release(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            // A double release would later hand the same object to two owners; a foreign object would
            // corrupt the counts. Both are bugs in the caller, so fail loudly here, not much later.
            if (!_active.Remove(item))
            {
                throw new InvalidOperationException("The object is not handed out by this pool (already released or foreign).");
            }

            _onRelease?.Invoke(item);
            _inactive.Push(item);
        }

        private T CreateItem()
        {
            T item = _create();
            if (item == null)
            {
                throw new InvalidOperationException("The pool's create function returned null.");
            }

            CountAll++;
            return item;
        }
    }
}
