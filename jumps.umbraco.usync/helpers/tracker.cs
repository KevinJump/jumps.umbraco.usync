using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Core.Models;

using System.IO;

using System.Xml;
using System.Xml.Linq;

using Umbraco.Core.Logging;
using System.Security.Cryptography; 


using jumps.umbraco.usync.Extensions;

namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    /// tracks the updates (where it can) so you can
    /// only run the changes where they might have happened
    /// </summary>
    public static class Tracker
    {
        public static bool ContentTypeChanged(XElement node)
        {
            string filehash = XmlDoc.GetPreCalculatedHash(node);
            if (string.IsNullOrEmpty(filehash))
                return true; 

            XElement aliasElement = node.Element("Info").Element("Alias");
            if (aliasElement == null)
                return true; 
            
            var _contentService = ApplicationContext.Current.Services.ContentTypeService;
            var item = _contentService.GetContentType(aliasElement.Value);
            if (item == null) // import because it's new. 
                return true; 

            XElement export = item.ExportToXml();
            string dbMD5 = XmlDoc.CalculateMD5Hash(export);

            return ( !filehash.Equals(dbMD5)); 
        }

    }
}
