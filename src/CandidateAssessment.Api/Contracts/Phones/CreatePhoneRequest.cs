using CandidateAssessment.Domain.Enums;

namespace CandidateAssessment.Api.Contracts.Phones;

/// <summary>
/// Corpo da requisição para criar um telefone. O número aceita entrada formatada ou somente com dígitos.
/// </summary>
public sealed class CreatePhoneRequest
{
    public PhoneType Type { get; init; }

    public string Number { get; init; } = default!;
}
