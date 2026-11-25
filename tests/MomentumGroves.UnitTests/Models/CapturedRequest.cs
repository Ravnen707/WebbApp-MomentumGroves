namespace MomentumGroves.Tests.Services;

internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri Uri,
    string Body,
    Dictionary<string, string> Headers);
