namespace GhApp.Support;

internal class GhAppException : Exception
{
    public GhAppException(string message)
        : base(message) { }

    public GhAppException(string message, Exception inner)
        : base(message, inner) { }
}
