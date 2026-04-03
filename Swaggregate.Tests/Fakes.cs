using Swaggregate.Services;

namespace Swaggregate.Tests;

/// <summary>Returns the same content for every URL.</summary>
internal sealed class FakeSpecFetcher : ISpecFetcher
{
    private readonly bool _success;
    private readonly string? _content;
    private readonly string? _contentType;
    private readonly string? _error;

    public FakeSpecFetcher(string content, string? contentType = "application/json")
    {
        _success = true;
        _content = content;
        _contentType = contentType;
    }

    public FakeSpecFetcher(string error)
    {
        _success = false;
        _error = error;
    }

    public Task<(bool success, string? content, string? contentType, string? error)> FetchAsync(
        string url, int cacheTtlMinutes = 5, CancellationToken ct = default) =>
        Task.FromResult((_success, _content, _contentType, _error));
}

/// <summary>Returns different responses based on URL — for mixed success/failure tests.</summary>
internal sealed class MixedSpecFetcher : ISpecFetcher
{
    private readonly string _successUrl;
    private readonly string _successContent;
    private readonly string _errorUrl;
    private readonly string _errorMessage;

    public MixedSpecFetcher(string successUrl, string successContent, string errorUrl, string errorMessage)
    {
        _successUrl = successUrl;
        _successContent = successContent;
        _errorUrl = errorUrl;
        _errorMessage = errorMessage;
    }

    public Task<(bool success, string? content, string? contentType, string? error)> FetchAsync(
        string url, int cacheTtlMinutes = 5, CancellationToken ct = default)
    {
        if (url == _successUrl)
            return Task.FromResult((true, (string?)_successContent, (string?)"application/json", (string?)null));
        return Task.FromResult((false, (string?)null, (string?)null, (string?)_errorMessage));
    }
}
