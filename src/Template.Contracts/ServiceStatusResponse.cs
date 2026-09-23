namespace Arkanis.Template.Contracts;

/// <summary>
/// Describes the externally visible status of the service.
/// </summary>
public sealed class ServiceStatusResponse
{
    /// <summary>
    /// Creates a service status response.
    /// </summary>
    /// <param name="status">The nonblank service status.</param>
    /// <exception cref="ArgumentException"><paramref name="status"/> is empty or whitespace.</exception>
    public ServiceStatusResponse(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Service status is required.", nameof(status));
        }

        Status = status;
    }

    /// <summary>
    /// Gets the service status.
    /// </summary>
    public string Status { get; }
}
