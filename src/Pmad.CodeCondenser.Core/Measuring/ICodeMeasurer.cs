using Pmad.AI.Tokens;
using Pmad.CodeCondenser.Core.Models;

namespace Pmad.CodeCondenser.Core.Measuring;

/// <summary>
/// Service responsible for measuring source code and comments in terms of estimated LLM tokens.
/// </summary>
public interface ICodeMeasurer
{
    /// <summary>
    /// Measures LLM token counts for a single source file.
    /// </summary>
    FileMeasurement MeasureFile(
        string filePath,
        FileCategory category,
        string sourceText,
        HeuristicTokenEstimator estimator);
}
