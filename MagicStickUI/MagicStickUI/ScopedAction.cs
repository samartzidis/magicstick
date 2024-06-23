using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicStickUI
{
    public class ScopedAction : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public ScopedAction(Action onEnter, Action onDispose)
        {
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            _disposed = false;
            onEnter?.Invoke();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _onDispose();
                _disposed = true;
            }
        }
    }
}
