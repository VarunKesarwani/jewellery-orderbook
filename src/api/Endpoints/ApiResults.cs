namespace JewelleryOrderbook.Api.Endpoints;

/// <summary>
/// Problem details with a stable machine-readable code, so the Angular client can branch on
/// the failure without string matching on a human message. See section 06 of the LLD.
/// </summary>
public static class ApiResults
{
    public static IResult Problem(string code, int statusCode, string? detail = null, string? title = null) =>
        Results.Problem(
            detail: detail,
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
