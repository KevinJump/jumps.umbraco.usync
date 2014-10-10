using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.IO;

using umbraco.BusinessLogic;
using umbraco.cms.businesslogic;

using Umbraco.Core.IO;
using Umbraco.Core.Logging;

using jumps.umbraco.usync.helpers;

namespace jumps.umbraco.usync
{
    public class SyncDictionary : SyncItemBase
    {
        public SyncDictionary() :
            base(uSyncSettings.Folder) { }

        public SyncDictionary(string folder) :
            base(folder) { }

        public void SaveToDisk(Dictionary.DictionaryItem item)
        {
            if (item != null)
            {
                Umbraco.Core.Strings.DefaultShortStringHelper _sh = new Umbraco.Core.Strings.DefaultShortStringHelper();
                XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                xmlDoc.AppendChild(item.ToXml(xmlDoc));
                xmlDoc.AddDictionaryHash();

                XmlDoc.SaveXmlDoc("Dictionary", 
                    _sh.Recode(item.key, Umbraco.Core.Strings.CleanStringType.Ascii),
                    xmlDoc, _savePath);
            }
        }

        public void SaveAllToDisk()
        {
            LogHelper.Debug<SyncDictionary>("Saving Dictionary Types");

            foreach (Dictionary.DictionaryItem item in Dictionary.getTopMostItems)
            {
                LogHelper.Debug<SyncDictionary>("Dictionary Item {0}", ()=> item.key);
                SaveToDisk(item);

                
            }
        }

        public void ReadAllFromDisk()
        {
            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Dictionary"));

            ReadFromDisk(path);

        }

        public void ReadFromDisk(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);

                    XmlNode node = xmlDoc.SelectSingleNode("./DictionaryItem");

                    if (node != null)
                    {
                        if (tracker.DictionaryChanged(xmlDoc))
                        {
                            _changeCount++;

                            LogHelper.Debug<SyncDictionary>("Node Import: {0} {1}",
                                () => node.Attributes["Key"].Value, () => node.InnerXml);

                            try
                            {

                                Dictionary.DictionaryItem item = Dictionary.DictionaryItem.Import(node);

                                if (item != null)
                                    item.Save();
                            }
                            catch (Exception ex)
                            {
                                LogHelper.Debug<SyncDictionary>("DictionaryItem.Import Failed {0}: {1}",
                                    () => path, () => ex.ToString());
                            }
                        }
                    }
                }
            }
            
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
