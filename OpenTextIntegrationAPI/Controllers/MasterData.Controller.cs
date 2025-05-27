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

    /// <summary>
    /// Controller that handles MasterData operations with OpenText Content Server.
    /// Provides endpoints for retrieving master data documents associated with business objects.
    /// </summary>
    [ApiController]
    [Route("v1/[controller]")]
    public class MasterDataController : ControllerBase
    {
        private readonly AuthManager _authManager;
        private readonly MasterData _masterData;
        private readonly ILogService _logger;

        /// <summary>
        /// Initializes a new instance of the MasterDataController with required dependencies.
        /// </summary>
        /// <param name="authManager">Service for authentication management</param>
        /// <param name="masterData">Service for master data operations</param>
        /// <param name="logger">Service for logging operations and errors</param>
        public MasterDataController(AuthManager authManager, MasterData masterData, ILogService logger)
        {
            _authManager = authManager;
            _masterData = masterData;
            _logger = logger;

            // Log controller initialization
            _logger.Log("MasterDataController initialized", LogLevel.DEBUG);
        }

        /// <summary>
        /// Retrieves master data documents associated with a business object.
        /// </summary>
        /// <param name="boType">Business Object Type (e.g., BUS1001006, BUS1001001, BUS1006)</param>
        /// <param name="boId">Business Object ID</param>
        /// <returns>HTTP response with master data documents information</returns>
        [HttpGet("{boType}/{boId}")]
        [SwaggerOperation(
            Summary = "Get master data documents for a business object",
            Description = "Retrieves a list of all master data documents stored in the business workspace associated with the specified business object"
        )]
        [SwaggerResponse(200, "OK", typeof(MasterDataDocumentsResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponseExample(400, typeof(InvalidParameterExample))]
        [SwaggerResponse(404, "Business Workspace Not Found")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Produces("application/json")]
        public async Task<IActionResult> GetMasterDataDocuments(string boType, string boId)
        {
            _logger.Log($"GetMasterDataDocuments called for BO: {boType}/{boId}", LogLevel.INFO);

            try
            {
                // Get ticket from Request
                _logger.Log("Extracting authentication ticket from request", LogLevel.DEBUG);
                string ticket = _authManager.ExtractTicket(Request);

                // Log successful ticket extraction (with masked ticket)
                if (ticket != null && ticket.Length > 12)
                {
                    string maskedTicket = ticket.Substring(0, 8) + "..." + ticket.Substring(ticket.Length - 4);
                    _logger.Log($"Authentication ticket extracted: {maskedTicket}", LogLevel.DEBUG);
                }

                // Validate and format business object parameters
                _logger.Log($"Validating and formatting BO parameters: {boType}/{boId}", LogLevel.DEBUG);
                (string validatedBoType, string formattedBoId) = _masterData.ValidateAndFormatBoParams(boType, boId);
                _logger.Log($"Validated BO parameters: {validatedBoType}/{formattedBoId}", LogLevel.DEBUG);

                // Validate boId is not empty
                if (string.IsNullOrWhiteSpace(boId))
                {
                    _logger.Log("Validation failed: boId cannot be empty", LogLevel.WARNING);
                    return BadRequest("boId cannot be empty.");
                }

                // Log request details
                _logger.LogRawOutbound("request_get_masterdata",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        boType = validatedBoType,
                        boId = formattedBoId
                    })
                );

                // Retrieve master data documents
                _logger.Log($"Calling GetMasterDataDocumentsAsync for {validatedBoType}/{formattedBoId}", LogLevel.DEBUG);
                var docs = await _masterData.GetMasterDataDocumentsAsync(validatedBoType, formattedBoId, ticket);

                // Check if documents were found
                //if (docs == null || (docs.Files?.Count == 0))
                //{
                //    _logger.Log($"No documents found for {boType}/{boId}", LogLevel.WARNING);

                    // Log response for not found case
                //    _logger.LogRawInbound("response_get_masterdata_notfound",
                //        System.Text.Json.JsonSerializer.Serialize(new
                //        {
                //            status = "not_found",
                //            message = $"Could not get a node for {boId}"
                //        })
                //    );

                //    return NotFound($"Could not get a node for {boId}");
                //}

                // Log successful retrieval
                _logger.Log($"Successfully retrieved {docs.Files?.Count ?? 0} master data documents for {boType}/{boId}", LogLevel.INFO);

                // Log response details (with file names but not full content)
                _logger.LogRawOutbound("response_get_masterdata",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        header = docs.Header,
                        fileCount = docs.Files?.Count ?? 0,
                        fileNames = docs.Files?.Select(f => f.Name).ToList()
                    })
                );

                return Ok(docs);
            }
            catch (Exception ex)
            {
                // Log exception details
                _logger.LogException(ex, LogLevel.ERROR);
                _logger.Log($"Error retrieving master data documents: {ex.Message}", LogLevel.ERROR);

                // Determine if it's an authentication error or other error
                if (ex.Message.Contains("ticket") || ex.Message.Contains("auth"))
                {
                    _logger.Log("Authentication error detected", LogLevel.ERROR);
                    return StatusCode(401, $"Authentication error: {ex.Message}");
                }

                // Log error response
                _logger.LogRawInbound("response_get_masterdata_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = ex.Message,
                        stack_trace = ex.StackTrace
                    })
                );

                return StatusCode(500, ex.Message);
            }
        }
    }
}