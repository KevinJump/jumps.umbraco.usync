using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using System.Xml.Linq;

using umbraco;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.datatype;

using Umbraco.Core.IO;
using Umbraco.Core.Logging;

using jumps.umbraco.usync.helpers;
using jumps.umbraco.usync.Models;

namespace jumps.umbraco.usync
{
    /// <summary>
    /// syncs the data types.
    /// </summary>
    public class SyncDataType : SyncItemBase<DataTypeDefinition> 
    {
        public SyncDataType() : base() { }

        public SyncDataType(ImportSettings settings) :
            base(settings) { }


        public override void ExportAll()
        {
            try
            {
                foreach(DataTypeDefinition item in DataTypeDefinition.GetAll())
                {
                    if (item != null)
                        ExportToDisk(item, _settings.Folder);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Info<SyncDataType>("Error saving all datatypes {0}",
                    () => ex.ToString());
            }
        }

        public override void ExportToDisk(DataTypeDefinition item, string folder = null)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (string.IsNullOrEmpty(folder))
                folder = _settings.Folder;

            XElement node = item.SyncExport();

            XmlDoc.SaveNode(folder, item.Text, node, Constants.ObjectTypes.DataType );
            
        }

        public override void ImportAll()
        {
            string rootFolder = IOHelper.MapPath(String.Format("{0}\\{1}", _settings.Folder, Constants.ObjectTypes.DataType));
            base.ImportFolder(rootFolder);
        }
        
        public override void Import(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "DataType")  
                throw new ArgumentException("Not a DataType File", filePath);

            if (_settings.ForceImport || tracker.DataTypeChanged(node))
            {
                if (!_settings.ReportOnly)
                {
                    var backup = Backup(node);

                    ChangeItem change = uDataTypeDefinition.SyncImport(node);

                    if (uSyncSettings.ItemRestore && change.changeType == ChangeType.Mismatch)
                    {
                        Restore(backup);
                        change.changeType = ChangeType.RolledBack;
                    }
                    uSyncReporter.WriteToLog("Imported DataType [{0}] {1}", change.name, change.changeType.ToString());

                    AddChange(change);
                }
                else
                {
                    AddChange(new ChangeItem
                    {
                        changeType = ChangeType.WillChange,
                        itemType = ItemType.DataType,
                        name = node.Attribute("Name").Value,
                        message = "Reporting: will update"
                    });
                }
            }
            else
            {
                AddNoChange(ItemType.DataType, filePath);
            }
       }

        protected override string Backup(XElement node)
        {
            var _def = new Guid(node.Attribute("Definition").Value);
            if ( CMSNode.IsNode(_def))
            {
                var dtd = DataTypeDefinition.GetDataTypeDefinition(_def);
                ExportToDisk(dtd, _settings.BackupPath);
                return XmlDoc.GetSavePath(_settings.BackupPath, dtd.Text, Constants.ObjectTypes.DataType);
            }
            return "";
        }

        protected override void Restore(string backup)
        {
            XElement backupNode = XmlDoc.GetBackupNode(backup);

            if (backupNode != null)
                uDataTypeDefinition.SyncImport(backupNode, false);
        }

 
        // timer work.
        private static Timer _saveTimer;
        private static Queue<int> _saveQueue = new Queue<int>();
        private static object _saveLock = new object();

        private static string _eventFolder = "" ;

        public static void AttachEvents(string folder)
        {
            _eventFolder = folder; 

            // this only fires in 4.11.5 + 
            DataTypeDefinition.Saving += new DataTypeDefinition.SaveEventHandler(DataTypeDefinition_Saving);
            // DataTypeDefinition.AfterSave += DataTypeDefinition_AfterSave;

            // but this is 
            DataTypeDefinition.AfterDelete += DataTypeDefinition_AfterDelete;

            // delay trigger - saving means we can sometimes miss
            // pre-value saving things - so we do a little wait
            // after we get the saving event before we jump in
            // and do the save - gets over this.
            _saveTimer = new Timer(4064);
            _saveTimer.Elapsed += _saveTimer_Elapsed;
        }

        static void _saveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock ( _saveLock )
            {
                while ( _saveQueue.Count > 0 )
                {
                    LogHelper.Info<SyncDataType>("DataType Saving (Saving)");
                    // do the save.
                    SyncDataType syncDataType = new SyncDataType();

                    int typeID = _saveQueue.Dequeue();
                    var dt = DataTypeDefinition.GetDataTypeDefinition(typeID);
                    if (dt != null)
                    {
                        syncDataType.ExportToDisk(dt, _eventFolder);
                    }
                }
            }
        }

        // after save doesn't fire on DataTypes (it still uses saving)
 
        public static void DataTypeDefinition_Saving(DataTypeDefinition sender, EventArgs e)
        {
            if (!uSync.EventPaused)
            {
                lock (_saveLock)
                {
                    _saveTimer.Stop();
                    _saveTimer.Start();

                    LogHelper.Info<SyncDataType>("Queuing {0}", () => sender.Id);

                    _saveQueue.Enqueue(sender.Id);

                }
                // SaveToDisk((DataTypeDefinition)sender);
            }
        }

        //
        // umbraco 6.0.4 changed the defintion of this event! 
        //
        public static void DataTypeDefinition_AfterDelete(DataTypeDefinition sender, EventArgs e)
        {
            if (!uSync.EventPaused)
            {
                if (typeof(DataTypeDefinition) == sender.GetType())
                {
                    XmlDoc.ArchiveFile(XmlDoc.GetSavePath(_eventFolder, sender.Text, Constants.ObjectTypes.DataType), true);
                }

                // no cancel... 
            }           
        }
        
    }
}
