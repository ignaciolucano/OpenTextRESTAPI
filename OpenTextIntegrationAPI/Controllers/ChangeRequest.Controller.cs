namespace OpenTextIntegrationAPI.Controllers
{
    // Controllers/ChangeRequestController.cs
    using Microsoft.AspNetCore.Mvc;
    using OpenTextIntegrationAPI.ClassObjects;
    using OpenTextIntegrationAPI.Models;
    using OpenTextIntegrationAPI.Services;
    using OpenTextIntegrationAPI.Utilities;
    using Swashbuckle.AspNetCore.Annotations;
    using Swashbuckle.AspNetCore.Filters;
    using System.Net.Sockets;

    [ApiController]
    [Route("v1/[controller]")]
    public class ChangeRequestController : ControllerBase
    {
        private readonly AuthManager _authManager;
        private readonly CRBusinessWorkspace _crBusinessWorkspace;
        private readonly Node _csNode;
        private readonly ILogService _logger;

        public ChangeRequestController(AuthManager authManager, CRBusinessWorkspace crBusinessWorkspace, Node csNode, ILogService logger)
        {
            _authManager = authManager;
            _crBusinessWorkspace = crBusinessWorkspace;
            _csNode = csNode;
            _logger = logger;
        }

        // POST: /v1/ChangeRequest/{boType}/{boId}
        [HttpPost("{crBoId}/approve/{origBoType}/{origBoId}")]
        [SwaggerOperation(
            Summary = "",
            Description = ""
        )]
        [SwaggerResponse(200, "OK", typeof(MasterDataDocumentsResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponseExample(400, typeof(InvalidParameterExample))]
        [SwaggerResponse(404, "Business Workspace Not Found")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Produces("application/json")]
        public async Task<IActionResult> ApproveChangeRequest(string crBoId, string origBoType, string origBoId, [FromBody] DTOs.ChangeRequestUpdateRequest updateRequest)
        {
            // Log inbound request
            //_logger.LogRawInbound("inbound_request_approve_change_request",
            //    System.Text.Json.JsonSerializer.Serialize(new
            //    {
            //        crBoId,
            //        origBoType,
            //        origBoId,
            //        source_ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            //         timestamp = DateTime.UtcNow
            //    })
            //);

            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Log error response
                _logger.LogRawInbound("response_approve_change_request_auth_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        timestamp = DateTime.UtcNow
                    })
                );

                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error Approving Change Request: {ex.Message}");
            }

            ChangeRequestUpdateResponse docs;
            try
            {
                docs = await _crBusinessWorkspace.UpdateChangeRequestDataAsync("BUS2250", crBoId, ticket, updateRequest, "APPROVED");

                // Log successful response
                _logger.LogRawOutbound("response_approve_change_request_success",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "success",
                        message = docs.Message,
                        timestamp = DateTime.UtcNow
                    })
                );
            }
            catch (Exception ex)
            {
                // Log error response
                _logger.LogRawInbound("response_approve_change_request_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        timestamp = DateTime.UtcNow
                    })
                );

                return BadRequest("Error processing request: " + ex.Message);
            }

            return Ok(docs);
        }

        // POST: /v1/ChangeRequest/{crBoId}/reject
        [HttpPost("{crBoId}/reject/")]
        [SwaggerOperation(
            Summary = "",
            Description = ""
        )]
        [SwaggerResponse(200, "OK", typeof(MasterDataDocumentsResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponseExample(400, typeof(InvalidParameterExample))]
        [SwaggerResponse(404, "Business Workspace Not Found")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Produces("application/json")]
        public async Task<IActionResult> RejectChangeRequest(string crBoId, [FromBody] DTOs.ChangeRequestUpdateRequest updateRequest)
        {
            // Log inbound request
            _logger.LogRawOutbound("request_reject_change_request",
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    crBoId,
                    source_ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    timestamp = DateTime.UtcNow
                })
            );

            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Log error response
                _logger.LogRawInbound("response_reject_change_request_auth_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        timestamp = DateTime.UtcNow
                    })
                );

                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error Rejecting Change Request: {ex.Message}");
            }

            ChangeRequestUpdateResponse docs;
            try
            {
                docs = await _crBusinessWorkspace.UpdateChangeRequestDataAsync("BUS2250", crBoId, ticket, updateRequest, "REJECTED");

                // Log successful response
                _logger.LogRawOutbound("response_reject_change_request_success",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "success",
                        message = docs.Message,
                        timestamp = DateTime.UtcNow
                    })
                );
            }
            catch (Exception ex)
            {
                // Log error response
                _logger.LogRawInbound("response_reject_change_request_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        timestamp = DateTime.UtcNow
                    })
                );

                return BadRequest("Error processing request: " + ex.Message);
            }

            return Ok(docs);
        }

        // GET: /v1/CRDocuments/{boType}/{boId}
        [HttpGet("{boType}/{boId}")]
        [SwaggerOperation(
            Summary = "",
            Description = ""
        )]
        [SwaggerResponse(200, "OK", typeof(MasterDataDocumentsResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponseExample(400, typeof(InvalidParameterExample))]
        [SwaggerResponse(404, "Business Workspace Not Found")]
        [SwaggerResponse(500, "Internal Error. Contact API Admin")]
        [Produces("application/json")]
        public async Task<IActionResult> GetDocumentsChangeRequest(string boType, string boId)
        {
            // Log inbound request
            //_logger.LogRawInbound("inbound_request_get_change_request_documents",
            //    System.Text.Json.JsonSerializer.Serialize(new
            //    {
            //        boType,
            //        boId,
            //        source_ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            //        timestamp = DateTime.UtcNow
            //    })
            //);

            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Log error response
                //_logger.LogRawOutbound("response_get_change_request_documents_auth_error",
                //System.Text.Json.JsonSerializer.Serialize(new
                //{
                //status = "error",
                //error_message = ex.Message,
                //timestamp = DateTime.UtcNow
                //})
                //);

                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error Getting Change Request Documents: {ex.Message}");
            }

            ChangeRequestDocumentsResponse docs;
            try
            {
                docs = await _crBusinessWorkspace.GetDocumentsChangeRequestAsync(boType, boId, ticket);

                // Log successful response
                _logger.LogRawOutbound("response_get_change_request_documents_success",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "success",
                        boType = docs.Header.BoType,
                        boId = docs.Header.BoId,
                        file_count = docs.Files?.Count ?? 0,
                        timestamp = DateTime.UtcNow
                    })
                );
            }
            catch (Exception ex)
            {
                // Log error response
                _logger.LogRawInbound("response_get_change_request_documents_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        timestamp = DateTime.UtcNow
                    })
                );

                return BadRequest("Error processing request: " + ex.Message);
            }

            return Ok(docs);
        }

        // PUT: /v1/ChangeRequest/{boType}/{boId}/Update
        [HttpPut("{boType}/{boId}/Update")]
        [SwaggerOperation(
            Summary = "",
            Description = ""
        )]
        [SwaggerResponse(200, "Change Request data updated successfully", typeof(ChangeRequestUpdateResponse))]
        [SwaggerResponse(400, "Invalid parameter value")]
        [SwaggerResponse(401, "Unauthorized. Authentication required.")]
        [SwaggerResponse(500, "Internal server error. Please contact the API administrator.")]
        [Produces("application/json")]
        public async Task<IActionResult> UpdateChangeRequestData(
            string boType,
            string boId,
            //[FromHeader] string authorization,
            [FromBody] DTOs.ChangeRequestUpdateRequest updateRequest)
        {
            // Log inbound request
            //_logger.LogRawInbound("inbound_request_update_change_request",
            //    System.Text.Json.JsonSerializer.Serialize(new
            //    {
            //        boType,
            //        boId,
            //        source_ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            //        timestamp = DateTime.UtcNow
            //    })
            //);

            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Log error response
                _logger.LogRawInbound("response_update_change_request_auth_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        timestamp = DateTime.UtcNow
                    })
                );

                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error Creating/Updating Change Request: {ex.Message}");
            }

            ChangeRequestUpdateResponse docs;
            try
            {
                docs = await _crBusinessWorkspace.UpdateChangeRequestDataAsync(boType, boId, ticket, updateRequest);

                // Log successful response
                _logger.LogRawOutbound("response_update_change_request_success",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "success",
                        message = docs.Message,
                        timestamp = DateTime.UtcNow
                    })
                );
            }
            catch (Exception ex)
            {
                // Log error response
                _logger.LogRawInbound("response_update_change_request_error",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = "error",
                        error_message = ex.Message,
                        timestamp = DateTime.UtcNow
                    })
                );

                return BadRequest("Error processing request: " + ex.Message);
            }

            return Ok(docs);
        }
    }
}