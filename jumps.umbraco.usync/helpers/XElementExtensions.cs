using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;

namespace jumps.umbraco.usync.helpers
{
    public static class XElementExtensions
    {

        /// <summary>
        ///  calculate a hash for the XElement and then add it to the file 
        /// </summary>
        /// <param name="node">XElement to calculate hash for</param>
        public static void AddMD5Hash(this XElement node)
        {
            string md5 = XmlDoc.CalculateMD5Hash(node);
            node.Add(new XElement("Hash", md5));
        }

        public static void AddMD5Hash(this XElement node, Boolean removePreValIds)
        {
            string md5 = XmlDoc.CalculateMD5Hash(node, removePreValIds);
            node.Add(new XElement("Hash", md5));
        }

        public static void AddMD5Hash(this XElement node, string values)
        {
            string md5 = XmlDoc.CalculateMD5Hash(values);
            node.Add(new XElement("Hash", md5));
        }
    }

    public static class XmlDocumentExtentions
    {

        public static void AddMD5Hash(this XmlDocument node)
        {
            
            string md5 = XmlDoc.CalculateMD5Hash(node);
            var n = node.CreateElement("Hash");
            n.InnerText = md5;
            node.DocumentElement.AppendChild(n);
        }

    }
}
