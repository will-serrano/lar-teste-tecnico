namespace CandidateAssessment.Api.Idempotency;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute
{
}
