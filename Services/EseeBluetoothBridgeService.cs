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
                            //if (sb.ToString().EndsWith("\n") || sb.ToString().EndsWith("END"))
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

    //public static PatientResult ParseResult(string resultData)
    //{
    //    // Implement parsing logic based on expected result format
    //    var result = new PatientResult();

    //    // Split by various common newline sequences and remove empty entries
    //    string[] lines = resultData.Split([Environment.NewLine, "\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries);

    //    int lineNumber = 1;
    //    bool rightEye = false;
    //    bool leftEye = false;
    //    foreach (string line in lines)
    //    {
    //        // Perform operations on each 'line' here
    //        if (!rightEye && !leftEye && line.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
    //            result.PatientId = line.Substring(3).Trim();
    //        else if (!rightEye && !leftEye && lineNumber == 2 && DateTime.TryParse(line, out DateTime diagDate))
    //            result.DiagDate = diagDate;
    //        else if (!rightEye && !leftEye && line.StartsWith("OD / Right"))
    //            rightEye = true;
    //        else if (rightEye && !line.StartsWith("Test") && !line.StartsWith('-'))
    //        {
    //            if (line.StartsWith("OS / Left"))
    //            {
    //                rightEye = false;
    //                leftEye = true;
    //            }
    //            else
    //            {
    //                string[] data = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    //                if (data.Length == 5 && int.TryParse(data[0], out int testNum) && testNum > 0)
    //                {
    //                    result.RightEyeResult.TestNumber = data[0];
    //                    result.RightEyeResult.Sphere = data[1];
    //                    result.RightEyeResult.Cylinder = data[2];
    //                    result.RightEyeResult.Axis = data[3];
    //                    result.RightEyeResult.SphericalEquivalent = data[4];
    //                }
    //            }
    //        }
    //        else if (rightEye && line.StartsWith("OS / Left"))
    //        {
    //            rightEye = false;
    //            leftEye = true;
    //        }
    //        else if (leftEye && !line.StartsWith("Test") && !line.StartsWith('-') && !line.StartsWith("Aurolab") && !line.StartsWith("S/N:"))
    //        {
    //            string[] data = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    //            if (data.Length == 5 && int.TryParse(data[0], out int testNum) && testNum > 0)
    //            {
    //                result.LeftEyeResult.TestNumber = data[0];
    //                result.LeftEyeResult.Sphere = data[1];
    //                result.LeftEyeResult.Cylinder = data[2];
    //                result.LeftEyeResult.Axis = data[3];
    //                result.LeftEyeResult.SphericalEquivalent = data[4];
    //            }
    //        }
    //        else if (leftEye && lineNumber > 9 && line.StartsWith("Aurolab"))
    //            result.DeviceName = line.Trim();
    //        else if (leftEye && line.StartsWith("S/N:"))
    //        {
    //            string[] data = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    //            result.DeviceSerialNumber = data[1];
    //        }

    //        lineNumber++;
    //    }

    //    return result;
    //}

    //private static string DeserializeData(eSeePatientData eSeePatientData)
    //{
    //    var eSeeParsedData = new eSeePatientData();
    //    eSeeParsedData.RightEye = new List<EyeAttributes>();
    //    eSeeParsedData.LeftEye = new List<EyeAttributes>();

    //    foreach (var det in eSeePatientData.RightEye)
    //    {
    //        eSeeParsedData.RightEye.Add(det);
    //    }
    //    foreach (var det in eSeePatientData.LeftEye)
    //    {
    //        eSeeParsedData.LeftEye.Add(det);
    //    }

    //    eSeeParsedData.patientId = eSeePatientData.patientId;
    //    eSeeParsedData.StudyDatetime = eSeePatientData.StudyDatetime;
    //    JObject jObj = JObject.FromObject(eSeeParsedData);
    //    return jObj.ToString();
    //}

    /// <summary>
    /// Main entry — send patient info, wait for refraction result.
    /// Auto-discovers and remembers E-SEE device.
    /// </summary>
    //public async Task<string> SendPatientInfoAndGetResultsAsync(PatientRequest patient, CancellationToken cancellationToken)
    //{
    //    string deviceAddress = string.Empty; //_config[_configKey]!;
    //                                         //BluetoothAddress? address = null;
    //    BluetoothAddress? address = null; //BluetoothAddress.Parse("B8:27:EB:F9:3D:8F");

    //    // Step 1: Find or remember E-SEE device
    //    ///if (string.IsNullOrWhiteSpace(deviceAddress))
    //    ///{
    //    _logger.LogInformation("🔍 No stored device address, scanning for E-SEE...");
    //    address = await DiscoverEseeDeviceAsync(cancellationToken);

    //    /// client.Connect(address, BluetoothService.SerialPort);

    //    if (address == null)
    //    {
    //        _logger.LogError("❌ No E-SEE device found during discovery.");
    //        return string.Empty;
    //    }

    //    // Save discovered address(in -memory or file)
    //    /// SaveDeviceAddress(address.ToString());
    //    /// _logger.LogInformation("💾 E-SEE device saved for future use: {Address}", address);
    //    ///}
    //    ///else
    //    ///{
    //    ///    _logger.LogInformation("📡 Using stored E-SEE address: {Address}", deviceAddress);
    //    ///    address = BluetoothAddress.Parse(deviceAddress);
    //    ///}

    //    // Step 2: Attempt connection and communication
    //    for (int attempt = 1; attempt <= _maxRetries; attempt++)
    //    {
    //        try
    //        {
    //            using var client = new BluetoothClient();

    //            if (_logger.IsEnabled(LogLevel.Information))
    //                _logger.LogInformation("🔗 Attempt {Attempt}/{Max} connecting to {Address}", attempt, _maxRetries, address);

    //            await Task.Run(() => client.Connect(address, _serviceGuid), cancellationToken);

    //            using Stream stream = client.GetStream();
    //            stream.ReadTimeout = (int)_readTimeout.TotalMilliseconds;

    //            //string patientInfo = $"\x02{patient.PatientId}\t{patient.Age}\t{patient.Gender}\x03"; // "\x02" + patientInfo + "\x03"; // Wrap with STX/ETX
    //            string patientInfo = "10123456";

    //            _logger.LogInformation("✅ Connected to E-SEE. Sending patient info...");
    //            byte[] data = Encoding.ASCII.GetBytes(patientInfo);
    //            await stream.WriteAsync(data, 0, data.Length, cancellationToken);
    //            await stream.FlushAsync(cancellationToken);

    //            //using (var stream = client.GetStream())
    //            //{
    //            //    var message = $"\x02{patientId}\t{patientName}\t{age}\t{gender}\x03";
    //            //    var data = Encoding.ASCII.GetBytes(message);
    //            //    await stream.WriteAsync(data, 0, data.Length);
    //            //    await stream.FlushAsync();
    //            //    await Task.Delay(500);
    //            //}

    //            _logger.LogInformation("📤 Patient info sent. Waiting for diagnosis result...");

    //            string result = await ReadResponseAsync(stream, cancellationToken);

    //            if (!string.IsNullOrWhiteSpace(result))
    //            {
    //                _logger.LogInformation("📥 Received data from E-SEE device.");
    //                return result;
    //            }
    //            else
    //            {
    //                _logger.LogWarning("⚠️ No data received from device (timeout or empty).");
    //            }
    //        }
    //        catch (IOException ioEx)
    //        {
    //            _logger.LogWarning(ioEx, "⚡ Connection lost, retrying...");
    //            //await Task.Delay(_connectRetryDelay, cancellationToken);
    //            await TryReconnectAsync(address, cancellationToken);
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, "❌ Error communicating with E-SEE.");
    //            await Task.Delay(_connectRetryDelay, cancellationToken);
    //        }
    //    }

    //    if (_logger.IsEnabled(LogLevel.Error))
    //        _logger.LogError("E-SEE communication failed after {MaxRetries} attempts.", _maxRetries);
    //    return string.Empty;
    //}

    /// <summary>
    /// Auto-discovers paired or nearby E-SEE devices.
    /// </summary>
    //private async Task<BluetoothAddress?> DiscoverEseeDeviceAsync(CancellationToken token)
    //{
    //    try
    //    {
    //        if (BluetoothRadio.Default.Mode != RadioMode.Discoverable)
    //            throw new Exception("No Bluetooth adapter detected.");
    //        if (BluetoothRadio.Default.Mode == RadioMode.PowerOff)
    //            BluetoothRadio.Default.Mode = RadioMode.Connectable;

    //        string deviceName = BluetoothRadio.Default.Name;
    //        string deviceAddress = BluetoothRadio.Default.LocalAddress.ToString();

    //        using var client = new BluetoothClient();
    //        var devices = await Task.Run(() => client.DiscoverDevices(), token);

    //        // You can filter by device name prefix or MAC vendor
    //        var eseeDevice = devices.FirstOrDefault(d =>
    //            d.DeviceName.Contains("E-SEE", StringComparison.OrdinalIgnoreCase) ||
    //            d.DeviceName.Contains("ESEE", StringComparison.OrdinalIgnoreCase));

    //        if (eseeDevice != null)
    //        {
    //            if (_logger.IsEnabled(LogLevel.Information))
    //                _logger.LogInformation("✅ Found E-SEE device: {Name} [{Address}]", eseeDevice.DeviceName, eseeDevice.DeviceAddress);
    //            return eseeDevice.DeviceAddress;
    //        }

    //        _logger.LogWarning("⚠️ No E-SEE device found in discovery scan.");
    //        return null;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "❌ Error during E-SEE device discovery.");
    //        return null;
    //    }
    //}

    //private void SaveDeviceAddress(string address)
    //{
    //    try
    //    {
    //        string filePath = Path.Combine(AppContext.BaseDirectory, "esee_device.txt");
    //        File.WriteAllText(filePath, address);
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogWarning(ex, "Failed to persist E-SEE address.");
    //    }
    //}

    //private async Task<string> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
    //{
    //    var sb = new StringBuilder();
    //    var buffer = new byte[1024];
    //    var start = DateTime.UtcNow;

    //    while ((DateTime.UtcNow - start) < _readTimeout && !cancellationToken.IsCancellationRequested)
    //    {
    //        if (stream.CanRead && stream is NetworkStream ns && ns.DataAvailable)
    //        {
    //            int bytes = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
    //            if (bytes > 0)
    //            {
    //                sb.Append(Encoding.ASCII.GetString(buffer, 0, bytes));
    //                if (sb.ToString().EndsWith("\r\n") || sb.ToString().EndsWith("END"))
    //                    break;
    //            }
    //        }
    //        else
    //        {
    //            await Task.Delay(500, cancellationToken);
    //        }
    //    }

    //    return sb.ToString().Trim();
    //}

    //private async Task<bool> TryReconnectAsync(BluetoothAddress addr, CancellationToken cancellationToken)
    //{
    //    for (int attempt = 1; attempt <= 3; attempt++)
    //    {
    //        try
    //        {
    //            using var client = new BluetoothClient();
    //            client.Connect(addr, BluetoothService.SerialPort);
    //            Console.WriteLine("✅ Reconnected successfully");
    //            return true;
    //        }
    //        catch
    //        {
    //            Console.WriteLine($"Retry {attempt}/3 failed...");
    //            await Task.Delay(_connectRetryDelay, cancellationToken);
    //        }
    //    }
    //    Console.WriteLine("❌ Unable to reconnect to device.");
    //    return false;
    //}
}