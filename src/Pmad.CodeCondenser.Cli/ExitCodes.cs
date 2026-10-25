namespace Pmad.CodeCondenser.Cli;

/// <summary>
/// Process exit codes returned by codecondenser CLI.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Operation succeeded without error.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// General error or unhandled exception.
    /// </summary>
    public const int Error = 1;

    /// <summary>
    /// Specific exit code returned in incremental mode when additional code duplication exceeds tolerance margin.
    /// </summary>
    public const int DuplicationDetected = 2;

    /// <summary>
    /// Operation was canceled by user (Ctrl+C / SIGINT).
    /// </summary>
    public const int Canceled = 130;
}
