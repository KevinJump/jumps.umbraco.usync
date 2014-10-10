using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace jumps.umbraco.usync
{
    public class ChangeItem
    {
        public string name;
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

    public enum ChangeType
    {
        Success,
        Fail,
        NoChange
    }
}
