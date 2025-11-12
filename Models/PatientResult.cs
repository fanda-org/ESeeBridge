namespace EseeBridge.Models;

public class PatientResult
{
    public string? PatientId { get; set; }
    public DateTime? DiagDate { get; set; }
    public List<TestResult> RightEyeResults { get; set; }
    public List<TestResult> LeftEyeResults { get; set; }
    public string? DeviceName { get; set; }
    public string? DeviceSerialNumber { get; set; }

    public PatientResult()
    {
        RightEyeResults = [];
        LeftEyeResults = [];
    }
}

public class TestResult
{
    public string? TestNumber { get; set; }
    public string? Sphere { get; set; }
    public string? Cylinder { get; set; }
    public string? Axis { get; set; }
    public string? SphericalEquivalent { get; set; }
}