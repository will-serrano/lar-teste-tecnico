namespace CandidateAssessment.Api.Configuration;

/// <summary>
/// Configuração fortemente tipada para destinos e enriquecimento do Serilog.
/// Vinculada à seção de configuração <c>Serilog</c>.
/// </summary>
public sealed class SerilogOptions
{
    public const string SectionName = "Serilog";

    /// <summary>
    /// Nível mínimo aplicado ao logger raiz.
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Substitui o nível mínimo para os namespaces de logger informados.
    /// </summary>
    public Dictionary<string, string> Overrides { get; set; } = [];

    /// <summary>
    /// Quando verdadeiro, grava eventos estruturados no console.
    /// </summary>
    public bool WriteToConsole { get; set; } = true;

    public bool ConsoleJson { get; set; }

    /// <summary>
    /// Quando verdadeiro, grava arquivos de log diários rotativos no sistema de arquivos local.
    /// </summary>
    public bool WriteToFile { get; set; } = true;

    /// <summary>
    /// Pasta onde os arquivos de log rotativos são gravados. Caminhos relativos são resolvidos
    /// a partir da raiz de conteúdo da aplicação. Ignorada quando <see cref="WriteToFile"/> é falso.
    /// </summary>
    public string FilePath { get; set; } = "Logs/app-.log";

    /// <summary>
    /// Quantos dias de arquivos de log rotativos manter. Ignorado quando <see cref="WriteToFile"/> é falso.
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 14;
}
