using System.Security.Claims;

namespace ManaDuitnya.Api.Extensions;

public static class ClaimsExtensions
{
    public static long GetUserId(this ClaimsPrincipal user) => 
        long.Parse(user.FindFirst("sub")!.Value);
}