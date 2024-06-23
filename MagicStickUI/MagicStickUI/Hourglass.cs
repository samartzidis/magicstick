using System;
using System.Windows.Input;

namespace MagicStickUI
{
    public class Hourglass : IDisposable
    {
        private readonly Cursor _previousCursor;

        public Hourglass()
        {
            _previousCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
        }

        public void Dispose()
        {
            Mouse.OverrideCursor = _previousCursor;
        }
    }
}
