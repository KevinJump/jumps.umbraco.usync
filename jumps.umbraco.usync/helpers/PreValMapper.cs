using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Xml; 

using Umbraco.Core.Logging;

using umbraco;
using umbraco.cms.businesslogic.web;

using System.Text.RegularExpressions;

namespace jumps.umbraco.usync.helpers
{
    public class PreValMapper
    {

#region Export Mappings 
        public static string MapContent(string val, XmlDocument xmlDoc, XmlElement node)
        {
            LogHelper.Info<PreValMapper>("Mapping Content in {0}", () => val);
            return val;
        }

        public static string MapStylesheets(string val, XmlDocument xmlDoc, XmlElement node)
        {
            LogHelper.Info<PreValMapper>("Mapping Stylesheets in {0}", () => val);
            
            // need to get these via config ?
            char split = '|';
            int splitCount = 6;
            
            var idVals = val;

            // we try to be clever, if we can
            if (val.Contains(split))
            {
                var elements = val.Split(split);

                if (elements.Count() >= splitCount)
                {
                    idVals = elements[splitCount-1];
                }
            }

            LogHelper.Info<PreValMapper>("We thing this has stylesheet IDs in it [{0}]", ()=> idVals);

            foreach (Match m in Regex.Matches(idVals, @"\d{1,9}"))
            {
                int id;
                if (int.TryParse(m.Value, out id))
                {
                    LogHelper.Info<PreValMapper>("Mapping the Stylesheet ID {0} to something", ()=> id);
                    var stylesheet = StyleSheet.GetStyleSheet(id, false, false);
                    if ( stylesheet != null )
                    {
                        LogHelper.Info<PreValMapper>("Mapping {0} to {1}", () => id, () => stylesheet.Text);
                        AddToNode(id, stylesheet.Text, "stylesheet", xmlDoc, node);
                    }

                }
            }
            return val;
        }

        public static string MapTabs(string val, XmlDocument xmlDoc, XmlElement node)
        {
            LogHelper.Info<PreValMapper>("Mapping Tabs in {0}", () => val);
            return val;
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
#endregion

#region Import Mapping 
        public static string GetMappedId(string id, string val, string type)
        {
            if ( type == "stylesheet")
            {
                return GetMappedStylesheetID( id,  val);
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
#endregion
    }
}
