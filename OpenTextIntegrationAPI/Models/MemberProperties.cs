// MemberProperties.cs
// DTO for member properties returned by OpenText /v2/members endpoint
// Author: Ignacio Lucano
// Date: 2025-05-14

namespace OpenTextIntegrationAPI.Models
{
    /// <summary>
    /// Represents the core "properties" for a member (user/group/privilege).
    /// Only a subset of all available fields is shown; expand as needed.
    /// </summary>
    public class MemberProperties
    {
        /// <summary>ID of the member.</summary>
        public int Id { get; set; }

        /// <summary>First name of the member.</summary>
        public string First_Name { get; set; }

        /// <summary>Last name of the member.</summary>
        public string Last_Name { get; set; }

        /// <summary>Name of the member.</summary>
        public string name { get; set; }

        /// <summary>Business email address.</summary>
        public string business_email { get; set; }

        /// <summary>Formatted display name.</summary>
        public string Name_Formatted { get; set; }

        /// <summary>URL to the member's photo, if available.</summary>
        public string Photo_Url { get; set; }

        /// <summary>Indicates if the member has system administration privileges.</summary>
        public bool Privilege_System_Admin_Rights { get; set; }

        // TODO: Add or remove additional properties to match your requirements
    }
}
