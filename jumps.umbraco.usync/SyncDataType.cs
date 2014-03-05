using System;
using System.Collections; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics; 

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
                    node.AddMD5Hash();
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
                Stopwatch sw = new Stopwatch();
                sw.Start();

                var dataTypeService = ApplicationContext.Current.Services.DataTypeService;

                foreach (var item in dataTypeService.GetAllDataTypeDefinitions() )
                {
                    if (item != null)
                    {
                        SaveToDisk(item);
                    }
                }

                sw.Stop();
                LogHelper.Info<uSync>("Datatypes to disk ({0}ms}", () => sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                LogHelper.Debug<SyncDataType>("Error saving all DataTypes, {0}", ()=> ex.ToString());
            }
        }

        public static void ReadAllFromDisk()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            string path = IOHelper.MapPath(string.Format("{0}{1}",
                helpers.uSyncIO.RootFolder,
                "DataTypeDefinition"));

            ReadFromDisk(path);

            sw.Stop();
            LogHelper.Info<uSync>("Processed data types ({0}ms)", () => sw.ElapsedMilliseconds);
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
                        if (Tracker.DataTypeChanged(node))
                        {

                            var dataTypeService = ApplicationContext.Current.Services.DataTypeService;

                            LogHelper.Debug<uSync>("Importing DataType {0}", () => file);
                            packagingService.ImportDataTypeDefinitions(node);

                            var dataTypeDefinitionId = new Guid(node.Attribute("Definition").Value);

                            var definition = dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionId);

                            if (definition != null)
                            {
                                /*
                                 can't do this - the proerties are private/interal
                                 */

                                /*
                                var id = node.Attribute("Definition").Value;
                                var definition = dataTypeService.GetDataTypeDefinitionById(id);

                                if ( definition != null )
                                {
                                
                                }
                                */

                                UpdatePreValues(definition, node);
                            }
                        }
                    }
                }
            }
        }

        private static void UpdatePreValues(IDataTypeDefinition dataType, XElement node)
        {
            // LogHelper.Info<uSync>("Updating preValues {0}", () => dataType.Id);
            var preValues = node.Element("PreValues");
            var dataTypeSerivce = ApplicationContext.Current.Services.DataTypeService;

            if (preValues != null)
            {
                var valuesWithoutKeys = preValues.Elements("PreValue")
                                                      .Where(x => ((string)x.Attribute("Alias")).IsNullOrWhiteSpace())
                                                      .Select(x => x.Attribute("Value").Value);

                var valuesWithKeys = preValues.Elements("PreValue")
                                                     .Where(x => ((string)x.Attribute("Alias")).IsNullOrWhiteSpace() == false)
                                                     .ToDictionary(key => (string)key.Attribute("Alias"), val => new PreValue((string)val.Attribute("Value")));

                dataTypeSerivce.SavePreValues(dataType.Id, valuesWithKeys);
                dataTypeSerivce.SavePreValues(dataType.Id, valuesWithoutKeys);
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
