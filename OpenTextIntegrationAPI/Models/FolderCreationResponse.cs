public class FolderResponse
{
    public class FolderCreationResponse
    {
        public Links links { get; set; }
        public Results results { get; set; }
    }

    public class Links
    {
        public LinkData data { get; set; }
    }

    public class LinkData
    {
        public Self self { get; set; }
    }

    public class Self
    {
        public string body { get; set; }
        public string content_type { get; set; }
        public string href { get; set; }
        public string method { get; set; }
        public string name { get; set; }
    }

    public class Results
    {
        public ResultsData data { get; set; }
    }

    public class ResultsData
    {
        public Properties properties { get; set; }
    }

    public class Properties
    {
        public object advanced_versioning { get; set; }
        public bool container { get; set; }
        public int container_size { get; set; }
        public DateTime create_date { get; set; }
        public int create_user_id { get; set; }
        public string description { get; set; }
        public Multilingual description_multilingual { get; set; }
        public object external_create_date { get; set; }
        public string external_identity { get; set; }
        public string external_identity_type { get; set; }
        public object external_modify_date { get; set; }
        public string external_source { get; set; }
        public bool favorite { get; set; }
        public bool hidden { get; set; }
        public string icon { get; set; }
        public string icon_large { get; set; }
        public int id { get; set; }
        public bool isinecase { get; set; }
        public bool isinefile { get; set; }
        public object mime_type { get; set; }
        public DateTime modify_date { get; set; }
        public int modify_user_id { get; set; }
        public string name { get; set; }
        public Multilingual name_multilingual { get; set; }
        public string owner { get; set; }
        public int owner_group_id { get; set; }
        public int owner_user_id { get; set; }
        public int parent_id { get; set; }
        public string permissions_model { get; set; }
        public bool reserved { get; set; }
        public object reserved_date { get; set; }
        public bool reserved_shared_collaboration { get; set; }
        public int reserved_user_id { get; set; }
        public int size { get; set; }
        public string size_formatted { get; set; }
        public object status { get; set; }
        public int type { get; set; }
        public string type_name { get; set; }
        public bool versionable { get; set; }
        public bool versions_control_advanced { get; set; }
        public int volume_id { get; set; }
        public object wnd_att_2xzx_2 { get; set; }
        public object wnd_att_2xzx_2_formatted { get; set; }
        public object wnf_att_7w0kj_5 { get; set; }
        public object wnf_att_7w0kj_5_formatted { get; set; }
    }

    public class Multilingual
    {
        public string de { get; set; }
        public string en { get; set; }
        public string en_US { get; set; }
        public string es { get; set; }
        public string it { get; set; }
        public string pt { get; set; }
    }
}