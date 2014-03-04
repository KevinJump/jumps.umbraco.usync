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

    }
}
