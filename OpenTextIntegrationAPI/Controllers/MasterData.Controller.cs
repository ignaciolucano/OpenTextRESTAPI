namespace OpenTextIntegrationAPI.Controllers
{
    // Controllers/MasterDataController.cs
    using Microsoft.AspNetCore.Mvc;
    using OpenTextIntegrationAPI.Models;
    using Swashbuckle.AspNetCore.Annotations;
    using OpenTextIntegrationAPI.Services;
    using Swashbuckle.AspNetCore.Filters;
    using OpenTextIntegrationAPI.Utilities;
    using OpenTextIntegrationAPI.ClassObjects;

    [ApiController]
    [Route("v1/[controller]")]
    public class MasterDataController : ControllerBase
    {
        private readonly AuthManager _authManager;
        private readonly MasterData _masterData;

        public MasterDataController(AuthManager authManager,MasterData masterData)
        {
            _authManager = authManager;
            _masterData = masterData;
        }

        // GET: /v1/GetBWDocuments/{boType}/{boId}
        [HttpGet("{boType}/{boId}")]
        [SwaggerResponse(200, "OK", typeof(MasterDataDocumentsResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponseExample(400, typeof(InvalidParameterExample))]
        [SwaggerResponse(404, "Business Workspace Not Found")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Produces("application/json")]
        public async Task<IActionResult> GetMasterDataDocuments(string boType, string boId)
        {
            // Get's Ticket from Request
            string ticket = _authManager.ExtractTicket(Request);

            // Validate boType
            (string validatedBoType, string formattedBoId) = _masterData.ValidateAndFormatBoParams(boType, boId);

            // Validate boId is not empty
            if (string.IsNullOrWhiteSpace(boId))
            {
                return BadRequest("boId cannot be empty.");
            }

            try
            {
                var docs = await _masterData.GetMasterDataDocumentsAsync(boType, boId, ticket);

                if (docs == null || (docs.Files?.Count == 0))
                {
                    return NotFound($"Could not get a node for {boId}");
                }

                return Ok(docs);
            }
            catch (Exception ex)
            {
                // Optionally log ex here
                return StatusCode(500, ex.Message);
            }
        }
    }

}
