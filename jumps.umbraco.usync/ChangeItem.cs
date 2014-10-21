using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace jumps.umbraco.usync
{
    public class ChangeItem
    {
        public string name { get; set; }
        public int id { get; set; }
        public string file { get; set; }
        public ItemType itemType { get; set; }
        public ChangeType changeType { get; set; }
        public string message { get; set; } 
    }

    public enum ItemType
    {
        DocumentType,
        MediaItem,
        DataType,
        Stylesheet,
        Template,
        Macro,
        Dictionary,
        Languages
    }

    // status - > 10 is an error.
    public enum ChangeType
    {
        Success = 0,
        NoChange,
        Fail = 11,
        ImportFail,
        Mismatch,
        WillChange
    }
}
