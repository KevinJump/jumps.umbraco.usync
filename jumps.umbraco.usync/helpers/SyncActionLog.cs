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
        static Dictionary<Guid, Tuple<string, string>> _renames ;
        static List<Guid> _deletes ;

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
            LogHelper.Info<SyncActionLog>("Load > ");
            _renames = new Dictionary<Guid, Tuple<string, string>>();
            _deletes = new List<Guid>();

            if (File.Exists(_actionLog))
            {
                XElement source = XElement.Load(_actionLog);

                foreach (XElement rename in source.Descendants("rename"))
                {
                    _renames.Add(
                        Guid.Parse(rename.Attribute("guid").Value),
                        new Tuple<string, string>(
                            rename.Attribute("old").Value,
                            rename.Attribute("new").Value)
                            );
                }

                foreach (XElement delete in source.Descendants("delete"))
                {
                    _deletes.Add(
                        Guid.Parse(delete.Attribute("guid").Value));
                }
            }
            LogHelper.Info<SyncActionLog>("<Load"); 

        }

        static void Save()
        {
            LogHelper.Info<SyncActionLog>("Save >"); 

            XmlDocument doc = new XmlDocument();
            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "UTF-8", "no");
            doc.AppendChild(dec);

            XmlElement data = doc.CreateElement("usync");
            XmlElement renames = doc.CreateElement("renames");

            foreach (KeyValuePair<Guid, Tuple<string, string>> item in _renames)
            {
                XmlElement element = doc.CreateElement("rename");
                element.SetAttribute("guid", item.Key.ToString());
                element.SetAttribute("old", item.Value.Item1);
                element.SetAttribute("new", item.Value.Item2);
                renames.AppendChild(element);
            }

            data.AppendChild(renames);

            XmlElement deletes = doc.CreateElement("deletes");

            foreach (Guid guid in _deletes)
            {
                XmlElement element = doc.CreateElement("delete");
                element.SetAttribute("guid", guid.ToString());
                deletes.AppendChild(element);
            }

            data.AppendChild(deletes);
            doc.AppendChild(data);

            if (File.Exists(_actionLog))
                File.Delete(_actionLog);

            doc.Save(_actionLog);
            LogHelper.Info<SyncActionLog>("<Save"); 

        }


        internal static void AddRename(Guid guid, string newName, string oldName)
        {
            if (_renames.ContainsKey(guid))
                _renames.Remove(guid);

            _renames.Add(guid,
                new Tuple<string, string>(oldName, newName));

            Save(); 
        }

        internal static void AddDelete(Guid guid)
        {
            if (!_deletes.Contains(guid))
                _deletes.Add(guid);

            // optimization if it's a delete but in rename - delete it
            // from rename
            if (_renames.ContainsKey(guid))
                _renames.Remove(guid); 

            Save(); 
        }


        internal static string GetRename(Guid guid)
        {
            if (_renames.ContainsKey(guid))
                return _renames[guid].Item2;

            else
                return null;
        }

        internal static List<Guid> GetDeletes()
        {
            return _deletes;
        }

    }
}
