using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Xml;


using Umbraco.Core.Services;
using Umbraco.Core.Logging;

using umbraco;
using umbraco.cms.businesslogic.web;

using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;

namespace jumps.umbraco.usync.helpers
{
 
    public class PreValMapper
    {
        #region Export Mappings
        
        
        public static string MapPreValId(string val, XmlDocument xmlDoc, XmlElement node, MappedDataTypeSettings settings)
        {
            LogHelper.Debug<PreValMapper>("Mapping PreValues {0}", () => val);

            var idVals = GetPreValueMatchSubString(val, settings);

            LogHelper.Debug<PreValMapper>("Looking for Ids in {0}", () => idVals);

            foreach (Match m in Regex.Matches(idVals, settings.IdRegEx))
            {
                int id;
                string type = settings.IdObjectType.ToLower();

                if (int.TryParse(m.Value, out id))
                {
                    string mappedVal = string.Empty;

                    switch (type)
                    {
                        case "stylesheet":
                            mappedVal = MapStylesheetId(id);
                            break;
                        case "content":
                            mappedVal = MapContentId(id);
                            if ( string.IsNullOrEmpty(mappedVal))
                            {
                                type = "media";
                                mappedVal = MapMediaId(id);
                            }
                            break;
                        case "tab":
                            mappedVal = MapTabId(id);
                            break;
                    }

                    if ( !string.IsNullOrEmpty(mappedVal))
                    {
                        // add it to the nodes thingy 
                        AddToNode(id, mappedVal, type, xmlDoc, node);
                    }
                }
            }

            return val;
        }


        /// <summary>
        ///  Gets the Sub portion of the preValue string that we think contains the IDs we want to match
        ///  - we do this on import and export - because if we can it reduces the risk of us finding 
        ///  a false posistive in the string.
        ///  
        /// if we can't get a better match we just return the full string.
        /// </summary>
        private static string GetPreValueMatchSubString(string val, MappedDataTypeSettings settings)
        {
            var idVals = val;

            switch (settings.PreValueType.ToLower())
            {
                case "json":
                    // if it's json we load the json, then look fro the named value and set that. 
                    if (!string.IsNullOrEmpty(settings.PropName) && IsJson(val))
                    {
                        JObject jObject = JObject.Parse(val);
                        LogHelper.Debug<PreValMapper>("JSON: {0}", () => jObject.ToString());

                        var propertyValue = jObject.SelectToken(settings.PropName);
                        if (propertyValue != null)
                        {
                            LogHelper.Debug<PreValMapper>("Prop: {0}", () => propertyValue.ToString());
                        }
                    }

                    break;
                case "number":
                    // we just assume that the preValue is the ID so return it ?
                    break;
                case "text":
                    // it's stored in text - we can either just search for the ID or 
                    // if the right values are set we can have a go at getting the right bit
                    if (settings.PropSplit != '\0' && settings.PropPos > 0)
                    {
                        if (val.Contains(settings.PropSplit))
                        {
                            var properties = val.Split(settings.PropSplit);

                            if (properties.Count() >= settings.PropPos)
                            {
                                idVals = properties[settings.PropPos - 1];
                            }
                        }
                    }
                    break;
            }

            return idVals;
        }

        public static string MapStylesheetId(int id)
        {
            var stylesheet = StyleSheet.GetStyleSheet(id, false, false);
            if (stylesheet != null)
            {
                return stylesheet.Text;
            }

            return string.Empty;
        }

        public static string MapContentId(int id)
        {
            helpers.ContentWalker cw = new ContentWalker();
            return cw.GetPathFromID(id);
        }

        public static string MapMediaId(int id)
        {
            helpers.MediaWalker mw = new MediaWalker();
            return mw.GetPathFromID(id);
        }

        public static string MapTabId(int id)
        {
            // use API to get all tabs (alias, id)
            foreach (IContentType contentType in ApplicationContext.Current.Services.ContentTypeService.GetAllContentTypes())
            {
                foreach (PropertyGroup propertyGroup in contentType.PropertyGroups)
                {
                    if ( propertyGroup.Id == id ) {
                        return contentType.Name + "|" + propertyGroup.Name ;
                    }
                }
            }

            return string.Empty;
        }

        private static void AddToNode(int id, string val, string type,  XmlDocument xmlDoc, XmlElement node)
        {
            XmlNode nodes = node.SelectSingleNode("//nodes");
            if ( nodes == null )
            {
                nodes = xmlDoc.CreateElement("nodes");
                node.AppendChild(nodes);
            }

            // add a new element to nodes for this mapping ---
            var mapNode = xmlDoc.CreateElement("node");
            mapNode.Attributes.Append(xmlHelper.addAttribute(xmlDoc, "id", id.ToString()));
            mapNode.Attributes.Append(xmlHelper.addAttribute(xmlDoc, "value", val));
            mapNode.Attributes.Append(xmlHelper.addAttribute(xmlDoc, "type", type));

            nodes.AppendChild(mapNode);

        }


