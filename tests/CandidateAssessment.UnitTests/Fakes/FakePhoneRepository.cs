using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Domain.Entities;

namespace CandidateAssessment.UnitTests.Fakes;

/// <summary>
/// Repositório simulado de telefones que reproduz o comportamento do EF Core: telefones
/// adicionados por <c>Person.AddPhone</c> ficam visíveis pela propriedade de navegação
/// do agregado Person. Esta implementação consulta o <see cref="FakePersonRepository"/>
/// associado para localizar os telefones, assim como o EF Core faria pelo DbContext.
/// </summary>
internal sealed class FakePhoneRepository : IPhoneRepository
{
    private readonly FakePersonRepository _personRepository;

    public FakePhoneRepository(FakePersonRepository personRepository)
    {
        _personRepository = personRepository;
    }

    /// <summary>
    /// Construtor de compatibilidade para testes que não precisam localizar telefones
    /// entre agregados. Os telefones só poderão ser encontrados se <see cref="AddAsync"/>
    /// for chamado explicitamente.
    /// </summary>
    public FakePhoneRepository()
        : this(new FakePersonRepository())
    {
    }

    public Task<Phone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var phone = _personRepository.AllPhones().FirstOrDefault(p => p.Id == id);
        return Task.FromResult(phone);
    }

    public Task<bool> ExistsForPersonAsync(
        Guid personId,
        string number,
        CancellationToken cancellationToken = default)
    {
        var result = _personRepository.AllPhones()
            .Any(p => p.PersonId == personId && p.Number == number);
        return Task.FromResult(result);
    }

    public Task AddAsync(Phone phone, CancellationToken cancellationToken = default)
    {
        // No fluxo atual, os telefones são rastreados por Person.AddPhone() e pela
        // propriedade de navegação do agregado Person. Este método é mantido para
        // cumprir a interface, mas não faz nada quando os telefones vêm pelo agregado.
        return Task.CompletedTask;
    }
}
