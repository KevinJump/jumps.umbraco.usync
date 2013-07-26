using System;

namespace jumps.umbraco.usync.helpers
{
    public class XmlDocFileEventArgs : EventArgs
    {
        private readonly string _path;

        public XmlDocFileEventArgs(string path)
        {
            _path = path;
        }

        public string Path
        {
            get { return _path; }
        }

    }
}
