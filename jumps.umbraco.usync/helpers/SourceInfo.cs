using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Umbraco.Core;

using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    ///  maintains some out of umbraco history, so we can
    ///  trap renames, etc...
    /// </summary>
    public static class SourceInfo
    {
        static Dictionary<Guid, Tuple<string, int>> _source ;
        static string _sourceFile;

        static SourceInfo()
        {
            // _source = new Dictionary<Guid, Tuple<string, int>>();
            _sourceFile = Umbraco.Core.IO.IOHelper.MapPath("~/App_Data/Temp/uSync.SourceInfo.xml");
            Load(); 

        }

        /// <summary>
        /// returns true if the source file isn't there
        /// </summary>
        public static bool IsNew()
        {
            return !File.Exists(_sourceFile);
        }

        public static void Load()
        {
            _source = new Dictionary<Guid, Tuple<string, int>>();

            if (File.Exists(_sourceFile))
            {
                XElement xmlInfo = XElement.Load(_sourceFile);

                foreach (var pair in xmlInfo.Descendants("info"))
                {
                    _source.Add(
                        Guid.Parse(pair.Attribute("guid").Value),
                        new Tuple<string, int>(
                            pair.Attribute("name").Value,
                            int.Parse(pair.Attribute("parent").Value)
                            )
                        );
                }
            }
        }

        public static void Save()
        {
            XmlDocument doc = new XmlDocument();
            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "UTF-8", "no");
            doc.AppendChild(dec);

            XmlElement data = doc.CreateElement("usync.data");
            XmlElement doctypes = doc.CreateElement("doctypes");

            foreach(KeyValuePair<Guid, Tuple<string, int>> info in _source)
            {
                XmlElement element = doc.CreateElement("info");
                element.SetAttribute("guid", info.Key.ToString());
                element.SetAttribute("name", info.Value.Item1);
                element.SetAttribute("parent", info.Value.Item2.ToString());

                doctypes.AppendChild(element);
            }

            data.AppendChild(doctypes);
            doc.AppendChild(data);

            if (File.Exists(_sourceFile))
                File.Delete(_sourceFile);

            doc.Save(_sourceFile);

        }

        public static void Add(Guid guid, string name, int parentId)
        {
            if (_source.ContainsKey(guid))
                _source.Remove(guid);

            _source.Add(guid, new Tuple<string, int>(name, parentId));
        }

        public static void Remove(Guid guid)
        {
            if (_source.ContainsKey(guid))
                _source.Remove(guid);
            
        }

        public static string GetName(Guid guid)
        {
            if (_source.ContainsKey(guid))
                return _source[guid].Item1;
            else
                return null;

        }

        public static int? GetParent(Guid guid)
        {
            if (_source.ContainsKey(guid))
                return _source[guid].Item2;
            else
                return null;
        }
    }
}
