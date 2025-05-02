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

        public ChangeRequestController( AuthManager authManager,CRBusinessWorkspace crBusinessWorkspace, Node csNode)
        {
            _authManager = authManager;
            _crBusinessWorkspace = crBusinessWorkspace;
            _csNode = csNode;
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
            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error Rejecting Change Request: {ex.Message}");
            }

            ChangeRequestUpdateResponse docs;
            try
            {
                docs = await _crBusinessWorkspace.UpdateChangeRequestDataAsync("BUS2250", crBoId, ticket, updateRequest, "APPROVED");
            }
            catch (Exception ex)
            {
                return BadRequest("Error processing request: " + ex.Message);
            }

            //var response = await _openTextService.UpdateChangeRequestDataAsync(boType, boId, ticket, updateRequest);
            return Ok(docs);
        }

        // POST: /v1/ChangeRequest/{boType}/{boId}
        [HttpPost("{crBoId}/reject/{origBoType}/{origBoId}")]
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
        public async Task<IActionResult> RejectChangeRequest(string crBoId, string origBoType, string origBoId, [FromBody] DTOs.ChangeRequestUpdateRequest updateRequest)
        {
            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error Rejecting Change Request: {ex.Message}");
            }

            ChangeRequestUpdateResponse docs;
            try
            {
                docs = await _crBusinessWorkspace.UpdateChangeRequestDataAsync("BUS2250", crBoId, ticket, updateRequest, "REJECTED");
            }
            catch (Exception ex)
            {
                return BadRequest("Error processing request: " + ex.Message);
            }

            //var response = await _openTextService.UpdateChangeRequestDataAsync(boType, boId, ticket, updateRequest);
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
            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error Getting Change Request Documents:  {ex.Message}");
            }


            MasterDataDocumentsResponse docs;
            try
            {
                docs = await _crBusinessWorkspace.GetDocumentsChangeRequestAsync(boType, boId, ticket);
            } catch (Exception ex)
            {
                return BadRequest("Error processing request: "+ex.Message);
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
            // Get's Ticket from Request
            string ticket = "";
            try
            {
                ticket = _authManager.ExtractTicket(Request);
            }
            catch (Exception ex)
            {
                // Return a 401 status code if auth fails.
                return StatusCode(401, $"Error Creating/Updating Change Request: {ex.Message}");
            }

            ChangeRequestUpdateResponse docs;
            try
            {
                docs = await _crBusinessWorkspace.UpdateChangeRequestDataAsync(boType, boId, ticket, updateRequest);
            }
            catch (Exception ex)
            {
                return BadRequest("Error processing request: " + ex.Message);
            }

            //var response = await _openTextService.UpdateChangeRequestDataAsync(boType, boId, ticket, updateRequest);
            return Ok(docs);
        }
    }





}
