using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using Umbraco.Core.Models;

using Newtonsoft.Json.Linq;
using Umbraco.Core.Logging;

namespace jumps.umbraco.usync.helpers
{
    public class PreValueMapper
    {
        MappedDataTypeSettings _mSettings;
        XElement _node;

        public PreValueMapper(XElement node, MappedDataTypeSettings settings)
        {
            _mSettings = settings;
            _node = node;
        }

        #region Exporter

        /// <summary>
        ///  Maps and internal ID into something we can pass between
        ///  umbraco installations
        /// </summary>
        /// <returns></returns>
        public bool MapIdToValue(string value, Guid guid)
        {
            bool mapped = false; 
            var IdValues = GetValueMatchSubstring(value);

            foreach(Match match in Regex.Matches(IdValues, _mSettings.IdRegEx))
            {
                int id;
                string type = _mSettings.IdObjectType.ToLower();

                if (int.TryParse(match.Value, out id))
                {
                    string mappedValue = string.Empty;

                    switch(type)
                    {
                        case "stylesheet":
                            mappedValue = MapStylesheetId(id);
                            break;
                        case "content":
                            mappedValue = MapContentId(id);
                            if (string.IsNullOrEmpty(mappedValue))
                            {
                                type = "media";
                                mappedValue = MapMediaId(id);
                            }

                            break;
                        case "tab":
                            mappedValue = MapTabId(id);
                            break;
                    }

                    if ( !string.IsNullOrEmpty(mappedValue))
                    {
                        // we've mapped something...
                        AddToNode(id, mappedValue, type, guid);
                        mapped = true;
                    }
                }
            }

            return mapped;
        }

        internal string GetValueMatchSubstring(string value)
        {
            if (_mSettings == null)
                return value;

            var idSubString = value;

            switch(_mSettings.PreValueType)
            {
                case "json":
                    if ( !string.IsNullOrEmpty(_mSettings.PropName) && IsJson(value))
                    {
                        JObject jObject = JObject.Parse(value);

                        var propertyValue = jObject.SelectToken(_mSettings.PropName);
                        if ( propertyValue != null )
                        {
                            return propertyValue.ToString(Newtonsoft.Json.Formatting.None);
                        }
                    }
                    break;
                case "number":
                    break;
                case "text":
                    if ( _mSettings.PropSplit != '\0' && _mSettings.PropPos > 0)
                    {
                        if ( value.Contains(_mSettings.PropSplit))
                        {
                            var properties = value.Split(_mSettings.PropSplit);

                            if (properties.Count() >= _mSettings.PropPos)
                            {
                                idSubString = properties[_mSettings.PropPos - 1];
                            }

                        }
                    }
                    break;
            }

            return idSubString;
        }


        private string MapStylesheetId(int id)
        {
            var stylesheet = StyleSheet.GetStyleSheet(id, false, false);
            if (stylesheet != null)
                return stylesheet.Text;

            return string.Empty;
        }

        private string MapContentId(int id)
        {
            helpers.ContentWalker cw = new ContentWalker();
            return cw.GetPathFromID(id);
        }

        private string MapMediaId(int id)
        {
            helpers.MediaWalker mw = new MediaWalker();
            return mw.GetPathFromID(id);
        }

        private string MapTabId(int id)
        {
            foreach(IContentType contentType in ApplicationContext.Current.Services.ContentTypeService.GetAllContentTypes())
            {
                foreach (PropertyGroup propertyGroup in contentType.PropertyGroups)
                {
                    if (propertyGroup.Id == id)
                    {
                        return contentType.Name + "|" + propertyGroup.Name;
                    }
                }
            }

            return string.Empty;
        }


        private void AddToNode(int id, string value, string type, Guid guid)
        {
            XElement nodes = _node.Element("Nodes");
            if (nodes == null)
            {
                nodes = new XElement("Nodes");
                _node.Add(nodes);
            }

            var mapNode = new XElement("Node");
            mapNode.Add(new XAttribute("Id", id.ToString()));
            mapNode.Add(new XAttribute("Value", value));
            mapNode.Add(new XAttribute("Type", type));
            mapNode.Add(new XAttribute("MapGuid", guid.ToString()));
            nodes.Add(mapNode);
        }


        private bool IsJson(string input)
        {
            input = input.Trim();
            return (input.StartsWith("{") && input.EndsWith("}"))
                || (input.StartsWith("[") && input.EndsWith("]"));
        }
        #endregion

        #region Importer

