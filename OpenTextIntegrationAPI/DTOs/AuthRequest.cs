using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace OpenTextIntegrationAPI.DTOs
{
    public class AuthRequest
    {
        [Required]
        [SwaggerSchema(Description = "The username used to authenticate.")]
        [DefaultValue("integrationRESTAPI")]
        public string Username { get; set; }

        /// Login password
        [Required]
        [SwaggerSchema("The password for the account.")]
        [DefaultValue("Nacho@1280")]
        public string Password { get; set; }

        /// Optional login domain
        [SwaggerSchema("The domain (if required).")]
        [DefaultValue("otds.admin")]
        public string Domain { get; set; }
    }
}
