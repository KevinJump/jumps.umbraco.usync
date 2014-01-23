using System;
using System.Collections; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;

using System.IO;

using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;

using jumps.umbraco.usync.helpers;

namespace jumps.umbraco.usync
{
    /// <summary>
    /// syncs the data types.
    /// </summary>
    public class SyncDataType
    {
        public static void SaveToDisk(IDataTypeDefinition item)
        {
            if (item != null)
            {
                var packagingService = ApplicationContext.Current.Services.PackagingService;
                try
                {
                    XElement node = packagingService.Export(item);
                    XmlDoc.SaveElement("DataTypeDefinition", XmlDoc.ScrubFile(item.Name), node); 
                }
                catch (Exception ex)
                {
                    LogHelper.Error<SyncDataType>(string.Format("DataType Failed {0}", item.Name), ex);
                }
            }
            else
            {
                LogHelper.Debug<SyncDataType>("Null DataType Save attempt - aborted");
            }
        }

        public static void SaveAllToDisk()
        {
            try
            {
                var dataTypeService = ApplicationContext.Current.Services.DataTypeService;

                foreach (var item in dataTypeService.GetAllDataTypeDefinitions() )
                {
                    if (item != null)
                    {
                        SaveToDisk(item);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Debug<SyncDataType>("Error saving all DataTypes, {0}", ()=> ex.ToString());
            }
        }

        public static void ReadAllFromDisk()
        {
            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "DataTypeDefinition"));

            ReadFromDisk(path); 
        }

        public static void ReadFromDisk(string path)
        {
            if (Directory.Exists(path))
            {
                var packagingService = ApplicationContext.Current.Services.PackagingService; 

                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XElement node = XElement.Load(file);

                    if (node != null)
                    {
                        packagingService.ImportDataTypeDefinitions(node); 
                    }
                }
            }
        }

        public static void AttachEvents()
        {
            DataTypeService.Saved += DataTypeService_Saved;
            DataTypeService.Deleted += DataTypeService_Deleted;
        }

        static void DataTypeService_Deleted(IDataTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IDataTypeDefinition> e)
        {
            foreach(var item in e.DeletedEntities)
            {
                XmlDoc.ArchiveFile("DataTypeDefinition", XmlDoc.ScrubFile(item.Name));
            }
        }

        static void DataTypeService_Saved(IDataTypeService sender, Umbraco.Core.Events.SaveEventArgs<IDataTypeDefinition> e)
        {
            foreach(var item in e.SavedEntities)
            {
                SaveToDisk(item);
            }
        }
        
    }
}
