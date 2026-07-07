public enum Severity { Error, Warning }

public record ValidationIssue(Severity Severity, string Code, string Message);
