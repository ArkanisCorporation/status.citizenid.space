using Aspire.Hosting.Kubernetes.Resources;

/// <summary>
/// Represents the policy/v1 PodDisruptionBudget emitted with the Kener Deployment.
/// </summary>
internal sealed class PodDisruptionBudget : BaseKubernetesResource
{
    /// <summary>
    /// Initializes the Kubernetes resource identity.
    /// </summary>
    public PodDisruptionBudget()
        : base("policy/v1", "PodDisruptionBudget")
    {
    }

    /// <summary>
    /// Gets or initializes the PodDisruptionBudget specification.
    /// </summary>
    public PodDisruptionBudgetSpec Spec { get; init; } = new();
}
