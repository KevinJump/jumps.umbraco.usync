using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace jumps.umbraco.usync
{
    public class ChangeItem
    {
        public string name;
        public int id;
        public string file; 
        public ItemType itemType;
        public ChangeType changeType;
        public string message; 
       
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
        Mismatch
    }
}
