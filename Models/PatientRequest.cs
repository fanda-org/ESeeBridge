namespace EseeBridge.Models;

public record PatientRequest(
    string Id,
    string Name,
    int Age,
    string Gender
);