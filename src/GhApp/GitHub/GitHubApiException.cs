using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GhApp.Support;

namespace GhApp.GitHub;

internal sealed class GitHubApiException : GhAppException
{
    public GitHubApiException(HttpStatusCode statusCode, string path, string body)
        : base(BuildMessage(statusCode, path, body))
    {
        StatusCode = (int)statusCode;
        Path = path;
        Body = body;
    }

    public int StatusCode { get; }
    public string Path { get; }
    public string Body { get; }

    private static string BuildMessage(HttpStatusCode statusCode, string path, string body)
    {
        var detail = body.Trim();
        if (detail.Length > 1000)
        {
            detail = detail[..1000] + "...";
        }
        return detail.Length == 0
            ? $"GitHub API {(int)statusCode} em {path}"
            : $"GitHub API {(int)statusCode} em {path}: {detail}";
    }
}
