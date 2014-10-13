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
            base(uSyncSettings.Folder) { }

        public SyncDictionary(string folder) :
            base(folder) { }

        public SyncDictionary(string folder, string set) :
            base(folder, set) { }

        public override void ExportAll(string folder)
        {
            LogHelper.Debug<SyncDictionary>("Saving Dictionary Types");

            foreach (Dictionary.DictionaryItem item in Dictionary.getTopMostItems)
            {
                LogHelper.Debug<SyncDictionary>("Dictionary Item {0}", () => item.key);
                ExportToDisk(item, folder);
            }
        }

        public override void ExportToDisk(Dictionary.DictionaryItem item, string folder = null)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (string.IsNullOrEmpty(folder))
                folder = _savePath;

            XElement node = ((uDictionaryItem)item).SyncExport();

            XmlDoc.SaveNode(folder, item.key, node, Constants.ObjectTypes.Dictionary);
        }
        /*
                XmlDoc.SaveXmlDoc("Dictionary", 
                    _sh.Recode(item.key, Umbraco.Core.Strings.CleanStringType.Ascii),
                    xmlDoc, _savePath);
        */

        public override void ImportAll(string folder)
        {
            string root = IOHelper.MapPath(string.Format("{0}{1}", folder, Constants.ObjectTypes.Dictionary));
            ImportFolder(root);
        }

        private void ImportFolder(string folder)
        {
            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, Constants.SyncFileMask))
                {
                    Import(file);
                }
            }
        }

        public override void Import(string filePath)
        {
            if ( !File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "Dictionary")
                throw new ArgumentException("Not a dictionart file", filePath);

            if (tracker.DictionaryChanged(node))
            {
                Backup(node);

                ChangeItem change = uDictionaryItem.SyncImport(node);

                if (change.changeType == ChangeType.Mismatch)
                    Restore(node);

                AddChange(change);
            }
            else
                AddNoChange(ItemType.Dictionary, filePath);
        
        }

        private void Backup(XElement node) 
        { 
            
        }

        private void Restore(XElement node) {

            var key = node.Attribute("Key").Value;
            XElement backupNode = XmlDoc.GetBackupNode(_backupPath, key, Constants.ObjectTypes.Dictionary);

            if (backupNode != null)
                uDictionaryItem.SyncImport(backupNode, false);

        }



        private void PreChangeBackup(XmlNode xDoc)
        {
           XElement node = XElement.Load(new XmlNodeReader(xDoc));
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
            lock (_deleteLock)
            {
                if (sender.hasChildren)
                {
                    // we get the delets in a backwards order, so we add all the children of this
                    // node to the list we are not going to delete when we get asked to.
                    // 
                    foreach(Dictionary.DictionaryItem child in sender.Children)
                    {
                        _dChildren.Add(child.id) ; 
                    }
                }

                if (_dChildren.Contains(sender.id))
                {
                    // this is a child of a parent we have already deleted.
                    _dChildren.Remove(sender.id);
                    LogHelper.Debug<SyncDictionary>("No Deleteing Dictionary item {0} because we deleted it's parent", 
                        ()=> sender.key); 
                }
                else
                {
                    //actually delete 


                    LogHelper.Debug<SyncDictionary>("Deleting Dictionary Item {0}",  ()=> sender.key);

                    // when you delete a tree, the top gets called before the children. 
                    //             
                    if (!sender.IsTopMostItem())
                    {
                        // if it's not top most, we save it's parent (that will delete)
                        var dicSync = new SyncDictionary(_eventFolder);
                        dicSync.SaveToDisk(GetTop(sender));
                    }
                    else
                    {
                        // it's top we need to delete
                        helpers.XmlDoc.ArchiveFile("Dictionary", sender.key);

                    }
                }
            }
            
            
            
        }


        static void DictionaryItem_Saving(Dictionary.DictionaryItem sender, EventArgs e)
        {
            var dicSync = new SyncDictionary(_eventFolder);
            dicSync.SaveToDisk(GetTop(sender));
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
                    LogHelper.Debug<SyncDictionary>("Exception (just like null)");
                }
                catch (ArgumentException ex)
                {
                    LogHelper.Debug<SyncDictionary>("Exception (just like null)");
                }

            }

            return item; 
        }
    }
}
