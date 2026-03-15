namespace Yarp.AiGateway.Abstractions.Models;

public sealed record RouteDecision(
    string Provider,
    string Model,
    bool UsedFallback = false);
