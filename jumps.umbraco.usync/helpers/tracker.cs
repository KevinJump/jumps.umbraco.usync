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

using umbraco.cms.businesslogic.web;

using jumps.umbraco.usync.Extensions;
using System.Diagnostics;

namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    /// tracks the updates (where it can) so you can
    /// only run the changes where they might have happened
    /// </summary>
    public static class Tracker
    {
        private static IFileService _fileService;
        private static IContentTypeService _contentService;
        private static IPackagingService _packagingService;
        private static IDataTypeService _dataTypeService;

        private static Dictionary<Guid, IDataTypeDefinition> _dataTypes;
        
        static Tracker()
        {
            _fileService = ApplicationContext.Current.Services.FileService;
            _contentService = ApplicationContext.Current.Services.ContentTypeService;
            _dataTypeService = ApplicationContext.Current.Services.DataTypeService;
            _packagingService = ApplicationContext.Current.Services.PackagingService;
        }

        public static bool ContentTypeChanged(XElement node)
        {
            string filehash = XmlDoc.GetPreCalculatedHash(node);
            if (string.IsNullOrEmpty(filehash))
                return true; 

            XElement aliasElement = node.Element("Info").Element("Alias");
            if (aliasElement == null)
                return true; 
            
            //var _contentService = ApplicationContext.Current.Services.ContentTypeService;
            var item = _contentService.GetContentType(aliasElement.Value);

            if (item == null) // import because it's new. 
                return true; 

            XElement export = item.ExportToXml();
            string dbMD5 = XmlDoc.CalculateMD5Hash(export);

            // XmlDoc.SaveElement("doctmp", item.Alias, export);

            return ( !filehash.Equals(dbMD5)); 
        }

        public static bool DataTypeChanged(XElement node)
        {
            string filehash = XmlDoc.GetPreCalculatedHash(node);
            if (string.IsNullOrEmpty(filehash))
                return true;

            var dataTypeDefinitionId = new Guid(node.Attribute("Definition").Value);

            XAttribute defId = node.Attribute("Definition");
            if (defId == null)
                return true;
            /*
            //var _dataTypeService = ApplicationContext.Current.Services.DataTypeService;
            var item = _dataTypeService.GetDataTypeDefinitionById(new Guid(defId.Value));
            */
            if ( _dataTypes == null )
            {
                // speed test, calling data types seems slow, 
                // so we load all them at once, then refrence this when doing the compares.
                // this is a little bit faster than calling each one as we go through...            
                _dataTypes = new Dictionary<Guid, IDataTypeDefinition>();
                foreach (IDataTypeDefinition dtype in _dataTypeService.GetAllDataTypeDefinitions())
                {
                    _dataTypes.Add(dtype.Key, dtype);
                }

            }

            Guid defGuid = new Guid(defId.Value);
            if (!_dataTypes.ContainsKey(defGuid) )
                return true;

            //var packagingService = ApplicationContext.Current.Services.PackagingService;
            XElement export = _packagingService.Export(_dataTypes[defGuid], false);
            string dbMD5 = XmlDoc.CalculateMD5Hash(export, true);

            // LogHelper.Info<uSync>("XML File (we just got to hash from) {0}", () => export.ToString());
            // LogHelper.Info<uSync>("File {0} : Guid {1}", () => filehash, () => dbMD5);

            return (!filehash.Equals(dbMD5));

        }

        public static bool TemplateChanged(XElement node)
        {
            string filehash = XmlDoc.GetPreCalculatedHash(node);
            if (string.IsNullOrEmpty(filehash))
                return true;

            XElement alias = node.Element("Alias");
            if (alias == null)
                return true;

            //var _fileService = ApplicationContext.Current.Services.FileService;
            var item = _fileService.GetTemplate(alias.Value);
            if (item == null)
                return true;

            // for a template - we never change the contents - lets just md5 the two 
            // properties we care about (and save having to load the thing from disk?

            string values = item.Alias + item.Name;
            string dbMD5 = XmlDoc.CalculateMD5Hash(values);

            return (!filehash.Equals(dbMD5));

        }

        public static bool StylesheetChanges(XmlDocument xDoc)
        {
            XElement node = XElement.Load(new XmlNodeReader(xDoc));

            string filehash = XmlDoc.GetPreCalculatedHash(node);
            if (string.IsNullOrEmpty(filehash))
                return true;

            XElement name = node.Element("Name");
            if (name == null)
                return true;

            var item = StyleSheet.GetByName(name.Value);
            if (item == null)
                return true;

            XmlDocument xmlDoc = helpers.XmlDoc.CreateDoc();
            xmlDoc.AppendChild(item.ToXml(xmlDoc));
            string dbMD5 = XmlDoc.CalculateMD5Hash(xmlDoc);

            return (!filehash.Equals(dbMD5));
        }
    }
}
