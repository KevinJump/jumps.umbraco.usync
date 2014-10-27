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

        public static ChangeItem DeleteStub(string name, ItemType type)
        {
            return new ChangeItem
            {
                name = name,
                itemType = type,
                changeType = ChangeType.NoChange,
                message = "Delete"
            };
        }

        public static ChangeItem RenameStub(string name, string newName, ItemType type)
        {
            return new ChangeItem
            {
                name = name,
                itemType = type,
                changeType = ChangeType.NoChange,
                message = string.Format("rename {0} to {1}", name, newName)
            };
        }
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
        Delete,
        NoChange,
        WillChange,
        Fail = 11,
        ImportFail,
        Mismatch,
        RolledBack
    }
}
