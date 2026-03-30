using Microsoft.AspNetCore.Mvc;

namespace YongChuanTools.Web;

public static class DecodeEndpoint
{
    public static IResult Handle(DecodeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Hex))
            return Results.BadRequest(new { error = "EMPTY_INPUT", message = "hex field is required" });

        var result = UTProtocol.DecodeFrame(req.Hex);

        if (result == null)
            return Results.BadRequest(new { valid = false, error = "INVALID_HEX", message = "Could not parse hex string" });

        return Results.Ok(result);
    }
}

public record DecodeRequest(string Hex);
