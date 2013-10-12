using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml;
using System.Xml.Linq;

using Umbraco.Core.Logging;

namespace jumps.umbraco.usync.helpers
{
    public class ImportInfo
    {
        static Dictionary<Guid, Guid> _pairs = new Dictionary<Guid, Guid>();
        static string _pairFile;

        static ImportInfo()
        {
            _pairFile = Path.Combine(
                Umbraco.Core.IO.IOHelper.MapPath("~/App_data/Temp/"), "uSync.ImportInfo.xml");
            Load(); 
        }

        public static void Add(Guid master, Guid local)
        {
            if (_pairs.ContainsKey(master))
            {
                _pairs.Remove(master);
            }

            _pairs.Add(master, local);
            Save(); 
        }

        public static void Remove(Guid id)
        {
            if (_pairs.ContainsKey(id))
            {
                _pairs.Remove(id);
                return;
            }

            if (_pairs.ContainsValue(id))
            {
                Guid key = _pairs.FirstOrDefault(x => x.Value == id).Key;
                _pairs.Remove(key);
            }
            Save(); 
        }

        public static void Load()
        {
            LogHelper.Info<ImportInfo>("Load>"); 

            _pairs = new Dictionary<Guid, Guid>();

            if (File.Exists(_pairFile))
            {
                XElement source = XElement.Load(_pairFile);

                foreach (XElement pair in source.Descendants("pair"))
                {
                    _pairs.Add(
                        Guid.Parse(pair.Attribute("master").Value),
                        Guid.Parse(pair.Attribute("local").Value));
                }
            }
            LogHelper.Info<ImportInfo>("<Load"); 

        }

        public static void Save()
        {
            LogHelper.Info<ImportInfo>("Save>"); 

            XmlDocument doc = new XmlDocument();
            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "UTF-8", "no");
            doc.AppendChild(dec);

            XmlElement data = doc.CreateElement("usync.data");
            XmlElement content = doc.CreateElement("content");

            foreach (KeyValuePair<Guid, Guid> pair in _pairs)
            {
                XmlElement p = doc.CreateElement("pair");
                p.SetAttribute("master", pair.Key.ToString());
                p.SetAttribute("local", pair.Value.ToString());

                content.AppendChild(p);
            }

            data.AppendChild(content);
            doc.AppendChild(data);

            if (File.Exists(_pairFile))
                File.Delete(_pairFile);

            doc.Save(_pairFile);
            LogHelper.Info<ImportInfo>("<Save"); 

        }

        public static Guid GetLocalGuid(Guid master)
        {
            if (_pairs.ContainsKey(master))
                return _pairs[master];

            return master;
        }

        public static Guid GetMasterGuid(Guid local)
        {
            if (_pairs.ContainsValue(local))
                return _pairs.FirstOrDefault(x => x.Value == local).Key;

            return local;
        }
    }
}
