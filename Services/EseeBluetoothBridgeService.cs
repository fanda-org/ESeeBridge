using EseeBridge.Models;

using InTheHand.Net.Bluetooth;
using InTheHand.Net.Bluetooth.Sdp;
using InTheHand.Net.Sockets;

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EseeBridge.Services;

public interface IEseeBluetoothBridgeService
{
    BridgeInfo GetInfo();

    PatientResult? SendAndReceive(PatientRequest patient, CancellationToken token);

    void StopListener();
}

public class EseeBluetoothBridgeService(ILogger<EseeBluetoothBridgeService> logger) : IEseeBluetoothBridgeService, IDisposable
{
    private BluetoothListener _listener = default!;
    private bool _isRunning = false;

    private readonly ILogger<EseeBluetoothBridgeService> _logger = logger;
    private readonly TimeSpan _readTimeout = TimeSpan.FromMinutes(10);

    public BridgeInfo GetInfo()
    {
        StartListener();
        // To get the dynamically assigned RFCOMM channel number (port)
        // The ServiceRecord property is populated after Start() is called
        ServiceRecord serviceRecord = _listener.ServiceRecord;

        // Use ServiceRecordHelper to extract the channel number
        int port = ServiceRecordHelper.GetRfcommChannelNumber(serviceRecord);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Bluetooth listener is using RFCOMM channel (port): {Port}", port);

        IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress? ipAddress = ipHostInfo.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

        return new BridgeInfo(
            MachineName: Environment.MachineName,
            IPAddress: ipAddress?.ToString(),
            BridgeServicePort: 5200,
            ListenerPort: port);
    }

    public void StartListener()
    {
        if (!_isRunning)
        {
            // Create Bluetooth listener
            var serviceGuid = BluetoothService.SerialPort;
            _listener = new BluetoothListener(serviceGuid)
            {
                ServiceName = "E-SEE Bluetooth Service"
            };
            _listener.Start();
            _isRunning = true;

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Bluetooth listener started on service GUID: {Guid}", serviceGuid);
        }
    }

    public PatientResult? SendAndReceive(PatientRequest patient, CancellationToken token)
    {
        try
        {
            StartListener();
            WritePatinetInfo(patient.Id, token);
            var result = ReadPatientResult(token);
            return result;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
                _logger.LogError(ex, "Error during Bluetooth communication with E-SEE device.");
            return null;
        }
    }

    public void StopListener()
    {
        _isRunning = false;
        _listener.Stop();
    }

    private void WritePatinetInfo(string patientId, CancellationToken token)
    {
        var start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start) < _readTimeout && !token.IsCancellationRequested && _isRunning)
        {
            if (_listener.Pending())
            {
                using var client = _listener.AcceptBluetoothClient();
                if (client is not null)
                {
                    using Stream stream = client.GetStream();
                    if (stream is not null && stream.CanWrite)
                    {
                        byte[] data = Encoding.ASCII.GetBytes(patientId);
                        stream.Write(data, 0, data.Length);
                        stream.Flush();
                        if (_logger.IsEnabled(LogLevel.Information))
                            _logger.LogInformation("Sent patient info: id={patientId}", patientId);
                        break;
                    }
                }
            }
            else
            {
                Task.Delay(500, token).Wait(token);
            }
        }
    }

    private PatientResult ReadPatientResult(CancellationToken token)
    {
        var sb = new StringBuilder();
        var buffer = new byte[1024];
        var start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start) < _readTimeout && !token.IsCancellationRequested && _isRunning)
        {
            if (_listener.Pending())
            {
                using var client = _listener.AcceptBluetoothClient();
                if (client is not null)
                {
                    using Stream stream = client.GetStream();
                    if (stream is not null && stream.CanRead && stream is NetworkStream ns && ns.DataAvailable)
                    {
                        int bytes = stream.Read(buffer, 0, buffer.Length);
                        if (bytes > 0)
                        {
                            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytes));
                            break;
                        }
                    }
                }
            }
            else
            {
                Task.Delay(500, token).Wait(token);
            }
        }

        string result = sb.ToString().Trim();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Received data from E-SEE device: {Result}", result);

        return TestResultParser.ParseResult(result);
    }

    public void Dispose()
    {
        StopListener();
        GC.SuppressFinalize(this);
    }
}