using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using umbraco.cms.businesslogic.datatype;
using Umbraco.Core.Logging;

using jumps.umbraco.usync.helpers;
using System;
using umbraco.cms.businesslogic;
using System.Collections;
using umbraco.BusinessLogic;

namespace jumps.umbraco.usync.Models
{
    public static class uDataTypeDefinition
    {
        public static XElement SyncExport(this DataTypeDefinition item)
        {
            // id mapping
            var _id = item.DataType.Id.ToString();

            XElement node = new XElement("DataType");
            node.Add(new XAttribute("Name", item.Text));
            node.Add(new XAttribute("Id", _id));
            node.Add(new XAttribute("Definition", item.UniqueId.ToString()));

            PreValueMapper mapper = null;
            if (uSyncSettings.MappedDataTypes.GetAll().Contains(_id))
            {
                var mappedSettings = uSyncSettings.MappedDataTypes[_id];
                mapper = new PreValueMapper(node, mappedSettings);
            }

            XElement preVals = new XElement("PreValues");
            List<PreValue> _preVals = GetPreValues(item);

            foreach (PreValue preValItem in _preVals)
            {
                /// pre-value mapping 
                /// - go off and try to get any id' mapped to something
                /// - more portable.
                /// 
                var preValueValue = preValItem.Value;

                XElement preValue = new XElement("PreValue");
                preValue.Add(new XAttribute("Id", preValItem.Id.ToString()));
                preValue.Add(new XAttribute("Value", preValItem.Value.ToString()));

                if (mapper != null)
                {
                    Guid _specialId = Guid.NewGuid();
                    if (mapper.MapIdToValue(preValueValue, _specialId))
                    {
                        preValue.Add(new XAttribute("MapGuid", _specialId));
                    }
                }

                preVals.Add(preValue);
            }

            node.Add(preVals);


            LogHelper.Debug<SyncDataType>("{0}", () => node.ToString());
            return node;
        }

        public static ChangeItem SyncImport(XElement node, bool postCheck = true)
        {
            var change = new ChangeItem
            {
                itemType = ItemType.DataType,
                changeType = ChangeType.Success,
                name = node.Attribute("Name").Value,
            };

            DataTypeDefinition dtd = ImportItem(node);
            if (dtd != null)
            {
                change.id = dtd.Id;
                change.name = dtd.Text;

                // post change
                if (postCheck && tracker.DataTypeChanged(node))
                {
                    change.changeType = ChangeType.Mismatch;
                    change.message = "Import didn't match";
                }
            }
            else
            {
                change.changeType = ChangeType.ImportFail;
            }

            return change;
        }

        public static ChangeItem SyncDelete(string def, bool reportOnly = false)
        {
            var change = ChangeItem.DeleteStub(def, ItemType.DataType);

            if (CMSNode.IsNode(new Guid(def)))
            {
                
                // delete
                var dtd = DataTypeDefinition.GetDataTypeDefinition(new Guid(def));

                change.changeType = ChangeType.Delete;
                change.name = dtd.Text;

                if (!reportOnly)
                {
                    LogHelper.Info<SyncDataType>("Deleted Datatype: {0}", () => change.name);

                    change.changeType = ChangeType.Delete;
                    dtd.delete();
                }
                else
                {
                    change.changeType = ChangeType.WillChange;
                }

            }

            return change;
        }

        /// <summary>
        ///  takes xml and converts it to something we can then just import
        ///  this means mapping IDs etc...
        ///  
        ///  We do it like this - because it makes comparisons between what 
        ///  we want to import and what we have much easier. 
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        internal static XElement ConvertToImportXML(XElement node)
        {
            XElement nodeCopy = new XElement(node);
            var id = node.Attribute("Id").Value;

            PreValueMapper mapper = null;
            if (uSyncSettings.MappedDataTypes.GetAll().Contains(id))
            {
                var mappedSettings = uSyncSettings.MappedDataTypes[id];
                mapper = new PreValueMapper(nodeCopy, mappedSettings);
            }

            if ( mapper != null )
            {
                var preValues = nodeCopy.Element("PreValues");
                if ( preValues != null && preValues.HasElements)
                {
                    foreach (var preValue in preValues.Descendants()
                        .Where(x => x.Attribute("MapGuid") != null).ToList())
                    {
                        var value = mapper.MapValueToID(preValue);

                        if (!string.IsNullOrEmpty(value))
                        {
                            preValue.Attribute("Value").Value = value;
                        }

                        preValue.Attribute("MapGuid").Remove();
                    }
                }
            }


            if (nodeCopy.Element("Nodes") != null)
                nodeCopy.Element("Nodes").Remove();
            return nodeCopy; 
        }
     

