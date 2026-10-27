namespace LocationTracker.Business.Validation;

/// <summary>Thrown when input breaks a domain rule. The Api layer turns it into a 400 problem details response.</summary>
public class BusinessValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public BusinessValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }
}
