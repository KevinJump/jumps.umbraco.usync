using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Logging;

using System.Xml;
using System.Xml.Linq;

using System.IO; 

using jumps.umbraco.usync.helpers;

namespace jumps.umbraco.usync
{
    /// <summary>
    ///  umbraco7+ data types compleatly re-written. 
    /// </summary>
    public class SyncDataTypesV7
    {
        static IDataTypeService _dtService ;
        static PackagingService _packService; 
        static SyncDataTypesV7()
        {
            _dtService = ApplicationContext.Current.Services.DataTypeService;
            _packService = ApplicationContext.Current.Services.PackagingService;
        }

        public static void SaveAllToDisk()
        {
            try {
                foreach( IDataTypeDefinition dataType in _dtService.GetAllDataTypeDefinitions() )
                {
                    if (dataType != null)
                    {
                        SaveToDisk(dataType); 
                    }
                }
            }
            catch ( Exception ex )
            {
                LogHelper.Error<SyncDataTypesV7>("error saving to disk", ex);
            }
        }

        public static void SaveToDisk(IDataTypeDefinition item)
        {
            if (item != null)
            {
                try
                {
                    LogHelper.Info<SyncDataTypesV7>("Syncing item {0}", () => item.Name);
                    XElement root = new XElement("DataTypes");
                    XElement element = Export(item);
                    root.Add(element) ; 
                    XmlDoc.SaveElement("DataTypeDefinition", item.Name.ToSafeAlias(), root);

                }
                catch (Exception ex)
                {
                    LogHelper.Error<SyncDataTypesV7>("error saving item", ex);
                }
            }
        }

        public static void AttachEvents()
        {
            DataTypeService.Deleted += DataTypeService_Deleted;
            DataTypeService.Saved += DataTypeService_Saved;
        }

        static void DataTypeService_Saved(IDataTypeService sender, Umbraco.Core.Events.SaveEventArgs<IDataTypeDefinition> e)
        {
            LogHelper.Info<SyncDataTypesV7>("Saved fired");
            foreach (IDataTypeDefinition item in e.SavedEntities)
            {
                SaveToDisk(item);
            }
        }

        static void DataTypeService_Deleted(IDataTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IDataTypeDefinition> e)
        {
            LogHelper.Info<SyncDataTypesV7>("Delete fired");
            foreach (IDataTypeDefinition item in e.DeletedEntities)
            {
                helpers.XmlDoc.ArchiveFile("DataTypeDefinition", item.Name.ToSafeAlias());
            }
           
        }

        private static XElement Export(IDataTypeDefinition dataTypeDefinition)
        {
            var prevalues = new XElement("PreValues");

            var prevalueList = _dtService.GetPreValuesCollectionByDataTypeId(dataTypeDefinition.Id)
                .FormatAsDictionary();

            var sort = 0;
            foreach (var pv in prevalueList)
            {
                var prevalue = new XElement("PreValue");
                prevalue.Add(new XAttribute("Id", pv.Value.Id));
                prevalue.Add(new XAttribute("Value", pv.Value.Value ?? ""));
                prevalue.Add(new XAttribute("Alias", pv.Key));
                prevalue.Add(new XAttribute("SortOrder", sort));
                prevalues.Add(prevalue);
                sort++;
            }

            var xml = new XElement("DataType", prevalues);
            xml.Add(new XAttribute("Name", dataTypeDefinition.Name));
            //The 'ID' when exporting is actually the property editor alias (in pre v7 it was the IDataType GUID id)
            xml.Add(new XAttribute("Id", dataTypeDefinition.PropertyEditorAlias));
            xml.Add(new XAttribute("Definition", dataTypeDefinition.Key));
            xml.Add(new XAttribute("DatabaseType", dataTypeDefinition.DatabaseType.ToString()));

            return xml;
        }

        public static void ReadAllFromDisk()
        {
            string path = Umbraco.Core.IO.IOHelper.MapPath(
                string.Format("{0}{1}", uSyncIO.RootFolder, "DataTypeDefinition"));

            ReadFromDisk(path); 
        }

        private static void ReadFromDisk(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XElement node = XElement.Load(file);
                    if (node != null)
                    {
                        LogHelper.Info<SyncDataTypesV7>("Importing Node {0}", ()=> node.Name.LocalName);
                        // import
                        _packService.ImportDataTypeDefinitions(node); 
                    }
                }
            }
        }
    }
}
