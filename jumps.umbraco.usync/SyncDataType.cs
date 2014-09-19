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

using System.Text.RegularExpressions;

using System.Timers;

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
                    node.AddMD5Hash(true); // md5 hash of file with preval ids blanked.
                    node = ReplaceCotentNodes(node);
                    // content node hunting goes here....
                   
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

                            var dataTypeService = ApplicationContext.Current.Services.DataTypeService;
                            foreach (var node in dataTypeElements)
                            {
                                packagingService.ImportDataTypeDefinitions(node);

                                var def = node.Attribute("Definition");
                                if (def != null)
                                {
                                    var dataTypeDefinitionId = new Guid(def.Value);
                                    var definition = dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionId);
                                    if (definition != null)
                                    {
                                        var cNode = HuntContentNodes(node);
                                        UpdatePreValues(definition, cNode);
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

        private static Timer _saveTimer;
        private static Queue<int> _saveQueue;
        private static object _saveLock; 

        public static void AttachEvents()
        {
            DataTypeService.Saved += DataTypeService_Saved;
            DataTypeService.Deleted += DataTypeService_Deleted;


            // delay trigger - used (upto and including umb 7.1.4
            // saved event on a datatype is called before prevalues
            // are saved - so we just wait a little while before 
            // we save our datatype... 
            //  not ideal but them's the breaks.
            //
            //
            //
            _saveTimer = new Timer(8128); // a perfect waiting time
            _saveTimer.Elapsed += _saveTimer_Elapsed;

            _saveQueue = new Queue<int>();
            _saveLock = new object();
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
                if (e.SavedEntities.Count() > 0)
                {
                    // at the moment this is true for all versions of umbraco 7+
                    // when a fix appears we will version check this code away.
                    //
                    // whole app only runs on 7.1+ so just check for < 7.5 ?
                    // if ( Umbraco.Core.Configuration.UmbracoVersion.Current.Major == 7 && 
                    //      Umbraco.Core.Configuration.UmbracoVersion.Current.Minor < 5 ) {

                    if (uSyncSettings.dataTypeSettings.WaitAndSave)
                    {         
                        // we lock so saves can't happen while we add to the queue.
                        lock (_saveLock)
                        {
                            // we reset the time, this means if two or more saves 
                            // happen close together, then they will be queued up
                            // only when no datatype saves have happened in the 
                            // timer elapsed period will saves start to happen.
                            //
                            _saveTimer.Stop();
                            _saveTimer.Start();

                            foreach (var item in e.SavedEntities)
                            {
                                _saveQueue.Enqueue(item.Id);
                            }
                        }
                    }
                    else
                    {
                        foreach (var item in e.SavedEntities)
                        {
                            SaveToDisk(item);
                        }
                    }
                }
            }
        }

        static void _saveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // we lock so things can't be added to the queue while we save them.
            // typically a save is ~ 50ms
            lock (_saveLock)
            {
                while (_saveQueue.Count > 0)
                {
                    var dataTypeService = ApplicationContext.Current.Services.DataTypeService;

                    int typeId = _saveQueue.Dequeue();

                    var item = dataTypeService.GetDataTypeDefinitionById(typeId);
                    if (item != null)
                    {
                        SaveToDisk(item);
                    }
                }
            }
        }


        #region Node Hunting (the MultiNode Tree Picker fix)

        /// <summary>
        ///  goes through the prevalues and makes content ids portable.
        /// </summary>
        private static XElement ReplaceCotentNodes(XElement node)
        {
            XElement nodepaths = null; 

            var preValueRoot = node.Element("PreValues");
            if (preValueRoot.HasElements)
            {
                var preValues = preValueRoot.Elements("PreValue");
                foreach (var preValue in preValues)
                {
                    if (!((string)preValue.Attribute("Alias")).IsNullOrWhiteSpace())
                    {
                        // look for an alias name that contains a content node
                        if ( uSyncSettings.dataTypeSettings.ContentPreValueAliases.Contains((string)preValue.Attribute("Alias")) )
                        {
                            LogHelper.Info<SyncDataType>("Mapping Content Ids in PreValue {0}", () => preValue.Attribute("Alias"));
                            var propVal = (string)preValue.Attribute("Value");
                            if ( !String.IsNullOrWhiteSpace(propVal)) 
                            {
                                foreach(Match m in Regex.Matches(propVal, @"\d{1,9}"))
                                {
                                    int id ;

                                    if ( int.TryParse(m.Value, out id))
                                    {
                                        // we have an ID : yippe, time to do some walking...
                                        string type = "content";

                                        helpers.ContentWalker cw = new ContentWalker();
                                        string nodePath = cw.GetPathFromID(id);

                                        // Didn't find the content id try media ...
                                        if (string.IsNullOrWhiteSpace(nodePath))                                             
                                        {
                                            type = "media";
                                            helpers.MediaWalker mw = new MediaWalker();
                                            nodePath = mw.GetPathFromID(id);
                                        }

                                        if (!string.IsNullOrWhiteSpace(nodePath))
                                        {
                                            // attach the node tree to the XElement
                                            if (nodepaths == null)
                                            {
                                                nodepaths = new XElement("Nodes");
                                                node.Add(nodepaths);
                                            }
                                            nodepaths.Add(new XElement("Node",
                                                new XAttribute("Id", m.Value),
                                                new XAttribute("Value", nodePath),
                                                new XAttribute("Alias", (string)preValue.Attribute("Alias")),
                                                new XAttribute("Type", type)));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return node;
        }

        /// <summary>
        ///  turns portable content ids back into static ones.
        /// </summary>
        private static XElement HuntContentNodes(XElement node)
        {
            var nodes = node.Element("Nodes");
            var preValues = node.Element("PreValues");

            if (nodes != null && preValues != null ) 
            { 
                if ( nodes.HasElements && preValues.HasElements )
                {
                    LogHelper.Info<SyncDataType>("Mapping PreValue Content Ids to Local Content Id Values"); 

                    foreach(var nodepath in nodes.Elements("Node"))
                    {
                        // go through the mapped things, and see if they are 
                        var alias = (string)nodepath.Attribute("Alias");
                        if ( !String.IsNullOrWhiteSpace(alias))
                        {
                            // find the alias in the preValues...
                            var preVal = preValues.Elements().Where(x => (string)x.Attribute("Alias") == alias).FirstOrDefault();
                            if ( preVal != null )
                            {
                                var preValVal = (string)preVal.Attribute("Value");
                                if (!string.IsNullOrWhiteSpace(preValVal))
                                {
                                    LogHelper.Debug<SyncDataType>("We have the preValue (we think....) {0}", ()=> preValVal);

                                    var nodeidPath = (string)nodepath.Attribute("Value");
                                    var nodeid = (string)nodepath.Attribute("Id");

                                    if ( !string.IsNullOrWhiteSpace(nodeidPath)) {

                                        int id = -1;
                                        
                                        var nodeType = (string)nodepath.Attribute("Type");
                                        if (nodeType == "stylesheet")
                                        {
                                            // it's a stylesheet id - quick swapsy ...
                                            // the core api - doesn't do getStyleSheetByID... so we're not doing this just yet
                                            // 
                                        }
                                        else if ( nodeType == "media" )
                                        {
                                            LogHelper.Debug<SyncDataType>("searching for a media node");
                                            MediaWalker mw = new MediaWalker();
                                            id = mw.GetIdFromPath(nodeidPath);
                                        }
                                        else
                                        {
                                            // content
                                            LogHelper.Debug<SyncDataType>("searching for a content node");
                                            ContentWalker cw = new ContentWalker();
                                            id = cw.GetIdFromPath(nodeidPath);
                                        }

                                        if (id != -1)
                                        {
                                            // try to illiminate changes for changes sake. 
                                            if (preValVal.Contains(nodeid) && nodeid != id.ToString())
                                            {
                                                preVal.SetAttributeValue("Value", preValVal.Replace(nodeid, id.ToString()));
                                                LogHelper.Debug<SyncDataType>("Set preValue value to {0}", () => preVal.Attribute("Value"));
                                            }
                                        }
                                        else
                                        {
                                            LogHelper.Debug<SyncDataType>("We didn't match the pre-value so we're leaving it alone");
                                        }
                                    }
                                    else
                                    {
                                        LogHelper.Info<SyncDataType>("Couldn't retrieve nodeIdPath from Value");
                                    }

                                }
                            }

                        }
                    }
                }
            
            }
            

            return node;
        }

        #endregion

    }
}
