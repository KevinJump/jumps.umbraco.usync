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

using System.Timers;

//  Check list
// ====================
//  SaveOne         X
//  SaveAll         X
//  OnSave          (Works in 4.11.5)
//  OnDelete        X
//  ReadFromDisk    X

namespace jumps.umbraco.usync
{
    /// <summary>
    /// syncs the data types.
    /// </summary>
    public class SyncDataType
    {
        public static void SaveToDisk(DataTypeDefinition item)
        {
            if (item != null)
            {
                try
                {
                    XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
                    xmlDoc.AppendChild(DataTypeToXml(item, xmlDoc));
                    helpers.XmlDoc.SaveXmlDoc(item.GetType().ToString(), item.Text, xmlDoc);
                }
                catch (Exception ex)
                {
                    LogHelper.Error<SyncDataType>(string.Format("DataType Failed {0}", item.Text), ex);
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
              
                foreach (DataTypeDefinition item in DataTypeDefinition.GetAll())
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

                User u = new User(0) ; 

                foreach (string file in Directory.GetFiles(path, "*.config"))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(file);
                                      

                    XmlNode node = xmlDoc.SelectSingleNode("//DataType");

                    if (node != null)
                    {
                        DataTypeDefinition d = Import(node, u);
                        if (d != null)
                        {
                            d.Save();
                        }

                        else
                        {
                            LogHelper.Debug<SyncDataType>("NULL NODE FOR {0}", ()=> file);
                        }
                    }
                    
                }
            }
        }

        /// <summary>
        /// DataType Import - taken from the core 
        /// 
        /// the core doesn't pass username, so fails on loading
        /// here we just pass usere User(0) - so we can work)
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static DataTypeDefinition Import(XmlNode xmlData, User u)
        {
            if (xmlData != null)
            {
                string _name = xmlData.Attributes["Name"].Value;
                string _id = xmlData.Attributes["Id"].Value;
                string _def = xmlData.Attributes["Definition"].Value;

                bool isNew = false; 

                DataTypeDefinition dtd;

                if (CMSNode.IsNode(new Guid(_def)))
                {
                    dtd = DataTypeDefinition.GetDataTypeDefinition(new Guid(_def));
                }
                else
                {
                    isNew = true; 

                    if (u == null)
                        u = global::umbraco.BusinessLogic.User.GetUser(0);

                    global::umbraco.cms.businesslogic.datatype.controls.Factory f = new global::umbraco.cms.businesslogic.datatype.controls.Factory();

                    dtd = DataTypeDefinition.MakeNew(u, _name, new Guid(_def));
                    var dataType = f.DataType(new Guid(_id));
                    if (dataType == null)
                        throw new NullReferenceException("Could not resolve a data type with id " + _id);



                    dtd.DataType = dataType;
                    dtd.Save();
                }



                if (!isNew && uSyncSettings.MatchedPreValueDataTypes.Contains(_id))
                {
                    // multi-node tree picker! do a match sync...
                    return MatchImport(dtd, xmlData, u);
                }
                else
                {
                    //
                    // PREVALUES - HELL :: num 4532
                    // 
                    // Here we are attempting to add new prevalues to a DataType, and remove old ones.
                    // between umbraco installs the IDs will change. we are not trying to match them,
                    // we are just trying to match, based on value - problem being, if you change 
                    // a value's value then this code would think it's a new ID, delete the old one
                    // and create a new one - as we are syncing from a dev point of view we are
                    // going to do this for now...
                    //

                    //
                    // MAPPING PREVALUES 
                    // 
                    // When an Export has detected possible mapped ids it creates a nodes element
                    // with a load of node child elements - so if this is there, we need to 
                    // check each preValue to see if it contains an ID from these nodes
                    // 
                    // when we find a match we swap the ID from the prevalue with the one
                    // the mapper function has returned to us.
                    //

                    List<string> mapIds = GetMappingList(xmlData);

                    System.Collections.SortedList prevals = PreValues.GetPreValues(dtd.Id);
                    Hashtable oldvals = new Hashtable();
                    foreach (DictionaryEntry v in prevals)
                    {
                        if ((PreValue)v.Value != null)
                        // if (!String.IsNullOrEmpty(((PreValue)v.Value).Value.ToString()))
                        {
                            oldvals.Add(((PreValue)v.Value).Id, ((PreValue)v.Value).Value.ToString());
                        }
                    }

                    Hashtable newvals = new Hashtable();
                    foreach (XmlNode xmlPv in xmlData.SelectNodes("PreValues/PreValue"))
                    {
                        XmlAttribute val = xmlPv.Attributes["Value"];

                        if (val != null)
                        {
                            // here we need to transpose any mapped ids we might have ...
                            var propValue = val.Value; 
                            if (mapIds.Count() > 0) {
                                foreach (var id in mapIds)
                                {
                                    if ( propValue.Contains(id) )
                                    {
                                        propValue = RemapId(id, propValue, xmlData);
                                       
                                    }
                                }
                            }

                            // add new values only - because if we mess with old ones. it all goes pete tong..
                            if ((propValue != null) && (!oldvals.ContainsValue(propValue)))
                            {
                                LogHelper.Debug<SyncDataType>("Adding Prevalue [{0}]", () => propValue);
                                PreValue p = new PreValue(0, 0, propValue);
                                p.DataTypeId = dtd.Id;
                                p.Save();
                            }

                            newvals.Add(xmlPv.Attributes["Id"], propValue);
                        }
                    }


                    // ok now delete any values that have gone missing between syncs..

                    if (!uSyncSettings.Preserve || !uSyncSettings.PreservedPreValueDataTypes.Contains(_id))
                    {
                        foreach (DictionaryEntry oldval in oldvals)
                        {
                            if (!newvals.ContainsValue(oldval.Value))
                            {
                                PreValue o = new PreValue((int)oldval.Key);
                                LogHelper.Debug<SyncDataType>("In {0} Deleting prevalue [{1}]", ()=> dtd.Text, ()=> oldval.Value);
                                o.Delete();
                            }
                        }
                    }
                    return dtd;
                }
            }
            return null;
        }

