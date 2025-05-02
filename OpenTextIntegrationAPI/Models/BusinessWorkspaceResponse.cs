namespace OpenTextIntegrationAPI.Models
{
    public class BusinessWorkspaceResponse
    {
        public Links? links { get; set; }
        public Paging? paging { get; set; }
        public List<BusinessWorkspaceResult> results { get; set; } = new List<BusinessWorkspaceResult>();
        public WkspInfo? wksp_info { get; set; }
    }

    public class Links
    {
        public DataLinks? data { get; set; }
    }

    public class DataLinks
    {
        public SelfLink? self { get; set; }
    }

    public class SelfLink
    {
        public string body { get; set; } = "";
        public string content_type { get; set; } = "";
        public string href { get; set; } = "";
        public string method { get; set; } = "";
        public string name { get; set; } = "";
    }

    public class Paging
    {
        public int limit { get; set; }
        public int page { get; set; }
        public int page_total { get; set; }
        public int range_max { get; set; }
        public int range_min { get; set; }
        public int total_count { get; set; }
    }

    public class BusinessWorkspaceResult
    {
        public BusinessWorkspaceData data { get; set; } = new BusinessWorkspaceData();
        // Puedes agregar la propiedad "actions" si la necesitas
    }

    public class BusinessWorkspaceData
    {
        public BusinessWorkspaceProperties properties { get; set; } = new BusinessWorkspaceProperties();
    }

    public class BusinessWorkspaceProperties
    {
        public bool container { get; set; }
        public string create_date { get; set; } = "";
        public int create_user_id { get; set; }
        public string description { get; set; } = "";
        public bool favorite { get; set; }
        public string icon { get; set; } = "";
        public int id { get; set; }
        public string image_url { get; set; } = "";
        public string modify_date { get; set; } = "";
        public int modify_user_id { get; set; }
        public string name { get; set; } = "";
        public int original_id { get; set; }
        public int parent_id { get; set; }
        public bool perm_add_major_version { get; set; }
        public bool perm_create { get; set; }
        public bool perm_delete { get; set; }
        public bool perm_delete_versions { get; set; }
        public bool perm_modify { get; set; }
        public bool perm_modify_attributes { get; set; }
        public bool perm_modify_permissions { get; set; }
        public bool perm_reserve { get; set; }
        public bool perm_see { get; set; }
        public bool perm_see_contents { get; set; }
        public bool reserved { get; set; }
        public string reserved_date { get; set; } = "";
        public int reserved_user_id { get; set; }
        public int size { get; set; }
        public int type { get; set; }
        public string type_name { get; set; } = "";
        public int user_id { get; set; }
        public int volume_id { get; set; }
    }

    public class WkspInfo
    {
        public string wksp_type_icon { get; set; } = "";
    }
}
