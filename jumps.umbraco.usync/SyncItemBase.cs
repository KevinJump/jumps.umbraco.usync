using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace jumps.umbraco.usync
{
    /// <summary>
    ///  Base of a uync item, stored where we are actually saving stuff
    ///  and change state. 
    /// </summary>
    public abstract class SyncItemBase<T>: IDisposable
    {
        public SyncItemBase(string root)
        {
            _savePath = root;
            _changeCount = 0;
            _changes = new List<ChangeItem>();
        }

        public SyncItemBase(string root, string set)
        {
            _savePath = root;
            _changeCount = 0;
            if (!string.IsNullOrEmpty(set))
            {
                _backupPath = string.Format("~\\{0}\\{1}", uSyncSettings.BackupFolder.Trim('\\'), set);
            }
            _changes = new List<ChangeItem>();
        }

        #region ChangeTracking 
        protected ChangeType _changeType; 
        protected string _savePath;
        protected string _backupPath;
        private int _changeCount;
        private List<ChangeItem> _changes;
        public bool ChangesMade
        {
            get { return _changeCount > 0; }
        }
        public int ChangeCount
        {
            get { return _changeCount; }
        }
        public List<ChangeItem> ChangeList
        {
            get { return _changes; }
        }
        protected void AddChange(ChangeItem item)
        {
            if ( item.changeType == null )
                item.changeType = _changeType;

            if (item.changeType != ChangeType.NoChange)
                _changeCount++;
        
            _changes.Add(item);
        }
        protected void AddNoChange(ItemType type, string filename)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(filename);

            if (type == ItemType.DocumentType || type == ItemType.MediaItem || type == ItemType.Template)
                name = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(filename));
                

            _changes.Add(new ChangeItem
            {
                file = filename,
                name = name,
                changeType = ChangeType.NoChange,
                itemType = type
            });
        }
        #endregion 
        
        #region Export and Import
        public abstract void ExportAll(string folder);
        public abstract void ExportToDisk(T item, string folder = null);
        public abstract void Import(string filePath);
        public abstract void ImportAll(string folder);

        protected void ImportFolder(string folder)
        {
            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, Constants.SyncFileMask))
                {
                    Import(file);
                }

                foreach (string subFolder in Directory.GetDirectories(folder))
                {
                    ImportFolder(subFolder);
                }
            }
        }

        protected abstract string Backup(XElement node);
        protected abstract void Restore(string backup);
        #endregion 

        public void Dispose()
        {
            _changes = null;
        }
    }
}
