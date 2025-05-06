namespace OpenTextIntegrationAPI.Models
{
    public class CreateBusinessWorkspaceResponse
    {
        public Links? links { get; set; }
        public ResultsData? results { get; set; }
    }

    public class ResultsData
    {
        public bool direct_open { get; set; }
        public long id { get; set; }
        public int sub_folder_id { get; set; }
    }
}