        public string MapValueToID(XElement preValue)
        {
            var value = preValue.Attribute("Value").Value;

            var MapGuid = preValue.Attribute("MapGuid");
            if (MapGuid == null)
                return value;

        
            var mapNodes = _node.Element("Nodes").Descendants()
                .Where(x => x.Attribute("MapGuid").Value == MapGuid.Value)
                .ToList();

            foreach(var mapNode in mapNodes)
            {
                var mapType = mapNode.Attribute("Type").Value;
                var mapValue = mapNode.Attribute("Value").Value;
                var mapId = mapNode.Attribute("Id").Value;

                var valueSubString = GetValueMatchSubstring(value);

                // convers the mapping to a local id - the thing we're putting in
                string localId = GetMappedId(mapId, mapValue, mapType);

                // look in the existing string, to see if we might have a clash 
                Regex existingRegEx = new Regex(string.Format("{0}(?!:zzusync)", localId));
                if (existingRegEx.IsMatch(valueSubString))  
                {
                    // what's happened here is the target value string already contains our 
                    // target id - so we add some strings to our target, to stop us
                    // from confusing the id we're putting in with anything else.
                    Regex rgx = new Regex(@"\d{1}(?!:zzusync)");
                    localId = "\"" + rgx.Replace(localId, "$0:zzusync") + "\""; 
                    // at the end of our mapping process we clean out the extra bits.
                }

                // replace the mapped id with the new local one, 
                // ** but only if it doesn't have :zzusync appended to it **                
                Regex mapRegEx = new Regex(string.Format("{0}(?!:zzusync)", mapId));
                var targetSubString = mapRegEx.Replace(valueSubString, localId);

                value = value.Replace(valueSubString, targetSubString);
            }

            return CleanValue(value);
        }

        public List<XElement> GetMapNodes()
        {
            var nodeRoot = _node.Element("Nodes");
            if (nodeRoot != null && nodeRoot.HasElements)
            {
                return nodeRoot.Descendants().Where(x => x.Name.LocalName == "Node").ToList();
            }

            return null;

        }

        public string CleanValue(string value)
        {
            var looper = 0;
            while (value.Contains(":zzusync") && looper < 5)
            {
                looper++;
                Regex rgx = new Regex("\"?(\\d{1,4})(:zzusync\"?)");
                var cleaned = rgx.Replace(value, "$1");
                value = cleaned; 
            }

            if (value.Contains(":zzusync"))
                value = value.Replace(":zzusync", "");

            return value;
        }

        private string GetMappedId(string id, string value, string type)
        {
            switch(type)
            {
                case "stylesheet":
                    return GetMappedStylesheetId(id, value);
                case "content":
                    return GetMappedContentId(id, value);
                case "media":
                    return GetMappedMediaId(id, value);
                case "tab":
                    return GetMappedTabId(id, value);
            }

            return id;
        }

        private string GetMappedStylesheetId(string id, string value)
        {
            var stylesheet = StyleSheet.GetByName(value);

            if (stylesheet != null)
            {
                LogHelper.Debug<PreValueMapper>("Stylesheet ID Match, Mapping {0} => {1}", () => id, () => stylesheet.Id);
                return stylesheet.Id.ToString();
            }

            return id;
        }

        private string GetMappedContentId(string id, string value)
        {
            ContentWalker cw = new ContentWalker();
            var targetId = cw.GetIdFromPath(value);

            if (targetId != -1)
            {
                LogHelper.Debug<PreValueMapper>("Content ID Match, Mapping {0} => {1}", () => id, () => targetId);
                return targetId.ToString();
            }

            return id;
        }

        private string GetMappedMediaId(string id, string value)
        {
            MediaWalker mw = new MediaWalker();
            var targetId = mw.GetIdFromPath(value);

            if (targetId != -1)
            {
                LogHelper.Debug<PreValueMapper>("Media ID Match, Mapping {0} => {1}", () => id, ()=> targetId);
                return targetId.ToString();
            }

            return id;
        }

        private string GetMappedTabId(string id, string value)
        {
            if (value.Contains('|') && value.Split('|').Count() == 2)
            {
                var bits = value.Split('|');

                var contentType = ApplicationContext.Current.Services.ContentTypeService.GetContentType(bits[0]);
                if (contentType != null)
                {
                    foreach (var tab in contentType.PropertyGroups)
                    {
                        if (tab.Name == bits[1])
                        {
                            // this is the one
                            LogHelper.Debug<PreValueMapper>("TabID Match, Mapping {0} => {1}", ()=> id, () => tab.Id);
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
