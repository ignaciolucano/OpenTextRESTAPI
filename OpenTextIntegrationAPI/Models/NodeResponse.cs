using System.Collections.Generic;

namespace OpenTextIntegrationAPI.Models
{
    public class NodeData
    {
        public bool advanced_versioning { get; set; }
        public bool container { get; set; }
        public int container_size { get; set; }
        public string create_date { get; set; }
        public int create_user_id { get; set; }
        public string description { get; set; }
        public bool hidden { get; set; }
        public string icon { get; set; }
        public string icon_large { get; set; }
        public int id { get; set; }
        public string modify_date { get; set; }
        public int modify_user_id { get; set; }
        public string name { get; set; }
        public int original_id { get; set; }
        public int owner_group_id { get; set; }
        public int owner_user_id { get; set; }
        public int parent_id { get; set; }
        public bool reserved { get; set; }
        public string reserved_date { get; set; }
        public int reserved_user_id { get; set; }
        public bool versionable { get; set; }
        public int volume_id { get; set; }
        // ... add or remove fields as needed
    }

    public class NodeResponse
    {
        // If you need additional arrays like addable_types or available_actions, you can define them here.
        //public NodeData data { get; set; }
        public string file_name { get; set; }
        public int file_size { get; set; }
        public int nodeId { get; set; }
        public int type { get; set; }
        public string type_name { get; set; }
        public string mime_type { get; set; }
        // etc. for other fields you care about
        public byte[]? Content { get; set; }
    }
}
