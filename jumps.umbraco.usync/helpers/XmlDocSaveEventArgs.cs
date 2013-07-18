using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace jumps.umbraco.usync.helpers
{
    public class XmlDocSaveEventArgs : EventArgs
    {
        private string _path;

        public XmlDocSaveEventArgs(string path)
        {
            _path = path;
        }

        public string Path
        {
            get { return _path; }
        }

    }
}
