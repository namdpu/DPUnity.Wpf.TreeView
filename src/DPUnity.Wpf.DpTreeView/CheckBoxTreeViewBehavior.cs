using DPUnity.Wpf.FilterTreeView;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Threading;

namespace DPUnity.Wpf.DpTreeView
{
    /// <summary>
    /// Provides behavior for hierarchical checkbox interactions in TreeViews
    /// </summary>
    public static class CheckBoxBehavior
    {
        // Cache for reflection delegates to improve performance
        private static readonly Dictionary<Type, Func<object, bool?>> _getterCache = [];
        private static readonly Dictionary<Type, Action<object, bool?>> _setterCache = [];

        // Add these fields to the CheckBoxBehavior class
        private static readonly Dictionary<object, System.Timers.Timer> _updateTimers = [];
        private static readonly object _timerLock = new();

        // Add UI locking mechanism
        private static readonly object _uiLockObj = new();
        private static bool _isUpdating = false;
        private static int _updateCounter = 0;

        // UI thread dispatcher
        private static Dispatcher? _uiDispatcher;

        private static Func<object, bool?> GetIsCheckedGetter<T>() where T : HierarchicalItemBase<T>
        {
            // Existing implementation
            var type = typeof(T);
            if (!_getterCache.TryGetValue(type, out var getter))
            {
                PropertyInfo isCheckedProperty = type.GetProperty("IsChecked", typeof(bool?))
                    ?? throw new InvalidOperationException($"Type {type.Name} must have an IsChecked property of type bool?");
                getter = obj => (bool?)isCheckedProperty.GetValue(obj);
                _getterCache[type] = getter;
            }
            return getter;
        }

        private static Action<object, bool?> GetIsCheckedSetter<T>() where T : HierarchicalItemBase<T>
        {
            // Existing implementation
            var type = typeof(T);
            if (!_setterCache.TryGetValue(type, out var setter))
            {
                PropertyInfo isCheckedProperty = type.GetProperty("IsChecked", typeof(bool?))
                    ?? throw new InvalidOperationException($"Type {type.Name} must have an IsChecked property of type bool?");
                setter = (obj, value) => isCheckedProperty.SetValue(obj, value);
                _setterCache[type] = setter;
            }
            return setter;
        }

        /// <summary>
        /// Sets up the hierarchical checkbox behavior for a node and its descendants
        /// </summary>
        public static void SetupCheckBoxBehavior<T>(T node) where T : HierarchicalItemBase<T>, INotifyPropertyChanged
        {
            // Store UI Dispatcher on first call
            _uiDispatcher ??= Dispatcher.CurrentDispatcher;

            var getter = GetIsCheckedGetter<T>();
            var setter = GetIsCheckedSetter<T>();

            node.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "IsChecked" && sender is T item)
                {
                    if (_isUpdating)
                    {
                        // Ignore changes while updating
                        return;
                    }

                    bool? isChecked = getter(item);

                    // Schedule update with throttling (50ms delay)
                    ScheduleUpdate(item, isChecked, getter, setter);
                }
            };

            foreach (var child in node.Children)
            {
                SetupCheckBoxBehavior(child);
            }
        }

        /// <summary>
        /// Sets up the hierarchical checkbox behavior for a collection of nodes
        /// </summary>
        public static void SetupCheckBoxBehavior<T>(IEnumerable<T> nodes) where T : HierarchicalItemBase<T>, INotifyPropertyChanged
        {
            foreach (var node in nodes)
            {
                SetupCheckBoxBehavior(node);
            }
        }

        // Flag to prevent redundant updates
        private static bool _updatingFromParent = false;

        private static void LockUI()
        {
            lock (_uiLockObj)
            {
                _updateCounter++;
                if (!_isUpdating)
                {
                    _isUpdating = true;
                    _uiDispatcher?.Invoke(() =>
                    {
                        Mouse.OverrideCursor = Cursors.Wait;
                    });
                }
            }
        }

        private static void UnlockUI()
        {
            lock (_uiLockObj)
            {
                _updateCounter--;
                if (_updateCounter == 0 && _isUpdating)
                {
                    _isUpdating = false;
                    _uiDispatcher?.Invoke(() =>
                    {
                        Mouse.OverrideCursor = null;
                    });
                }
            }
        }

        private static void UpdateChildCheckStatesBatch<T>(T parent, bool isChecked, Action<object, bool?> setter) where T : HierarchicalItemBase<T>
        {
            try
            {
                _updatingFromParent = true;

                // Use queue instead of recursion to prevent stack overflow on large trees
                Queue<T> nodesToProcess = new();
                foreach (var child in parent.Children)
                {
                    nodesToProcess.Enqueue(child);
                }

                while (nodesToProcess.Count > 0)
                {
                    T current = nodesToProcess.Dequeue();
                    setter(current, isChecked);

                    foreach (var childOfChild in current.Children)
                    {
                        nodesToProcess.Enqueue(childOfChild);
                    }
                }
            }
            finally
            {
                _updatingFromParent = false;
            }
        }

        private static void UpdateParentCheckStateOptimized<T>(T node, Func<object, bool?> getter, Action<object, bool?> setter) where T : HierarchicalItemBase<T>
        {
            // Existing implementation
            T? currentNode = node.Parent;

            while (currentNode != null)
            {
                if (!currentNode.Children.Any())
                {
                    currentNode = currentNode.Parent;
                    continue;
                }

                bool allChecked = true;
                bool allUnchecked = true;
                bool shouldBreak = false;

                // Use for loop instead of foreach for better performance
                var children = currentNode.Children;
                for (int i = 0; i < children.Count; i++)
                {
                    bool? siblingState = getter(children[i]);

                    if (siblingState != true) allChecked = false;
                    if (siblingState != false) allUnchecked = false;

                    if (!allChecked && !allUnchecked)
                    {
                        shouldBreak = true;
                        break;
                    }
                }

                bool? newState = allChecked ? true : allUnchecked ? false : null;
                bool? currentState = getter(currentNode);

                if (newState != currentState)
                {
                    setter(currentNode, newState);
                }
                else if (!shouldBreak)
                {
                    break;
                }

                currentNode = currentNode.Parent;
            }
        }

        private static void ScheduleUpdate<T>(T item, bool? isChecked, Func<object, bool?> getter, Action<object, bool?> setter)
            where T : HierarchicalItemBase<T>, INotifyPropertyChanged
        {
            LockUI(); // Lock UI before starting the update process

            lock (_timerLock)
            {
                if (_updateTimers.TryGetValue(item as object, out var existingTimer))
                {
                    existingTimer.Stop();
                    existingTimer.Dispose();
                }

                var timer = new System.Timers.Timer(50)
                {
                    AutoReset = false
                };
                timer.Elapsed += async (s, e) =>
                {
                    try
                    {
                        if (isChecked != null)
                        {
                            UpdateChildCheckStatesBatch(item, isChecked.Value, setter);
                        }

                        if (!_updatingFromParent)
                        {
                            UpdateParentCheckStateOptimized(item, getter, setter);
                        }

                        // Add delay to ensure UI refreshes properly
                        await Task.Delay(10);
                    }
                    finally
                    {
                        lock (_timerLock)
                        {
                            _updateTimers.Remove(item as object);
                            timer.Dispose();
                        }

                        UnlockUI(); // Unlock UI after all updates are complete
                    }
                };

                _updateTimers[item as object] = timer;
                timer.Start();
            }
        }
    }
}