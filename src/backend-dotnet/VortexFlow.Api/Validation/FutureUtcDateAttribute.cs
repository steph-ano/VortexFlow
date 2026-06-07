using System.ComponentModel.DataAnnotations;

namespace VortexFlow.Api.Validation;

/// <summary>
/// Validates that a <see cref="DateTime"/> is strictly in the future relative to
/// <see cref="DateTime.UtcNow"/>. The comparison treats the input as UTC: if the
/// caller submits a local time, it is normalised to UTC using
/// <see cref="DateTime.ToUniversalTime"/>, so the validation is independent of
/// the API server's local timezone.
/// </summary>
/// <remarks>
/// The server's <c>CampaignService</c> performs the same check at runtime as
/// defense-in-depth; this attribute is the first line of defense (binding-time)
/// so invalid payloads are rejected with 400 before the service layer is
/// touched. The runtime re-check matters because binding can be bypassed if a
/// caller invokes a service method directly (e.g. from a background job or a
/// future internal pipeline).
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class FutureUtcDateAttribute : ValidationAttribute
{
    /// <summary>
    /// Tolerance applied when comparing against "now". A small positive value
    /// prevents a request submitted milliseconds before the validation runs from
    /// being rejected due to clock drift between the client and the server.
    /// Defaults to 1 second.
    /// </summary>
    public TimeSpan Tolerance { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// When true, the same instant (<c>value == now</c>) is rejected. Defaults to
    /// true because business rules require the date to be strictly in the future
    /// (a post scheduled for "now" would race with the publish job).
    /// </summary>
    public bool Strict { get; set; } = true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            // Required-ness is enforced by [Required]; do not duplicate the error.
            return ValidationResult.Success;
        }
        if (value is not DateTime dt)
        {
            return new ValidationResult(
                $"{validationContext.DisplayName} must be a DateTime value.",
                new[] { validationContext.MemberName ?? string.Empty });
        }

        var asUtc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        var now = DateTime.UtcNow;
        var isFuture = Strict
            ? asUtc > now - Tolerance
            : asUtc >= now - Tolerance;

        return isFuture
            ? ValidationResult.Success
            : new ValidationResult(
                $"{validationContext.DisplayName} must be a UTC date strictly in the future (got {dt:O}, now {now:O}).",
                new[] { validationContext.MemberName ?? string.Empty });
    }
}
