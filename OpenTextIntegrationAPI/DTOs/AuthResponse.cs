using System.Text.Json.Serialization;

namespace OpenTextIntegrationAPI.DTOs
{
    public class AuthResponse
    {
        [JsonPropertyName("ticket")]
        public string Ticket { get; set; }
    }
}
