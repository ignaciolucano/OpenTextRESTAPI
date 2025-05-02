// Models/NodesAllResponse.cs
using System.Collections.Generic;

namespace OpenTextIntegrationAPI.Models
{
    public class NodeProperty
    {
        public int id { get; set; }
        public int parent_id { get; set; }
        public string name { get; set; }
        public int type { get; set; }
        public string type_name { get; set; }
        public string description { get; set; }
        public string create_date { get; set; }
        public int create_user_id { get; set; }
        public string modify_date { get; set; }
        public int modify_user_id { get; set; }
        public bool reserved { get; set; }
        public int reserved_user_id { get; set; }
        public string reserved_date { get; set; }
        public int order { get; set; }
        public string icon { get; set; }
        public bool hidden { get; set; }
        public int size { get; set; }
        public string mime_type { get; set; }
        public int original_id { get; set; }
    }

    public class DataItem
    {
        public List<NodeProperty> properties { get; set; }
    }

    public class ResultsItem
    {
        public List<DataItem> data { get; set; }
    }

    public class NodesAllResponse
    {
        public List<ResultsItem> results { get; set; }
    }
}
