using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace jumps.umbraco.usync
{
    /// <summary>
    ///  Base of a uync item, stored where we are actually saving stuff
    ///  and change state. 
    /// </summary>
    public class SyncItemBase: IDisposable
    {
        internal string _savePath;
        internal int _changeCount;
        internal List<ChangeItem> _changes;

        public bool Changes
        {
            get { return _changeCount > 0; }
        }

        public int ChangeCount
        {
            get { return _changeCount; }
        }

        public SyncItemBase(string root)
        {
            _savePath = root;
            _changeCount = 0;
            _changes = new List<ChangeItem>();
        }


        public void Dispose()
        {
            _changes = null;
        }
    }
}
