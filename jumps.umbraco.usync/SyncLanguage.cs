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
using System.Diagnostics;

namespace jumps.umbraco.usync
{
    public class SyncLanguage
    {
        public static void SaveToDisk(Language item)
        {
            if (item != null)
            {
                XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc(); 
                xmlDoc.AppendChild(item.ToXml(xmlDoc));
                xmlDoc.AddMD5Hash();
                helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.CultureAlias, xmlDoc) ; 
            }
        }

        public static void SaveAllToDisk()
        {
            LogHelper.Debug<SyncLanguage>(">>>> Language save all to disk");
            foreach (Language item in Language.GetAllAsList())
            {
                LogHelper.Debug<SyncLanguage>(">>>> {0} <<<<<", ()=> item.CultureAlias);
                SaveToDisk(item);
            }
        }

        public static void ReadAllFromDisk()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            string path = IOHelper.MapPath(String.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "Language"));

            ReadFromDisk(path);

            sw.Stop();
            LogHelper.Info<uSync>("Processed Languages ({0}ms)", () => sw.ElapsedMilliseconds);
        }

        public static void ReadFromDisk(string path)
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
                        LogHelper.Debug<SyncLanguage>("About to Load Language {0}", ()=> node.OuterXml); 
                        Language l = Language.Import(node);

                        if (l != null)
                        {
                            l.Save();
                        }
                    }
                    LogHelper.Debug<SyncLanguage>("Language done"); 
                }
            }
        }

        public static void AttachEvents()
        {
            Language.New += Language_New;
            Language.AfterSave += Language_AfterSave;
            Language.AfterDelete += Language_AfterDelete;
        }

        static void Language_New(Language sender, global::umbraco.cms.businesslogic.NewEventArgs e)
        {
            if (!uSync.EventsPaused)
            {
                SaveToDisk(sender);
            }
        }

        static void Language_AfterDelete(Language sender, global::umbraco.cms.businesslogic.DeleteEventArgs e)
        {
            if (!uSync.EventsPaused)
            {
                helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), sender.CultureAlias);
            }
        }

        static void Language_AfterSave(Language sender, global::umbraco.cms.businesslogic.SaveEventArgs e)
        {
            if (!uSync.EventsPaused)
            {
                SaveToDisk(sender);
            }
        }
    }
}
