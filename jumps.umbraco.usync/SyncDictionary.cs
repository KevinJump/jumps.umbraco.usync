using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;
using System.IO;

using umbraco.BusinessLogic;
using umbraco.cms.businesslogic;

using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core;

using jumps.umbraco.usync.helpers;
using jumps.umbraco.usync.Models;

namespace jumps.umbraco.usync
{
    public class SyncDictionary : SyncItemBase<Dictionary.DictionaryItem>
    {
        public SyncDictionary() :
            base() { }

        public SyncDictionary(ImportSettings settings) :
            base(settings) { }

        public override void ExportAll()
        {
            LogHelper.Debug<SyncDictionary>("Saving Dictionary Types");

            foreach (Dictionary.DictionaryItem item in Dictionary.getTopMostItems)
            {
                LogHelper.Info<SyncDictionary>("Dictionary Item {0}", () => item.key);
                ExportToDisk(item, _settings.Folder);
            }
        }

        public override void ExportToDisk(Dictionary.DictionaryItem item, string folder = null)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (string.IsNullOrEmpty(folder))
                folder = _settings.Folder;

            XElement node = item.SyncExport();

            XmlDoc.SaveNode(folder, item.key, node, Constants.ObjectTypes.Dictionary);
        }
        /*
                XmlDoc.SaveXmlDoc("Dictionary", 
                    _sh.Recode(item.key, Umbraco.Core.Strings.CleanStringType.Ascii),
                    xmlDoc, _savePath);
        */

        public override void ImportAll()
        {
            string root = IOHelper.MapPath(string.Format("{0}\\{1}", _settings.Folder, Constants.ObjectTypes.Dictionary));
            ImportFolder(root);
        }

        public override void Import(string filePath)
        {
            if ( !File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "DictionaryItem")
                throw new ArgumentException("Not a DictionaryItem file", filePath);

            if (_settings.ForceImport || tracker.DictionaryChanged(node))
            {
                if (!_settings.ReportOnly)
                {
                    var backup = Backup(node);

                    ChangeItem change = uDictionaryItem.SyncImport(node);

                    if (uSyncSettings.ItemRestore && change.changeType == ChangeType.Mismatch)
                    {
                        change.changeType = ChangeType.RolledBack;
                        Restore(backup);
                    }
                    uSyncReporter.WriteToLog("Imported Dictionary [{0}] {1}", change.name, change.changeType.ToString());

                    AddChange(change);
                }
                else
                {
                    AddChange(new ChangeItem
                    {
                        changeType = ChangeType.WillChange,
                        itemType = ItemType.Dictionary,
                        name = node.Attribute("Key").Value,
                        message = "Reporting: will update"
                    });
                }
            }
            else
                AddNoChange(ItemType.Dictionary, filePath);
        
        }

        protected override string Backup(XElement node, string filePath = null) 
        {
            if (uSyncSettings.ItemRestore || uSyncSettings.FullRestore || uSyncSettings.BackupOnImport)
            {

                var key = node.Attribute("Key").Value;
                var items = Dictionary.getTopMostItems;

                foreach (var i in items)
                {
                    if (i.key == key)
                    {
                        ExportToDisk(i, _settings.BackupPath);
                        return XmlDoc.GetSavePath(_settings.BackupPath, key, Constants.ObjectTypes.Dictionary);
                    }
                }
            }

            return "";
        }

        protected override void Restore(string backup)
        {
            LogHelper.Info<SyncDictionary>("Restoring Backup: {0}", ()=> backup);

            XElement backupNode = XmlDoc.GetBackupNode(backup);
            if (backupNode != null)
                uDictionaryItem.SyncImport(backupNode, false);
        }

        static string _eventFolder = "";

        public static void AttachEvents(string folder)
        {
            _eventFolder = folder;
            Dictionary.DictionaryItem.Saving += DictionaryItem_Saving;
            Dictionary.DictionaryItem.Deleting += DictionaryItem_Deleting;
        }

        static object _deleteLock = new object();
        static System.Collections.ArrayList _dChildren = new System.Collections.ArrayList(); 


        static void DictionaryItem_Deleting(Dictionary.DictionaryItem sender, EventArgs e)
        {
            if (!uSync.EventPaused)
            {
                lock (_deleteLock)
                {
                    if (sender.hasChildren)
                    {
                        // we get the delets in a backwards order, so we add all the children of this
                        // node to the list we are not going to delete when we get asked to.
                        // 
                        foreach (Dictionary.DictionaryItem child in sender.Children)
                        {
                            _dChildren.Add(child.id);
                        }
                    }

                    if (_dChildren.Contains(sender.id))
                    {
                        // this is a child of a parent we have already deleted.
                        _dChildren.Remove(sender.id);
                        LogHelper.Debug<SyncDictionary>("No Deleteing Dictionary item {0} because we deleted it's parent",
                            () => sender.key);
                    }
                    else
                    {
                        //actually delete 


                        LogHelper.Debug<SyncDictionary>("Deleting Dictionary Item {0}", () => sender.key);

                        // when you delete a tree, the top gets called before the children. 
                        //             
                        if (!sender.IsTopMostItem())
                        {
                            // if it's not top most, we save it's parent (that will delete)
                            var dicSync = new SyncDictionary();
                            dicSync.ExportToDisk(GetTop(sender), _eventFolder);
                        }
                        else
                        {
                            // it's top we need to delete
                            XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, sender.key, Constants.ObjectTypes.Dictionary), true);
                        }
                    }
                }
            }            
        }


        static void DictionaryItem_Saving(Dictionary.DictionaryItem sender, EventArgs e)
        {
            if (!uSync.EventPaused)
            {
                var dicSync = new SyncDictionary();
                dicSync.ExportToDisk(GetTop(sender), _eventFolder);
            }
        }

        private static Dictionary.DictionaryItem GetTop(Dictionary.DictionaryItem item)
        {

            if (!item.IsTopMostItem())
            {
                LogHelper.Debug<SyncDictionary>("is Top Most [{0}]", ()=> item.IsTopMostItem());
                try
                {
                    if (item.Parent != null)
                    {
                        LogHelper.Debug<SyncDictionary>("parent [{0}]", ()=> item.Parent.key);
                        return GetTop(item.Parent);
                    }
                }
                catch (ApplicationException ex)
                {
                    LogHelper.Debug<SyncDictionary>("Exception (just like null) {0}", () => ex.Message);
                }
                catch (ArgumentException ex)
                {
                    LogHelper.Debug<SyncDictionary>("Exception (just like null) {0}", ()=> ex.Message);
                }

            }

            return item; 
        }
    }
}
