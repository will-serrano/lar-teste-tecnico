using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.Api.Contracts.Phones;

internal static class PhoneMappings
{
    public static PhoneResponse ToResponse(this Phone phone)
    {
        ArgumentNullException.ThrowIfNull(phone);

        return new PhoneResponse
        {
            Id = phone.Id,
            PersonId = phone.PersonId,
            Type = phone.Type,
            Number = phone.Number,
            CreatedAtUtc = phone.CreatedAtUtc,
            UpdatedAtUtc = phone.UpdatedAtUtc,
        };
    }
}
