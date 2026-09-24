using Aspire.Hosting.Kubernetes.Resources;

/// <summary>
/// Represents the required subset of the policy/v1 PodDisruptionBudget specification.
/// </summary>
internal sealed class PodDisruptionBudgetSpec
{
    /// <summary>
    /// Gets or initializes the number of ready Kener pods retained during voluntary disruption.
    /// </summary>
    public int MinAvailable { get; init; }

    /// <summary>
    /// Gets or initializes the selector for Kener pod labels.
    /// </summary>
    public LabelSelectorV1 Selector { get; init; } = new();
}
