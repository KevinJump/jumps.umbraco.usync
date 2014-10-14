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
using jumps.umbraco.usync.Models;
using System.Xml.Linq;

namespace jumps.umbraco.usync
{
    public class SyncLanguage : SyncItemBase<Language>
    {
        public SyncLanguage() :
            base(uSyncSettings.Folder) { }

        public SyncLanguage(string folder) :
            base(folder) { }

        public SyncLanguage(string folder, string set) :
            base(folder, set) { }

        public override void ExportAll(string folder)
        {
            foreach(Language item in Language.GetAllAsList())
            {
                ExportToDisk(item);
            }
        }

        public override void ExportToDisk(Language item, string folder = null)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (string.IsNullOrEmpty(folder))
                folder = _savePath;

            XElement node = item.SyncExport();
            XmlDoc.SaveNode(folder, item.CultureAlias, node, Constants.ObjectTypes.Language);
        }

        public override void ImportAll(string folder)
        {
            string root = IOHelper.MapPath(string.Format("{0}\\{1}", folder, Constants.ObjectTypes.Language));
            ImportFolder(root);
        }

        public override void Import(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "Language")
                throw new ArgumentException("Not a Language file", filePath);

            if (tracker.LanguageChanged(node))
            {
                var backup = Backup(node);

                ChangeItem change = uLanguage.SyncImport(node);

                if (change.changeType == ChangeType.Mismatch)
                    Restore(backup);

                AddChange(change);
            }
            else
                AddNoChange(ItemType.Languages, filePath);
        }

        protected override string Backup(XElement node)
        {
            var culture = node.Attribute("CultureAlias").Value;
            var lang = Language.GetByCultureCode(culture);

            if (lang != null)
            {
                ExportToDisk(lang, _backupPath);
                return XmlDoc.GetSavePath(_backupPath, lang.CultureAlias, Constants.ObjectTypes.Language);
            }

            return "";
        }

        protected override void Restore(string backup)
        {
            XElement backupNode = XmlDoc.GetBackupNode(backup);

            if (backupNode != null)
                uLanguage.SyncImport(backupNode, false);
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
            var langSync = new SyncLanguage();
            langSync.ExportToDisk(sender, _eventFolder); 
        }

        static void Language_AfterDelete(Language sender, global::umbraco.cms.businesslogic.DeleteEventArgs e)
        {
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), sender.CultureAlias);

        }

        static void Language_AfterSave(Language sender, global::umbraco.cms.businesslogic.SaveEventArgs e)
        {
            var langSync = new SyncLanguage();
            langSync.ExportToDisk(sender, _eventFolder);
        }
    }
}
