using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO ;
using System.Xml;
using System.Xml.Linq;

using Umbraco.Core.Logging;

namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    ///  maps the actions (renames, deletes) that can 
    ///  happen so we can keep them across versions
    ///  
    ///  usync.actions.config (in usyncroot)
    /// 
    ///  <usync>
    ///     <deletes>
    ///         <delete guid="342434..." />
    ///     </deletes> 
    ///     <renames>
    ///         <rename guid="1342.." old="fred" new="fred2" />
    ///      </renames>
    /// </usync>
    /// 
    /// these are then performed at the begging of the disk import
    /// 
    /// </summary>
    public class SyncActionLog
    {
        static Dictionary<int, Tuple<string, string>> _renames ;
        static List<int> _deletes ;

        static string _actionLog ;

        static SyncActionLog()
        {
            _actionLog = Path.Combine(
                Umbraco.Core.IO.IOHelper.MapPath(uSyncIO.RootFolder),
                "usync.actions.config");
            Load(); 
        }

        static void Load()
        {
            _renames = new Dictionary<int, Tuple<string, string>>();
            _deletes = new List<int>();

            if (File.Exists(_actionLog))
            {
                XElement source = XElement.Load(_actionLog);

                foreach (XElement rename in source.Descendants("rename"))
                {
                    _renames.Add(
                        int.Parse(rename.Attribute("id").Value),
                        new Tuple<string, string>(
                            rename.Attribute("old").Value,
                            rename.Attribute("new").Value)
                            );
                }

                foreach (XElement delete in source.Descendants("delete"))
                {
                    _deletes.Add(
                        int.Parse(delete.Attribute("id").Value));
                }
            }
        }

        static void Save()
        {
            XmlDocument doc = new XmlDocument();
            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "UTF-8", "no");
            doc.AppendChild(dec);

            XmlElement data = doc.CreateElement("usync");
            XmlElement renames = doc.CreateElement("renames");

            foreach (KeyValuePair<int, Tuple<string, string>> item in _renames)
            {
                XmlElement element = doc.CreateElement("rename");
                element.SetAttribute("id", item.Key.ToString());
                element.SetAttribute("old", item.Value.Item1);
                element.SetAttribute("new", item.Value.Item2);
                renames.AppendChild(element);
            }

            data.AppendChild(renames);

            XmlElement deletes = doc.CreateElement("deletes");

            foreach (int id in _deletes)
            {
                XmlElement element = doc.CreateElement("delete");
                element.SetAttribute("id", id.ToString());
                deletes.AppendChild(element);
            }

            data.AppendChild(deletes);
            doc.AppendChild(data);

            if (File.Exists(_actionLog))
                File.Delete(_actionLog);

            doc.Save(_actionLog);

        }


        internal static void AddRename(int id, string newName, string oldName)
        {
            if (_renames.ContainsKey(id))
                _renames.Remove(id);

            _renames.Add(id,
                new Tuple<string, string>(oldName, newName));

            Save(); 
        }

        internal static void AddDelete(int id)
        {
            if (!_deletes.Contains(id))
                _deletes.Add(id);

            // optimization if it's a delete but in rename - delete it
            // from rename
            if (_renames.ContainsKey(id))
                _renames.Remove(id); 

            Save(); 
        }


        internal static string GetRename(int id)
        {
            if (_renames.ContainsKey(id))
                return _renames[id].Item2;

            else
                return null;
        }

        internal static List<int> GetDeletes()
        {
            return _deletes;
        }

    }
}
