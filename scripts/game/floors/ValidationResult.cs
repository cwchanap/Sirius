using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public class ValidationResult
{
    public List<ValidationIssue> Issues { get; } = new();
    public bool HasErrors => Issues.Any(i => i.Severity == Severity.Error);
    public void Error(string code, string message) => Issues.Add(new ValidationIssue(Severity.Error, code, message));
}
