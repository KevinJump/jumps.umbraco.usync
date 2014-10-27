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
            base() { }

        public SyncLanguage(ImportSettings settings) :
            base(settings) { }

        public override void ExportAll()
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
                folder = _settings.Folder;

            XElement node = item.SyncExport();
            XmlDoc.SaveNode(folder, item.CultureAlias, node, Constants.ObjectTypes.Language);
        }

        public override void ImportAll()
        {
            foreach(var rename in uSyncNameManager.GetRenames(Constants.ObjectTypes.Language))
            {
                AddChange(uLanguage.Rename(rename.Key, rename.Value, _settings.ReportOnly));
            }

            foreach(var delete in uSyncNameManager.GetDeletes(Constants.ObjectTypes.Language))
            {
                AddChange(uLanguage.Delete(delete.Value, _settings.ReportOnly));
            }

            string root = IOHelper.MapPath(string.Format("{0}\\{1}", _settings.Folder, Constants.ObjectTypes.Language));
            ImportFolder(root);
            // RemoveFromSource(root);
        }

        public override void Import(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "Language")
                throw new ArgumentException("Not a Language file", filePath);

            if (_settings.ForceImport || tracker.LanguageChanged(node))
            {
                if (!_settings.ReportOnly)
                {

                    var backup = Backup(node);

                    ChangeItem change = uLanguage.SyncImport(node);

                    if (uSyncSettings.ItemRestore && change.changeType == ChangeType.Mismatch)
                    {
                        Restore(backup);
                        change.changeType = ChangeType.RolledBack;
                    }
                    uSyncReporter.WriteToLog("Imported Language [{0}] {1}", change.name, change.changeType.ToString());

                    AddChange(change);
                }
                else
                {
                    AddChange(new ChangeItem
                    {
                        changeType = ChangeType.WillChange,
                        itemType = ItemType.Languages,
                        name = node.Attribute("CultureAlias").Value,
                        message = "Reporting: will update"
                    });
                }
            }
            else
                AddNoChange(ItemType.Languages, filePath);
        }

        private void RemoveFromSource(string filepath)
        {
            // will remove any languages from the suste, that are not on the disk.
            foreach (Language item in Language.GetAllAsList())
            {
                var file = XmlDoc.GetSavePath(_settings.Folder, item.CultureAlias, Constants.ObjectTypes.Language);
                if ( !System.IO.File.Exists(file))
                {
                    // delete from the db.
                    if (_settings.ReportOnly)
                    {
                        AddChange(new ChangeItem
                        {
                            changeType = ChangeType.WillChange,
                            name = item.CultureAlias,
                            message = "Will delete " + file,
                            itemType = ItemType.Languages
                        });
                    }
                    else
                    {
                        item.Delete();
                        AddChange(new ChangeItem
                        {
                            changeType = ChangeType.Delete,
                            name = item.CultureAlias,
                            message = "deleted " + file,
                            itemType = ItemType.Languages
                        });
                    }
                }
            }
        }

        protected override string Backup(XElement node)
        {
            if (uSyncSettings.ItemRestore || uSyncSettings.FullRestore)
            {
                var culture = node.Attribute("CultureAlias").Value;
                var lang = Language.GetByCultureCode(culture);

                if (lang != null)
                {
                    ExportToDisk(lang, _settings.BackupPath);
                    return XmlDoc.GetSavePath(_settings.BackupPath, lang.CultureAlias, Constants.ObjectTypes.Language);
                }
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
            if (!uSync.EventPaused)
            {
                uSyncNameManager.SaveDelete(Constants.ObjectTypes.Language, sender.CultureAlias);
                uSyncNameCache.Languages.Remove(sender.id);

                XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, sender.CultureAlias, Constants.ObjectTypes.Language), true);
            }
        }

        static void Language_AfterSave(Language sender, global::umbraco.cms.businesslogic.SaveEventArgs e)
        {
            if (!uSync.EventPaused)
            {
                if ( uSyncNameCache.IsRenamed(sender))
                {
                    uSyncNameManager.SaveRename(Constants.ObjectTypes.Language, uSyncNameCache.Languages[sender.id], sender.CultureAlias);
                    XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, uSyncNameCache.Languages[sender.id], Constants.ObjectTypes.Language), true);
                }

                uSyncNameCache.UpdateCache(sender);

                var langSync = new SyncLanguage();
                langSync.ExportToDisk(sender, _eventFolder);
            }
        }

        static void InitNameCache()
        {
            if ( uSyncNameCache.Languages == null)
            {
                uSyncNameCache.Languages = new Dictionary<int, string>();
                foreach (Language item in Language.GetAllAsList())
                {
                    uSyncNameCache.Languages.Add(item.id, item.CultureAlias);
                }
            }
        }
    }
}
