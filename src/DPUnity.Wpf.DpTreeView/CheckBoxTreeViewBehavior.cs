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
        private static readonly Dictionary<Type, Func<object, bool?>> _getterCache = new();
        private static readonly Dictionary<Type, Action<object, bool?>> _setterCache = new();
        // Add these fields to the CheckBoxBehavior class
        private static readonly Dictionary<object, System.Timers.Timer> _updateTimers = new();
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
            // Track dynamic children changes to keep check-state propagation consistent when items are added/removed at runtime
            node.Children.CollectionChanged += (s, e) =>
            {
                // Handle added children: hook behavior and align parent/ancestors state
                if (e.NewItems != null)
                {
                    foreach (var obj in e.NewItems)
                    {
                        if (obj is T child)
                        {
                            // Ensure the new child has behavior wired up
                            SetupCheckBoxBehavior(child);

                            // If parent has a definite state, optionally propagate it to the new subtree
                            var parentState = getter(node);
                            if (parentState.HasValue)
                            {
                                // Avoid re-entrancy while bulk-updating new subtree
                                LockUI();
                                try
                                {
                                    UpdateChildCheckStatesBatch(child, parentState.Value, setter);
                                }
                                finally
                                {
                                    UnlockUI();
                                }
                            }

                            // Recalculate tri-state for the parent and its ancestors now that a child exists/changed
                            LockUI();
                            try
                            {
                                UpdateNodeStateFromChildren(node, getter, setter);
                                // Propagate upwards starting from this parent
                                UpdateParentCheckStateOptimized(node, getter, setter);
                            }
                            finally
                            {
                                UnlockUI();
                            }
                        }
                    }
                }

                // Handle removed children: recompute parent's tri-state and bubble up
                if (e.OldItems != null)
                {
                    // After removal, child.Parent may be nulled already; we know 'node' is the parent
                    LockUI();
                    try
                    {
                        UpdateNodeStateFromChildren(node, getter, setter);
                        UpdateParentCheckStateOptimized(node, getter, setter);
                    }
                    finally
                    {
                        UnlockUI();
                    }
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
                // Use Stack<T> for DFS traversal to minimize memory usage (max stack size ~ tree depth, better than BFS queue size ~ tree width/subtree size)
                Stack<T> nodesToProcess = new Stack<T>();
                // Push all immediate children (order doesn't matter for setting states, but reverse to simulate typical recursion order if needed)
                for (int i = parent.Children.Count - 1; i >= 0; i--)
                {
                    nodesToProcess.Push(parent.Children[i]);
                }
                while (nodesToProcess.Count > 0)
                {
                    T current = nodesToProcess.Pop();
                    setter(current, isChecked);
                    // Push children in reverse order
                    for (int i = current.Children.Count - 1; i >= 0; i--)
                    {
                        nodesToProcess.Push(current.Children[i]);
                    }
                }
                // No need to clear stack; it's local and will be GC'ed after method exit
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
                if (_updateTimers.TryGetValue(item, out var existingTimer))
                {
                    existingTimer.Stop();
                    existingTimer.Dispose();
                }
                var timer = new System.Timers.Timer(50)
                {
                    AutoReset = false
                };
                timer.Elapsed += (s, e) =>
                {
                    // Dispatch the update to the UI thread to avoid cross-thread issues with property changes and bindings
                    _uiDispatcher?.Invoke(() =>
                    {
                        try
                        {
                            if (isChecked.HasValue)
                            {
                                UpdateChildCheckStatesBatch(item, isChecked.Value, setter);
                            }
                            if (!_updatingFromParent)
                            {
                                UpdateParentCheckStateOptimized(item, getter, setter);
                            }
                        }
                        finally
                        {
                            lock (_timerLock)
                            {
                                _updateTimers.Remove(item);
                                timer.Dispose();
                            }
                            UnlockUI(); // Unlock UI after all updates are complete
                        }
                    });
                };
                _updateTimers[item] = timer;
                timer.Start();
            }
        }

        // Helper: recompute a node's IsChecked from its children's states (all true => true, all false => false, mixed => null)
        private static void UpdateNodeStateFromChildren<T>(T node, Func<object, bool?> getter, Action<object, bool?> setter) where T : HierarchicalItemBase<T>
        {
            if (node.Children == null || node.Children.Count == 0)
            {
                return;
            }

            bool allChecked = true;
            bool allUnchecked = true;
            var children = node.Children;
            for (int i = 0; i < children.Count; i++)
            {
                bool? state = getter(children[i]);
                if (state != true) allChecked = false;
                if (state != false) allUnchecked = false;
                if (!allChecked && !allUnchecked)
                {
                    break;
                }
            }

            bool? newState = allChecked ? true : allUnchecked ? false : (bool?)null;
            bool? currentState = getter(node);
            if (newState != currentState)
            {
                setter(node, newState);
            }
        }
    }
}