        private static bool IsJson(string input)
        {
            input = input.Trim();
            return (input.StartsWith("{") && input.EndsWith("}"))
                || (input.StartsWith("[") && input.EndsWith("]"));
        }
#endregion

#region Import Mapping 

        /// <summary>
        ///  Replaces IDs in a string with local versions
        /// </summary>
        /// <param name="id"></param>
        /// <param name="propValue"></param>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static string MapIDtoLocal(string id, string propValue, XmlNode xmlData, MappedDataTypeSettings settings)
        {
            LogHelper.Debug<SyncDataType>("Found ID we need to map");
            var mapNode = xmlData.SelectSingleNode(string.Format("//nodes/node[@id='{0}']", id));

            if (mapNode != null)
            {
                var type = mapNode.Attributes["type"].Value;
                var value = mapNode.Attributes["value"].Value;

                var subValueString = GetPreValueMatchSubString(propValue, settings);

                LogHelper.Debug<PreValMapper>("GetMappedID(\"{0}\", \"{1}\", \"{2}\");"
                    , () => id, () => value, () => type);

                string targetId = PreValMapper.GetMappedId(id, value, type);
                LogHelper.Debug<PreValMapper>("[MAPPING] Mapping ID {0} {1}", () => id, () => targetId);

                //
                // replace - first just the little bit we're looking at 
                // then the new replaced bit in the larger string.
                //

                // multi type work around 
                //
                // it's possible to have the new ID be the same as something that is yet to be mapped
                // (rare but possible) so if the string already contains our targetID, we add some rouge
                // charecters to the tartget ID so next pass it won't match 
                //
                // then at the very end (in the datatype functions) we remove the rouges. 
                // 
                if ( subValueString.Contains(targetId)) 
                {
                    // we replace the number with "number:zzusync" 
                    // at the other end we remove the quotes and the zzusync
                    //
                    Regex rgx = new Regex(@"\d{1}");
                    targetId = "\"" + rgx.Replace(targetId, "$0:zzusync") + "\""; 

                    LogHelper.Debug<PreValMapper>("Possible Clash: {0}", () => targetId);
                }

                var targetSubString = subValueString.Replace(id, targetId);
                propValue = propValue.Replace(subValueString, targetSubString);
                LogHelper.Debug<SyncDataType>("New PropVal {0}", () => propValue);
            }

            return propValue;
        }

        public static string StripMarkers(string val)
        {
            Regex rgx = new Regex("(\")(\\d{1,4})(:zzusync)(\")");
            return rgx.Replace(val, "$2");
        }

        /// <summary>
        ///  goes into the xml and gets the list of nodes that need mapping inside this prevalue
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static List<string> GetMapIdList(XmlNode xmlData)
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

        public static string GetMappedId(string id, string val, string type)
        {
            switch (type) {
                case "stylesheet":
                    return GetMappedStylesheetID(id, val);
                case "content":
                    return GetMappedContentID(id, val);
                case "media":
                    return GetMappedMediaID(id, val);
                case "tab":
                    return GetMappedTabId(id, val);
            }

            return id; 
        }

        private static string GetMappedStylesheetID(string id, string val)
        {
            var stylesheet = StyleSheet.GetByName(val);

            if (stylesheet != null)
                return stylesheet.Id.ToString();

            return id; 
        }

        private static string GetMappedContentID(string id, string val)
        {
            ContentWalker cw = new ContentWalker();
            var targetId = cw.GetIdFromPath(val);

            if (targetId != -1)
                return targetId.ToString();

            return id;
        }

        private static string GetMappedMediaID(string id, string val)
        {
            LogHelper.Debug<PreValMapper>("searching for a media node");
            MediaWalker mw = new MediaWalker();
            var targetId = mw.GetIdFromPath(val);

            if (targetId != -1)
                return targetId.ToString();

            return id;
        }

        private static string GetMappedTabId(string id, string val)
        {
            if ( val.Contains('|') && val.Split('|').Count() == 2 )
            {
                var bits = val.Split('|');

                var contentType = ApplicationContext.Current.Services.ContentTypeService.GetContentType(bits[0]);
                if ( contentType != null )
                {
                    LogHelper.Debug<PreValMapper>("Found Content Type for Tab: {0}", () => contentType.Name);
                    foreach(var tab in contentType.PropertyGroups)
                    {
                        if ( tab.Name == bits[1])
                        {
                            LogHelper.Debug<PreValMapper>("Found Tab Name {0} - {1}", () => tab.Name, () => tab.Id);
                            // this is the one
                            return tab.Id.ToString();
                        }
                    }
                }
            }

            return id;
        }
#endregion
    }
}
