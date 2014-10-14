using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml ;
using System.IO; 

using umbraco.cms.businesslogic; 
using umbraco.cms.businesslogic.macro ;
using umbraco.cms.businesslogic.packager ; 
using umbraco.BusinessLogic;

using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core;

using jumps.umbraco.usync.helpers;
using jumps.umbraco.usync.Models;
using System.Xml.Linq;

namespace jumps.umbraco.usync
{
    /// <summary>
    /// Sycronizes all the macros to/from the usync folder
    /// 
    /// the macros definitions are stored compleatly in the 
    /// database although they often point to files on the 
    /// disk (scrips, user controls). 
    /// 
    /// SyncMacro uses the package API to read write the xml
    /// files for macros. no structure in macros.
    /// 
    /// </summary>
    /// 
    ///
    public class SyncMacro : SyncItemBase<Macro>
    {
        public SyncMacro() :
            base(uSyncSettings.Folder) { }

        public SyncMacro(string folder) :
            base (folder) {}

        public SyncMacro(string folder, string set) :
            base(folder, set) { }

        public override void ExportAll(string folder)
        {
            foreach(Macro item in Macro.GetAll())
            {
                ExportToDisk(item);
            }
        }

        public override void ExportToDisk(Macro item, string folder = null)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (string.IsNullOrEmpty(folder))
                folder = _savePath;

            try
            {
                XElement node = item.SyncExport();
                XmlDoc.SaveNode(folder, item.Alias, node, Constants.ObjectTypes.Macro);
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncMacro>("uSync: Error Saving Macro {0} - {1}", () => item.Name, () => ex.ToString());
            }
        }

        public override void ImportAll(string folder)
        {
            string root = IOHelper.MapPath(string.Format("{0}\\{1}", folder, Constants.ObjectTypes.Macro));
            base.ImportFolder(root);
        }

        public override void Import(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "macro")
                throw new ArgumentException("Not a macro file", filePath);

            if (tracker.MacroChanged(node))
            {
                var backup = Backup(node);

                ChangeItem change = uMacro.SyncImport(node);

                if (change.changeType == ChangeType.Mismatch)
                    Restore(backup);

                AddChange(change);
            }
            else
                AddNoChange(ItemType.Macro, filePath);
        }

        protected override string Backup(XElement node)
        {
            var alias = node.Element("alias").Value;
            var macro = Macro.GetByAlias(alias);

            if ( macro != null )
            {
                ExportToDisk(macro, _backupPath);
                return XmlDoc.GetSavePath(_backupPath, macro.Alias, Constants.ObjectTypes.Macro);
            }

            return "";
        }

        protected override void Restore(string backup)
        {
            XElement backupNode = XmlDoc.GetBackupNode(backup);

            if (backupNode != null)
                uMacro.SyncImport(backupNode, false);
        }

        static string _eventFolder = "";

        public static void AttachEvents(string folder)
        {
            _eventFolder = folder;
            Macro.AfterSave += Macro_AfterSave;
            Macro.AfterDelete += Macro_AfterDelete;
        }

        static void Macro_AfterDelete(Macro sender, DeleteEventArgs e)
        {
            helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), sender.Alias);

            e.Cancel = false;
        }

        static void Macro_AfterSave(Macro sender, SaveEventArgs e)
        {
            SyncMacro m = new SyncMacro();
            m.ExportToDisk(sender, _eventFolder); 
        }
    }
}
