using System;
using System.Collections; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.datatype ;

using umbraco.BusinessLogic ; 

using System.IO;
using Umbraco.Core.IO;
using umbraco;

using Umbraco.Core.Logging;

using jumps.umbraco.usync.helpers;
using jumps.umbraco.usync.Models;
using System.Timers;

using System.Xml.Linq;

namespace jumps.umbraco.usync
{
    /// <summary>
    /// syncs the data types.
    /// </summary>
    public class SyncDataType : SyncItemBase<DataTypeDefinition> 
    {
        public SyncDataType() :
            base(uSyncSettings.Folder) { }

        public SyncDataType(string folder) :
            base(folder) { }

        public SyncDataType(string folder, string set) :
            base(folder, set) { }


        public override void ExportAll(string folder)
        {
            try
            {
                foreach(DataTypeDefinition item in DataTypeDefinition.GetAll())
                {
                    if (item != null)
                        ExportToDisk(item, folder);
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
                folder = _savePath;

            XElement node = ((uDataTypeDefinition)item).SyncExport();

            XmlDoc.SaveNode(folder, item.Text, node, Constants.ObjectTypes.DataType );
            
        }

        public override void ImportAll(string folder)
        {
            string rootFolder = IOHelper.MapPath(String.Format("{0}{1}", folder, Constants.ObjectTypes.DataType));
            ImportFolder(rootFolder);
        }

        private void ImportFolder(string folder)
        {
            if ( Directory.Exists(folder))
            {
                foreach(string file in Directory.GetFiles(folder, Constants.SyncFileMask))
                {
                    Import(file);
                }
            }
        }

        public override void Import(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ArgumentNullException("filePath");

            XElement node = XElement.Load(filePath);

            if (node.Name.LocalName != "DataType")  
                throw new ArgumentException("Not a DataType File", filePath);

            if (tracker.DataTypeChanged(node))
            {
                Backup(node);

                ChangeItem change = uDataTypeDefinition.SyncImport(node);

                if (change.changeType == ChangeType.Mismatch)
                {
                    Restore(node);
                }

                AddChange(change);
            }
            else
            {
                AddNoChange(ItemType.DataType, filePath);
            }
       }

        private void Backup(XElement node)
        {
            var _def = new Guid(node.Attribute("Definition").Value);
            if ( CMSNode.IsNode(_def))
            {
                var dtd = DataTypeDefinition.GetDataTypeDefinition(_def);
                ExportToDisk(dtd, _backupPath);
            }
        }

        private void Restore(XElement node)
        {
            var name = node.Attribute("Name").Value;
            XElement backupNode = XmlDoc.GetBackupNode(_backupPath, name, Constants.ObjectTypes.DataType);

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
                    SyncDataType syncDataType = new SyncDataType(_eventFolder);

                    int typeID = _saveQueue.Dequeue();
                    var dt = DataTypeDefinition.GetDataTypeDefinition(typeID);
                    if (dt != null)
                    {
                        syncDataType.ExportToDisk(dt);
                    }

                    LogHelper.Info<SyncDataType>("DataType Saved (Saving-complete)");
                }
            }
        }

        // after save doesn't fire on DataTypes (it still uses saving)
 
        public static void DataTypeDefinition_Saving(DataTypeDefinition sender, EventArgs e)
        {
            lock ( _saveLock )
            {
                _saveTimer.Stop();
                _saveTimer.Start();

                LogHelper.Info<SyncDataType>("Queuing {0}", () => sender.Id);

                _saveQueue.Enqueue(sender.Id);

            }
            // SaveToDisk((DataTypeDefinition)sender);
        }

        //
        // umbraco 6.0.4 changed the defintion of this event! 
        //
        public static void DataTypeDefinition_AfterDelete(DataTypeDefinition sender, EventArgs e)

        {
            if (typeof(DataTypeDefinition) == sender.GetType())
            {
                helpers.XmlDoc.ArchiveFile(sender.GetType().ToString(), ((DataTypeDefinition)sender).Text);
            }

            // no cancel... 
           
        }
        
    }
}