        /// <summary>
        /// DataType Import - taken from the core 
        /// 
        /// the core doesn't pass username, so fails on loading
        /// here we just pass usere User(0) - so we can work)
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        private static DataTypeDefinition ImportItem(XElement sourceNode)
        {
            if (sourceNode != null)
            {
                XElement node = ConvertToImportXML(sourceNode);

                string _name = node.Attribute("Name").Value;
                LogHelper.Debug<SyncDataType>("Importing: {0}", () => _name);

                
                string _id = node.Attribute("Id").Value;
                string _def = node.Attribute("Definition").Value;

                bool isNew = false;

                DataTypeDefinition dtd;

                global::umbraco.cms.businesslogic.datatype.controls.Factory f = new global::umbraco.cms.businesslogic.datatype.controls.Factory();

                if (CMSNode.IsNode(new Guid(_def)))
                {
                    dtd = DataTypeDefinition.GetDataTypeDefinition(new Guid(_def));

                    if ( dtd != null )
                    {
                        dtd.Text = _name;

                        var dataType = f.DataType(new Guid(_id));
                        if (dataType == null && dataType.Id != null)
                            throw new NullReferenceException("Could not resolve a data type with id " + _id);

                        if ( dtd.DataType.Id != dataType.Id )
                        {
                            // change type ? 
                            // lets have a go see if this works !
                            dtd.DataType = dataType;
                        }
                        dtd.Save();
                                               
                    }
                }
                else
                {
                    isNew = true;

                    var u = global::umbraco.BusinessLogic.User.GetUser(0);

                    dtd = DataTypeDefinition.MakeNew(u, _name, new Guid(_def));
                    var dataType = f.DataType(new Guid(_id));
                    if (dataType == null && dataType.Id != null)
                        throw new NullReferenceException("Could not resolve a data type with id " + _id);

                    dtd.DataType = dataType;
                    dtd.Save();
                }

                if (dtd == null || dtd.DataType == null)
                {
                    LogHelper.Info<SyncDataType>("Import Failed for [{0}] .uSync Could not find the underling type", () => _name);
                    return null;
                }
                

                if (!isNew && uSyncSettings.MatchedPreValueDataTypes.Contains(_id))
                {
                    // multi-node tree picker! do a match sync...
                    return MatchImport(dtd, node);
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
                    foreach (var xmlPv in node.Element("PreValues").Elements("PreValue"))
                    {
                        var val = xmlPv.Attribute("Value");

                        if (val != null)
                        {
                            var propValue = val.Value;

                            // add new values only - because if we mess with old ones. it all goes pete tong..
                            if ((propValue != null) && (!oldvals.ContainsValue(propValue)))
                            {
                                LogHelper.Debug<SyncDataType>("Adding Prevalue [{0}]", () => propValue);
                                PreValue p = new PreValue(0, 0, propValue);
                                p.DataTypeId = dtd.Id;
                                p.Save();
                            }

                            newvals.Add(xmlPv.Attribute("Id"), propValue);
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
                                LogHelper.Debug<SyncDataType>("In {0} Deleting prevalue [{1}]", () => dtd.Text, () => oldval.Value);
                                o.Delete();
                            }
                        }
                    }
                    return dtd;
                }
                LogHelper.Debug<SyncDataType>("Finished Import: {0}", () => _name);
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
        private static DataTypeDefinition MatchImport(DataTypeDefinition dtd, XElement node)
        {
            LogHelper.Debug<SyncDataType>("usync - Match Import: for {0}", () => dtd.Text);

            List<PreValue> current = GetPreValues(dtd);
            var target = node.Element("PreValues").Elements("PreValue").ToArray();

            LogHelper.Debug<SyncDataType>("uSync - Match Import: Counts [{0} Existing] [{1} New]",
                () => current.Count, () => target.Count());

            for (int n = 0; n < current.Count(); n++)
            {
                var val = target[n].Attribute("Value");

                if (val != null)
                {
                    // here we need to transpose any mapped ids we might have ...
                    var propValue = val.Value;

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



        // we need to map this back then...
        private static List<PreValue> GetPreValues(DataTypeDefinition dataType)
        {
            LogHelper.Debug<SyncDataType>("Getting Pre-Values"); 
            return PreValues.GetPreValues(dataType.Id).Values.OfType<PreValue>().OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToList();
        }
        

    }
}
