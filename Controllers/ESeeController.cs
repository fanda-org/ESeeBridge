using Microsoft.AspNetCore.Mvc;
using EseeBridge.Services;
using EseeBridge.Models;
using Microsoft.AspNetCore.Authorization;

namespace EseeBridge.Controllers;

[Authorize(AuthenticationSchemes = "BasicAuthentication")]
[ApiController]
[Route("api/e-see")]
public class ESeeController(IEseeBluetoothBridgeService bridge) : ControllerBase
{
    private readonly IEseeBluetoothBridgeService _bridge = bridge;

    [HttpGet("info")]
    public IActionResult GetPort()
    {
        return Ok(_bridge.GetInfo());
    }

    [HttpPost("send-receive")]
    public IActionResult SendAndReceive([FromBody] PatientRequest patient, CancellationToken token)
    {
        var result = _bridge.SendAndReceive(patient, token);

        if (result is null)
            return StatusCode(504, "No response received from E-SEE device.");

        return Ok(result);
    }
}