﻿using System;
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
        private static IDataTypeService _dataTypeService;
        static Tracker()
        {
            _fileService = ApplicationContext.Current.Services.FileService;
            _contentService = ApplicationContext.Current.Services.ContentTypeService;
            _dataTypeService = ApplicationContext.Current.Services.DataTypeService;
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

            return (!filehash.Equals(dbMD5));
        }

        public static bool DataTypeChanged(XmlDocument xDoc)
        {
            return true;
            /*
             * for umbraco 6.2 there is little cost, so we always sync the doctypes 
             */
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