using System;

namespace OpenTextIntegrationAPI.DTOs
{
    /// <summary>
    /// Contains all optional update parameters for a Change Request.
    /// </summary>
    public class ChangeRequestUpdateRequest
    {
        // Header properties (Mandatory)
        public string ChangeRequestName { get; set; } // max length 150

        // Related BO properties (all optional)
        public string? MainBOId { get; set; } // max length 150
        public string? MainBOType { get; set; } // max length 150


        // Main properties (all optional)
        public string? Template { get; set; } // max length 150
        public string? ObjectID { get; set; } // max length 100
        public string? ERP { get; set; } // max length 1000
        public string? Status { get; set; } // max length 30
        public string? CreatedBy { get; set; } // max length 100
        public string? CreatedAt { get; set; }
        public string? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; } // max length 100
        public string? ApprovalVersion { get; set; }
        public string? EndTime { get; set; }

        // Template Detail
        public string? RequestType { get; set; } // max length 30
        public string? ObjectType { get; set; } // max length 100
    }
}
