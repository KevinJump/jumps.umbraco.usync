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
        protected ImportSettings _settings; 

        public SyncItemBase()
        {
            ImportSettings _settings = new ImportSettings();
        }

        public SyncItemBase(ImportSettings settings)
        {
            _settings = settings; 
            _changeCount = 0;
            _changes = new List<ChangeItem>();
        }

        #region ChangeTracking 
        protected ChangeType _changeType; 

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
        public abstract void ExportAll();
        public abstract void ExportToDisk(T item, string folder = null);
        public abstract void Import(string filePath);
        public abstract void ImportAll();

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
