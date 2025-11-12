using EseeBridge.Models;

namespace EseeBridge.Services;

public class TestResultParser
{
    public static PatientResult ParseResult(string resultData)
    {
        var result = new PatientResult();
        var lines = resultData
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .ToArray();

        bool isRightEye = false;
        bool isLeftEye = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (line.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
            {
                result.PatientId = line[3..].Trim();
            }
            else if (DateTime.TryParse(line, out var diagDate) && result.DiagDate == default)
            {
                result.DiagDate = diagDate;
            }
            else if (line.StartsWith("OD / Right", StringComparison.OrdinalIgnoreCase))
            {
                isRightEye = true;
                isLeftEye = false;
            }
            else if (line.StartsWith("OS / Left", StringComparison.OrdinalIgnoreCase))
            {
                isRightEye = false;
                isLeftEye = true;
            }
            else if (isRightEye && IsValidDataLine(line))
            {
                var eyeResult = ParseEyeData(line);
                if (eyeResult is not null)
                {
                    result.RightEyeResults.Add(eyeResult);
                }
            }
            else if (isLeftEye && IsValidDataLine(line))
            {
                var eyeResult = ParseEyeData(line);
                if (eyeResult is not null)
                {
                    result.LeftEyeResults.Add(eyeResult);
                }
            }
            else if (isLeftEye && line.StartsWith("Aurolab", StringComparison.OrdinalIgnoreCase))
            {
                result.DeviceName = line;
            }
            else if (isLeftEye && line.StartsWith("S/N:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                    result.DeviceSerialNumber = parts[1];
            }
        }

        return result;
    }

    private static bool IsValidDataLine(string line)
    {
        return !string.IsNullOrWhiteSpace(line)
            && !line.StartsWith("Test", StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("-", StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("Aurolab", StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("S/N:", StringComparison.OrdinalIgnoreCase);
    }

    private static TestResult? ParseEyeData(string line)
    {
        var data = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (int.TryParse(data[0], out int testNum) && testNum > 0) //
        {
            if (data.Length == 5)
            {
                return new TestResult
                {
                    TestNumber = data[0],
                    Sphere = data[1],
                    Cylinder = data[2],
                    Axis = data[3],
                    SphericalEquivalent = data[4]
                };
            }
            else if (data.Length == 4)
            {
                return new TestResult
                {
                    TestNumber = data[0],
                    Sphere = data[1],
                    Cylinder = data[2],
                    Axis = "--",
                    SphericalEquivalent = data[3]
                };
            }
        }
        return null;
    }
}