using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceClock.Api.Time;

/// <summary>
/// Turns <see cref="TimeSourceUnavailableException"/> into a clear 503 response for any endpoint
/// (e.g. Clock In): "time service unavailable, nothing was recorded, try again".
/// </summary>
public sealed class TimeSourceUnavailableExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not TimeSourceUnavailableException)
            return false;

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Time service unavailable",
                Detail = "Couldn't get the official Zurich time. Nothing was recorded. Please try again in a moment.",
                Extensions = { ["code"] = "TIME_SOURCE_UNAVAILABLE" },
            },
        });
    }
}
