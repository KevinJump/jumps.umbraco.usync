using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;

using Umbraco.Core;
using Umbraco.Core.Logging;

namespace jumps.umbraco.usync.helpers
{
    /// <summary>
    ///  works out just what the diffrences between a usync file and 
    ///  the umbraco bits are... used for the diffrence reports
    ///  
    ///  Will form the backbone of reporting, when you just want to 
    ///  see changes, but not make them...
    /// </summary>
    public class DiffrenceEngine
    {
        private List<Difference> _changeSet;

        public DiffrenceEngine()
        {
            _changeSet = new List<Difference>();
        }

        /// <summary>
        ///  chuck everything into the log. 
        /// </summary>
        public void LogChanges()
        {
            LogHelper.Info<DiffrenceEngine>("Logging Changes");
            LogHelper.Info<DiffrenceEngine>("Processed {0} Changes found", () => _changeSet.Count());

            foreach(var diffrence in _changeSet)
            {
                LogHelper.Info<DiffrenceEngine>("DE: {0} {1} {2}", () => diffrence.Name, ()=> diffrence.changeType, ()=> diffrence.changes.Count());

                foreach(var change in diffrence.changes)
                {
                    LogHelper.Info<DiffrenceEngine>("DE: {0} Change {1} {2} : {3} -> {4}",
                        () => diffrence.Name, () => change.Name, () => change.changeType, () => change.Before, () => change.After);
                }

            }
        }


        public void LogChange(XElement before, XElement after)
        {
            var diffrence = new Difference();
            diffrence.UmbracoType = after.Name.LocalName; 

            switch( after.Name.LocalName )
            {
                case "DataType":                
                    LogDataTypeChange(diffrence, before, after);
                    break;
                case "MediaType":
                    break;
                case "DocumentType":
                    break;
                case "Language":
                    break;
                case "Dictionary":
                    break;
                case "Template":
                    break;
                case "Stylesheet":
                    break; 
            }
        }

        /// <summary>
        ///  passed the xml export for both current (before)
        ///  and our usync file (after). 
        /// </summary>
        /// <param name="before"></param>
        /// <param name="after"></param>
        public void LogDataTypeChange(Difference diffrence, XElement before, XElement after)
        {
            var name = after.Attribute("Name").Value;

            if (before == null)
            {
                diffrence.changeType = DiffrenceType.New;
            }

            foreach (var attrib in after.Attributes())
            {
                if (before.Attribute(attrib.Name.LocalName) != null)
                {
                    // exists is it the same...
                    if (before.Attribute(attrib.Name.LocalName).Value != attrib.Value)
                    {
                        // change
                        diffrence.changeType = DiffrenceType.Change;
                        diffrence.changes.Add(
                            new DiffrenceDetail
                            {
                                Name = diffrence.Name,
                                changeType = DiffrenceType.Change,
                                PropName = attrib.Name.LocalName,
                                Before = before.Attribute(attrib.Name.LocalName).Value,
                                After = attrib.Value
                            });
                    }
                }
                else
                {
                    diffrence.changeType = DiffrenceType.Change;
                    // doesn't exist so it's an add 
                    diffrence.changes.Add(
                        new DiffrenceDetail
                        {
                            Name = diffrence.Name,
                            changeType = DiffrenceType.New,
                            PropName = attrib.Name.LocalName,
                            After = attrib.Value,
                            Before = ""
                        });
                }
            }
            _changeSet.Add(diffrence);
        }
                
    }


    public class Difference
    {
        public string UmbracoType;
        public string Name;
        public DiffrenceType changeType;
        public List<DiffrenceDetail> changes;

        public Difference()
        {
            changeType = DiffrenceType.None;
            changes = new List<DiffrenceDetail>(); 
        }
    }

    public class DiffrenceDetail
    {
        public DiffrenceType changeType;
        public string Name;
        public string PropName;
        public string Before;
        public string After;
    }

    public enum DiffrenceType
    {
        None,
        New,
        Change,
        Delete
    }
}
