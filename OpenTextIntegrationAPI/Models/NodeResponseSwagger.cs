using System.Collections.Generic;

namespace OpenTextIntegrationAPI.Models
{

    public class NodeResponseSwagger
    {
        // If you need additional arrays like addable_types or available_actions, you can define them here.
        public NodeData data { get; set; }
        public int type { get; set; }
        public string type_name { get; set; }
        // etc. for other fields you care about
        public byte[]? Content { get; set; }
    }
}
