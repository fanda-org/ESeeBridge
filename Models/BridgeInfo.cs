namespace EseeBridge.Models;

public record BridgeInfo(string MachineName, string? IPAddress, int BridgeServicePort, int ListenerPort);