        /// <summary>
        ///  the more agressive pre-value manager - basically for Multi-Node Tree Pickers
        ///  
        /// does a like for like change - so goes through exsting values (in order) and sets
        /// their values to those in the import file (in same order) 
        /// 
        /// if the DataType doesn't maintain order (ie in lists) you would loose values doing this
        /// </summary>
        /// <param name="xmlData"></param>
        /// <param name="u"></param>
        /// <returns></returns>
        public static DataTypeDefinition MatchImport(DataTypeDefinition dtd, XmlNode xmlData, User u)
        {
            LogHelper.Debug<SyncDataType>("usync - Match Import: for {0}", ()=> dtd.Text);

            List<PreValue> current = GetPreValues(dtd);
            XmlNodeList target = xmlData.SelectNodes("PreValues/PreValue");

            LogHelper.Debug<SyncDataType>("uSync - Match Import: Counts [{0} Existing] [{1} New]", 
                ()=> current.Count, ()=> target.Count);

            List<string> mapIds = GetMappingList(xmlData);

            for(int n = 0; n < current.Count(); n++)
            {
                XmlAttribute val = target[n].Attributes["Value"];

                if (val != null)
                {
                    // here we need to transpose any mapped ids we might have ...
                    var propValue = val.Value;
                    if (mapIds.Count() > 0)
                    {
                        foreach (var id in mapIds)
                        {
                            if (propValue.Contains(id))
                            {
                                propValue = RemapId(id, propValue, xmlData);
                            }
                        }
                    }

                    if (current[n].Value != propValue)
                    {
                        LogHelper.Debug<SyncDataType>("uSync - Match Import: Overwrite {0} with {1}",
                            () => current[n].Value, () => propValue);
                        current[n].Value = propValue;
                        current[n].Save();
                    }
                }
            }

            LogHelper.Debug<SyncDataType>("uSync - Match Import: Complete");  
            return dtd;
        }

