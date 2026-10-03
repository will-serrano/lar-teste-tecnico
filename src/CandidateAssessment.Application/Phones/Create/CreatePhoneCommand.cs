using CandidateAssessment.Domain.Enums;

namespace CandidateAssessment.Application.Phones.Create;

/// <summary>
/// Comando para adicionar um telefone a uma pessoa existente.
/// O número é aceito formatado ou apenas com dígitos; o objeto de valor o normaliza.
/// </summary>
public sealed record CreatePhoneCommand(
    Guid PersonId,
    PhoneType Type,
    string Number);
