using System.Text.Json.Serialization;

namespace Pmad.CodeCondenser.Core.Models;

/// <summary>
/// Categorization of a file analyzed by CodeCondenser.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FileCategory
{
    /// <summary>
    /// Application business logic, libraries, and services.
    /// </summary>
    Production,

    /// <summary>
    /// Unit and integration test suites.
    /// </summary>
    Test,

    /// <summary>
    /// Tooling- or compiler-generated source code.
    /// </summary>
    Generated
}