        /// <summary>
        /// DataType ToXML - taken from the core (must learn to patch sometime)
        /// 
        /// fixing basic problem, of prevalues not coming out sorted by id (and sort-order)
        /// with thanks to Kenn Jacobsen for info on this. 
        /// </summary>
        /// <param name="dataType">the datatype to export</param>
        /// <param name="xd">the xmldocument</param>
        /// <returns>the xmlelement representation of the type</returns>
        public static XmlElement DataTypeToXml(DataTypeDefinition dataType, XmlDocument xd)
        {
            // id mapping
            var _id = dataType.DataType.Id.ToString();
            
            var mappings = String.Empty;
            if (uSyncSettings.MappedDataTypes.GetAll().Contains(_id)) {
                mappings = uSyncSettings.MappedDataTypes[_id].Mapping;
            }


            LogHelper.Debug<SyncDataType>("DataType To XML"); 

            XmlElement dt = xd.CreateElement("DataType");
            dt.Attributes.Append(xmlHelper.addAttribute(xd, "Name", dataType.Text));
            dt.Attributes.Append(xmlHelper.addAttribute(xd, "Id", dataType.DataType.Id.ToString()));
            dt.Attributes.Append(xmlHelper.addAttribute(xd, "Definition", dataType.UniqueId.ToString()));



            // templates
            XmlElement prevalues = xd.CreateElement("PreValues");
            foreach (PreValue item in GetPreValues(dataType))
            {
                var preValueValue = item.Value;
                if ( !string.IsNullOrEmpty(mappings) )
                {
                    MapPreValue(preValueValue, mappings, xd, dt);
                }
                
                XmlElement prevalue = xd.CreateElement("PreValue");
                //
                // prevalue.Attributes.Append(xmlHelper.addAttribute(xd, "Alias", item.GetAlias()));
                // alias is often blank :( 
                //
                prevalue.Attributes.Append(xmlHelper.addAttribute(xd, "Id", item.Id.ToString()));
                prevalue.Attributes.Append(xmlHelper.addAttribute(xd, "Value", item.Value));

                prevalues.AppendChild(prevalue);
            }

            dt.AppendChild(prevalues);

            return dt;
        }

        private static string MapPreValue(string preVal, string mappingTypes, XmlDocument xd, XmlElement dt)
        {
            if ( mappingTypes.Contains("content"))
            {
                preVal = PreValMapper.MapContent(preVal, xd, dt);
            }

            if (mappingTypes.Contains("stylesheet"))
            {
                preVal = PreValMapper.MapStylesheets(preVal, xd, dt);
            }

            if (mappingTypes.Contains("tab"))
            {
                preVal = PreValMapper.MapTabs(preVal, xd, dt);
            }

            return preVal;
        }


        private static List<string> GetMappingList(XmlNode xmlData)
        {
            List<string> mapIds = new List<string>();

            if (xmlData.SelectSingleNode("nodes") != null)
            {
                foreach (XmlNode mapNode in xmlData.SelectNodes("nodes/node"))
                {
                    XmlAttribute mapId = mapNode.Attributes["id"];
                    XmlAttribute mapVal = mapNode.Attributes["value"];
                    XmlAttribute mapType = mapNode.Attributes["type"];

                    if (mapId != null && mapVal != null && mapType != null)
                    {
                        // we only add the id to our string if we know we have all the stuff we will
                        // need should we match this.
                        mapIds.Add(mapId.Value);
                    }
                }
            }

            return mapIds;
        }

         // we need to map this back then...
        private static string RemapId(string id, string propValue, XmlNode xmlData)
        {
            LogHelper.Debug<SyncDataType>("Found ID we need to map");

            var mapNode = xmlData.SelectSingleNode(string.Format("//nodes/node[@id='{0}']", id));

            if (mapNode != null)
            {
                var type = mapNode.Attributes["type"].Value;
                var value = mapNode.Attributes["value"].Value;

                LogHelper.Info<SyncDataType>("GetMappedID(\"{0}\", \"{1}\", \"{2}\");"
                    , () => id, () => value, () => type);

                string targetId = PreValMapper.GetMappedId(id, value, type);
                LogHelper.Info<SyncDataType>("[MAPPING] Mapping ID {0} {1}", () => id, () => targetId);

                // replace.
                propValue = propValue.Replace(id, targetId);
                LogHelper.Debug<SyncDataType>("New PropVal {0}", () => propValue);
            }

            return propValue;
        }

        private static List<PreValue> GetPreValues(DataTypeDefinition dataType)
        {
            LogHelper.Debug<SyncDataType>("Getting Pre-Values"); 
            return PreValues.GetPreValues(dataType.Id).Values.OfType<PreValue>().OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToList();
        }

        // timer work.
        private static Timer _saveTimer;
        private static Queue<int> _saveQueue = new Queue<int>();
        private static object _saveLock = new object();

        public static void AttachEvents()
        {
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
                    int typeID = _saveQueue.Dequeue();
                    var dt = DataTypeDefinition.GetDataTypeDefinition(typeID);
                    if (dt != null)
                        SaveToDisk(dt);

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
