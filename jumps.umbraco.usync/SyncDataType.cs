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
                LogHelper.Info<uSync>("Datatypes to disk ({0}ms)", () => sw.ElapsedMilliseconds);
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
                    XElement element = XElement.Load(file);

                    if (element != null)
                    {
                        if (Tracker.DataTypeChanged(element))
                        {
                            var name = element.Name.LocalName;
                            var dataTypeElements = name.Equals("DataTypes")
                                           ? (from doc in element.Elements("DataType") select doc).ToList()
                                           : new List<XElement> { element };

                            foreach (var node in dataTypeElements)
                            {
                                var dataTypeService = ApplicationContext.Current.Services.DataTypeService;
                                packagingService.ImportDataTypeDefinitions(node);

                                var def = node.Attribute("Definition");
                                if (def != null)
                                {
                                    var dataTypeDefinitionId = new Guid(def.Value);
                                    var definition = dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionId);
                                    if (definition != null)
                                    {
                                        UpdatePreValues(definition, node);
                                    }
                                }
                            } /* end for each */
                        }
                    }
                }
            }
        }

        private static void UpdatePreValues(IDataTypeDefinition dataType, XElement node)
        {
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
            if (!uSync.EventsPaused)
            {
                foreach (var item in e.DeletedEntities)
                {
                    XmlDoc.ArchiveFile("DataTypeDefinition", XmlDoc.ScrubFile(item.Name));
                }
            }
        }

        static void DataTypeService_Saved(IDataTypeService sender, Umbraco.Core.Events.SaveEventArgs<IDataTypeDefinition> e)
        {
            if (!uSync.EventsPaused)
            {
                foreach (var item in e.SavedEntities)
                {
                    SaveToDisk(item);
                }
            }
        }
        
    }
}
