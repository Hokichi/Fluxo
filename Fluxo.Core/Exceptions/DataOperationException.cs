namespace Fluxo.Core.Exceptions;

public sealed class DataOperationException : Exception
{
    public DataOperationException(string performedProcess, string userMessage, Exception innerException)
        : base(userMessage, innerException)
    {
        PerformedProcess = performedProcess;
        UserMessage = userMessage;
    }

    public string PerformedProcess { get; }

    public string UserMessage { get; }
}
