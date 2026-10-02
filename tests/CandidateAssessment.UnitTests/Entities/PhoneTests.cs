using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Domain.Enums;
using CandidateAssessment.Domain.ValueObjects;

namespace CandidateAssessment.UnitTests.Entities;

public class PhoneTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldGenerateIdAndSetAuditFields()
    {
        var number = PhoneNumber.Create("(13) 99999-9999", PhoneType.Mobile);
        var phone = Phone.Create(Guid.NewGuid(), PhoneType.Mobile, number, FixedNow);

        Assert.NotEqual(Guid.Empty, phone.Id);
        Assert.Equal(PhoneType.Mobile, phone.Type);
        Assert.Equal("13999999999", phone.Number);
        Assert.Equal(FixedNow, phone.CreatedAtUtc);
        Assert.Equal(FixedNow, phone.UpdatedAtUtc);
    }

    [Fact]
    public void Update_ShouldChangeTypeAndNumber()
    {
        var number = PhoneNumber.Create("(13) 99999-9999", PhoneType.Mobile);
        var phone = Phone.Create(Guid.NewGuid(), PhoneType.Mobile, number, FixedNow);

        var newNumber = PhoneNumber.Create("(11) 3333-4444", PhoneType.Residential);
        var later = FixedNow.AddDays(1);

        phone.Update(PhoneType.Residential, newNumber, later);

        Assert.Equal(PhoneType.Residential, phone.Type);
        Assert.Equal("1133334444", phone.Number);
        Assert.Equal(later, phone.UpdatedAtUtc);
        Assert.Equal(FixedNow, phone.CreatedAtUtc);
    }
}
