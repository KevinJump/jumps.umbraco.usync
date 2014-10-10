using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using umbraco.cms.businesslogic.language;

using System.Xml;
using System.IO;

using Umbraco.Core.IO;
using Umbraco.Core.Logging;

using jumps.umbraco.usync.helpers;

namespace jumps.umbraco.usync
{
    public class SyncLanguage : SyncItemBase
    {
        public SyncLanguage() :
            base(uSyncSettings.Folder) { }

        public SyncLanguage(string folder) :
            base(folder) { }

        public void SaveToDisk(Language item)
        {
            if (item != null)
            {
                XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc(); 
                xmlDoc.AppendChild(item.ToXml(xmlDoc));

                helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.CultureAlias, xmlDoc, _savePath) ; 
            }
        }

        public void SaveAllToDisk()
        {
            LogHelper.Debug<SyncLanguage>(">>>> Language save all to disk");
            foreach (Language item in Language.GetAllAsList())
            {
                LogHelper.Debug<SyncLanguage>(">>>> {0} <<<<<", ()=> item.CultureAlias);
                SaveToDisk(item);
            }
        }

        public void ReadAllFromDisk()
        {
            string path = IOHelper.MapPath(String.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Language"));

            ReadFromDisk(path);
        }

        public void ReadFromDisk(string path)
        {
            LogHelper.Debug<SyncLanguage>("Reading from disk {0}", ()=> path); 
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    LogHelper.Debug<SyncLanguage>("Reading file {0} from disk", ()=> file); 

                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);

                    LogHelper.Debug<SyncLanguage>("XML Loaded"); 

                    XmlNode node = xmlDoc.SelectSingleNode("//Language");

                    LogHelper.Debug<SyncLanguage>("Node Found"); 

                    if (node != null)
                    {
                        if (tracker.LanguageChanged(xmlDoc))
                        {
                            LogHelper.Debug<SyncLanguage>("About to Load Language {0}", () => node.OuterXml);
                            Language l = Language.Import(node);

                            if (l != null)
                            {
                                l.Save();
                            }
                        }
                    }
                    LogHelper.Debug<SyncLanguage>("Language done"); 
                }
            }
        }

        static string _eventFolder = "";

        public static void AttachEvents(string folder)
        {
            _eventFolder = folder;
            Language.New += Language_New;
            Language.AfterSave += Language_AfterSave;
            Language.AfterDelete += Language_AfterDelete;
        }

        static void Language_New(Language sender, global::umbraco.cms.businesslogic.NewEventArgs e)
        {
            var langSync = new SyncLanguage(_eventFolder);
            langSync.SaveToDisk(sender); 
        }

        static void Language_AfterDelete(Language sender, global::umbraco.cms.businesslogic.DeleteEventArgs e)
        {
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), sender.CultureAlias);

        }

        static void Language_AfterSave(Language sender, global::umbraco.cms.businesslogic.SaveEventArgs e)
        {
            var langSync = new SyncLanguage(_eventFolder);
            langSync.SaveToDisk(sender);
        }
    }
}
