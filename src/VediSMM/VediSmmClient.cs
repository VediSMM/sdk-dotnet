using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VediSMM.Errors;

namespace VediSMM;

/// <summary>Reusable secure asynchronous transport for the VediSMM public user API.</summary>
public sealed class VediSmmClient : IDisposable
{
    private static readonly Regex PathParameter = new("\\{(?<name>[A-Za-z0-9_]+)\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ReservedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "X-API-Token",
        "Host",
        "Content-Length",
        "Content-Type",
        "Idempotency-Key",
        "If-Match",
    };
    private static readonly HashSet<HttpStatusCode> RetryableStatuses =
    [
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
    ];

    private readonly HttpClient httpClient;
    private readonly Uri baseAddress;
    private readonly string? accessToken;
    private readonly Func<CancellationToken, ValueTask<string?>>? tokenProvider;
    private readonly TimeSpan requestTimeout;
    private readonly int maximumResponseBytes;
    private readonly int maximumErrorBytes;
    private readonly int maximumAttempts;
    private readonly TimeSpan maximumRetryDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private int disposed;

    /// <summary>Creates a client with one owned redirect-denying connection pool.</summary>
    /// <param name="options">Secure client options.</param>
    public VediSmmClient(VediSmmClientOptions? options = null)
        : this(options ?? new VediSmmClientOptions(), CreateDefaultHandler(), disposeHandler: true)
    {
    }

    internal VediSmmClient(VediSmmClientOptions options, HttpMessageHandler handler)
        : this(options, handler, disposeHandler: false)
    {
    }

    private VediSmmClient(VediSmmClientOptions options, HttpMessageHandler handler, bool disposeHandler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);
        baseAddress = ValidateBaseAddress(options.BaseAddress);
        requestTimeout = BoundedDuration(
            options.RequestTimeout, nameof(options.RequestTimeout), TimeSpan.FromDays(1));
        maximumRetryDelay = BoundedDuration(
            options.MaximumRetryDelay, nameof(options.MaximumRetryDelay), TimeSpan.FromMinutes(1));
        maximumResponseBytes = BoundedBytes(
            options.MaximumResponseBytes, nameof(options.MaximumResponseBytes), 256 * 1024 * 1024);
        maximumErrorBytes = BoundedBytes(
            options.MaximumErrorBytes, nameof(options.MaximumErrorBytes), 1024 * 1024);
        if (options.MaximumAttempts is < 1 or > 8)
        {
            throw new ConfigurationException("MaximumAttempts must be between 1 and 8.");
        }

        if (options.AccessToken is not null && options.TokenProvider is not null)
        {
            throw new ConfigurationException("AccessToken and TokenProvider are mutually exclusive.");
        }

        maximumAttempts = options.MaximumAttempts;
        accessToken = options.AccessToken;
        tokenProvider = options.TokenProvider;
        delayAsync = options.DelayAsync ?? throw new ConfigurationException("DelayAsync must not be null.");
        httpClient = new HttpClient(handler, disposeHandler)
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };
    }

    /// <summary>Calls one canonical operation with finite deadlines and typed failures.</summary>
    /// <param name="operationId">Canonical operation ID from <see cref="OperationCatalog"/>.</param>
    /// <param name="options">Call-specific path, query, body, and metadata.</param>
    /// <param name="cancellationToken">Cooperative caller cancellation.</param>
    /// <returns>The successful immutable response.</returns>
    public async Task<ApiResponse> CallAsync(
        string operationId,
        CallOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (!OperationCatalog.All.TryGetValue(operationId, out ApiOperation? operation))
        {
            throw new ConfigurationException($"Unknown VediSMM operation ID: {operationId}.");
        }

        CallSnapshot call = Snapshot(options ?? new CallOptions());
        TimeSpan timeout = call.Timeout ?? requestTimeout;
        BoundedDuration(timeout, nameof(CallOptions.Timeout), TimeSpan.FromDays(1));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        string? token;
        try
        {
            token = await ResolveTokenAsync(operation, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException failure) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new RequestTimeoutException("VediSMM request deadline expired while resolving credentials.", failure);
        }

        bool retryEligible = IsRetryEligible(operation, call);
        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                using HttpRequestMessage request = await BuildRequestAsync(operation, call, token, deadline.Token)
                    .ConfigureAwait(false);
                using HttpResponseMessage response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    deadline.Token).ConfigureAwait(false);

                int status = (int)response.StatusCode;
                if (status is >= 300 and < 400)
                {
                    throw new RedirectException(response.StatusCode, response.Headers.Location);
                }

                TimeSpan? retryAfter = ParseRetryAfter(response.Headers.RetryAfter);
                if (!response.IsSuccessStatusCode)
                {
                    if (retryEligible && attempt < maximumAttempts && RetryableStatuses.Contains(response.StatusCode))
                    {
                        await DelayForRetryAsync(attempt, retryAfter, deadline.Token).ConfigureAwait(false);
                        continue;
                    }

                    throw await CreateApiExceptionAsync(response, token, retryAfter, deadline.Token).ConfigureAwait(false);
                }

                return await CreateSuccessAsync(response, call.ResponseStream, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException failure) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
            {
                if (retryEligible && attempt < maximumAttempts)
                {
                    throw new RequestTimeoutException("VediSMM request deadline expired before a retry could start.", failure);
                }

                throw new RequestTimeoutException("VediSMM request deadline expired.", failure);
            }
            catch (HttpRequestException) when (retryEligible && attempt < maximumAttempts)
            {
                await DelayForRetryAsync(attempt, null, deadline.Token).ConfigureAwait(false);
            }
            catch (HttpRequestException failure)
            {
                throw new TransportException("VediSMM HTTP transport failed.", failure);
            }
        }

        throw new TransportException("VediSMM retry policy ended unexpectedly.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            httpClient.Dispose();
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"VediSmmClient({baseAddress.GetLeftPart(UriPartial.Authority)})";

    private static SocketsHttpHandler CreateDefaultHandler() => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        MaxConnectionsPerServer = 64,
        UseCookies = false,
    };

    private static Uri ValidateBaseAddress(Uri? value)
    {
        if (value is null || !value.IsAbsoluteUri || !string.IsNullOrEmpty(value.UserInfo)
            || !string.IsNullOrEmpty(value.Query) || !string.IsNullOrEmpty(value.Fragment))
        {
            throw new ConfigurationException("BaseAddress must be an absolute API URI without credentials, query, or fragment.");
        }

        bool secure = value.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        bool loopback = value.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && IsLoopback(value.Host);
        if (!secure && !loopback)
        {
            throw new ConfigurationException("BaseAddress must use HTTPS; HTTP is allowed only for loopback tests.");
        }

        string absolute = value.AbsoluteUri.EndsWith('/') ? value.AbsoluteUri : value.AbsoluteUri + "/";
        return new Uri(absolute, UriKind.Absolute);
    }

    private static bool IsLoopback(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    private static TimeSpan BoundedDuration(TimeSpan value, string name, TimeSpan maximum)
    {
        if (value <= TimeSpan.Zero || value > maximum)
        {
            throw new ConfigurationException($"{name} must be positive and no greater than {maximum}.");
        }

        return value;
    }

    private static int BoundedBytes(int value, string name, int maximum)
    {
        if (value <= 0 || value > maximum)
        {
            throw new ConfigurationException($"{name} must be between 1 and {maximum} bytes.");
        }

        return value;
    }

    private static CallSnapshot Snapshot(CallOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Path is null || options.Query is null || options.Headers is null)
        {
            throw new ConfigurationException("Path, Query, and Headers collections must not be null.");
        }

        foreach ((string key, string value) in options.Path)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                throw new ConfigurationException("Path parameters require non-blank names and non-null values.");
            }
        }

        foreach ((string key, IReadOnlyList<string> values) in options.Query)
        {
            if (string.IsNullOrWhiteSpace(key) || values is null || values.Any(static value => value is null))
            {
                throw new ConfigurationException("Query parameters require non-blank names and non-null values.");
            }
        }

        foreach ((string key, IReadOnlyList<string> values) in options.Headers)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Contains('\r') || key.Contains('\n')
                || values is null || values.Any(static value => value is null || value.Contains('\r') || value.Contains('\n')))
            {
                throw new ConfigurationException("Headers require valid single-line names and values.");
            }
        }

        int bodyCount = (options.JsonBody is null ? 0 : 1)
            + (options.RawBody is null ? 0 : 1)
            + (options.ContentFactory is null ? 0 : 1);
        if (bodyCount > 1)
        {
            throw new ConfigurationException("JsonBody, RawBody, and ContentFactory are mutually exclusive.");
        }

        var path = new ReadOnlyDictionary<string, string>(
            options.Path.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        var query = new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            options.Query.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
                StringComparer.Ordinal));
        var headers = new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            options.Headers.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
                StringComparer.OrdinalIgnoreCase));
        byte[]? json = options.JsonBody is null ? null : JsonSerializer.SerializeToUtf8Bytes(options.JsonBody);
        byte[]? raw = options.RawBody?.ToArray();
        string? contentType = NormalizeOptional(options.ContentType, "ContentType");
        if (contentType is not null && !MediaTypeHeaderValue.TryParse(contentType, out _))
        {
            throw new ConfigurationException("ContentType must be a valid media type.");
        }

        return new CallSnapshot(
            path,
            query,
            headers,
            json,
            raw,
            options.ContentFactory,
            contentType,
            NormalizeOptional(options.IdempotencyKey, "IdempotencyKey"),
            NormalizeOptional(options.IfMatch, "IfMatch"),
            options.Timeout,
            options.ResponseStream);
    }

    private static string? NormalizeOptional(string? value, string name)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value) || value.Contains('\r') || value.Contains('\n'))
        {
            throw new ConfigurationException($"{name} must be a non-blank single-line value.");
        }

        return value;
    }

    private async ValueTask<string?> ResolveTokenAsync(ApiOperation operation, CancellationToken cancellationToken)
    {
        if (!operation.Authenticated)
        {
            return null;
        }

        string? token;
        if (tokenProvider is not null)
        {
            try
            {
                token = await tokenProvider(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                throw new TransportException("VediSMM token provider failed.");
            }
        }
        else
        {
            token = accessToken;
        }

        if (string.IsNullOrWhiteSpace(token) || token.Contains('\r') || token.Contains('\n'))
        {
            throw new ConfigurationException("An authenticated operation requires a valid in-memory access token.");
        }

        return token;
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(
        ApiOperation operation,
        CallSnapshot call,
        string? token,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(new HttpMethod(operation.Method), BuildUri(operation, call));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("VediSMM-DotNet/0.1.1");
        request.Headers.TryAddWithoutValidation("X-SDK-Version", "0.1.1");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("X-API-Token", token);
        }

        if (call.IdempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", call.IdempotencyKey);
        }

        if (call.IfMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", call.IfMatch);
        }

        foreach ((string name, IReadOnlyList<string> values) in call.Headers)
        {
            if (ReservedHeaders.Contains(name))
            {
                request.Dispose();
                throw new ConfigurationException($"Header {name} is reserved and managed by the SDK.");
            }

            if (!request.Headers.TryAddWithoutValidation(name, values))
            {
                request.Dispose();
                throw new ConfigurationException($"Header {name} is not a valid request header.");
            }
        }

        try
        {
            request.Content = await CreateContentAsync(call, cancellationToken).ConfigureAwait(false);
            return request;
        }
        catch (OperationCanceledException)
        {
            request.Dispose();
            throw;
        }
        catch (ConfigurationException)
        {
            request.Dispose();
            throw;
        }
        catch (Exception)
        {
            request.Dispose();
            throw new TransportException("VediSMM request body factory failed.");
        }
    }

    private Uri BuildUri(ApiOperation operation, CallSnapshot call)
    {
        string relative = PathParameter.Replace(operation.Path, match =>
        {
            string name = match.Groups["name"].Value;
            if (!call.Path.TryGetValue(name, out string? value) || string.IsNullOrEmpty(value))
            {
                throw new ConfigurationException($"Missing path parameter: {name}.");
            }

            return Uri.EscapeDataString(value);
        });
        var uri = new Uri(baseAddress, relative.TrimStart('/'));
        if (call.Query.Count == 0)
        {
            return uri;
        }

        string query = string.Join("&", call.Query.SelectMany(pair => pair.Value.Select(value =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(value)}")));
        var builder = new UriBuilder(uri) { Query = query };
        return builder.Uri;
    }

    private static async ValueTask<HttpContent?> CreateContentAsync(CallSnapshot call, CancellationToken cancellationToken)
    {
        HttpContent? content = null;
        if (call.JsonBody is not null)
        {
            content = new ByteArrayContent(call.JsonBody);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }
        else if (call.RawBody is not null)
        {
            content = new ByteArrayContent(call.RawBody);
        }
        else if (call.ContentFactory is not null)
        {
            content = await call.ContentFactory(cancellationToken).ConfigureAwait(false)
                ?? throw new ConfigurationException("ContentFactory returned null.");
        }

        if (content is not null && call.ContentType is not null)
        {
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(call.ContentType);
        }

        return content;
    }

    private static bool IsRetryEligible(ApiOperation operation, CallSnapshot call)
    {
        bool methodIsIdempotent = operation.Method is "GET" or "PUT" or "DELETE";
        bool stableCommand = call.IdempotencyKey is not null;
        return methodIsIdempotent || stableCommand;
    }

    private async Task DelayForRetryAsync(int attempt, TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        double exponentialMilliseconds = Math.Min(200 * Math.Pow(2, attempt - 1), maximumRetryDelay.TotalMilliseconds);
        TimeSpan delay = retryAfter ?? TimeSpan.FromMilliseconds(exponentialMilliseconds);
        if (delay > maximumRetryDelay)
        {
            delay = maximumRetryDelay;
        }

        if (delay > TimeSpan.Zero)
        {
            await delayAsync(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static TimeSpan? ParseRetryAfter(RetryConditionHeaderValue? value)
    {
        if (value?.Delta is TimeSpan delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (value?.Date is DateTimeOffset date)
        {
            TimeSpan duration = date - DateTimeOffset.UtcNow;
            return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        }

        return null;
    }

    private async Task<ApiResponse> CreateSuccessAsync(
        HttpResponseMessage response,
        Stream? sink,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers = SnapshotHeaders(response);
        string? requestId = FirstHeader(response, "X-Request-ID");
        string? etag = response.Headers.ETag?.ToString();
        if (sink is not null)
        {
            await response.Content.CopyToAsync(sink, cancellationToken).ConfigureAwait(false);
            return new ApiResponse(response.StatusCode, headers, null, requestId, etag);
        }

        byte[] body = await ReadBoundedAsync(response.Content, maximumResponseBytes, failWhenExceeded: true, cancellationToken)
            .ConfigureAwait(false);
        if (body.Length == 0)
        {
            return new ApiResponse(response.StatusCode, headers, null, requestId, etag);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            JsonElement data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out JsonElement wrapped)
                ? wrapped.Clone()
                : root.Clone();
            return new ApiResponse(response.StatusCode, headers, data, requestId, etag);
        }
        catch (JsonException failure)
        {
            throw new DecodeException("VediSMM returned invalid JSON for a successful response.", failure);
        }
    }

    private async Task<ApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        string? token,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken)
    {
        byte[] body = await ReadBoundedAsync(response.Content, maximumErrorBytes, failWhenExceeded: false, cancellationToken)
            .ConfigureAwait(false);
        string code = "http_" + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        string detail = response.ReasonPhrase ?? "VediSMM request failed";
        JsonElement? errors = null;
        if (body.Length > 0)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                JsonElement root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    code = TextProperty(root, "code") ?? TextProperty(root, "type") ?? code;
                    detail = TextProperty(root, "detail") ?? TextProperty(root, "message") ?? detail;
                    if (root.TryGetProperty("errors", out JsonElement validation))
                    {
                        errors = validation.Clone();
                    }
                }
            }
            catch (JsonException)
            {
                detail = "VediSMM returned a non-JSON error body";
            }
        }

        code = Redact(code, token);
        detail = Redact(detail, token);
        errors = errors is null ? null : RedactJson(errors.Value, token);
        string? requestId = FirstHeader(response, "X-Request-ID");
        return response.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => new RateLimitException(code, detail, errors, requestId, retryAfter),
            HttpStatusCode.PreconditionFailed => new PreconditionFailedException(code, detail, errors, requestId, retryAfter),
            _ => new ApiException(response.StatusCode, code, detail, errors, requestId, retryAfter),
        };
    }

    private static string? TextProperty(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Redact(string value, string? token)
    {
        string redacted = token is null ? value : value.Replace(token, "[REDACTED]", StringComparison.Ordinal);
        return redacted.Length <= 2048 ? redacted : redacted[..2048] + "…";
    }

    private static JsonElement RedactJson(JsonElement value, string? token)
    {
        if (token is null)
        {
            return value.Clone();
        }

        string redacted = value.GetRawText().Replace(token, "[REDACTED]", StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(redacted);
        return document.RootElement.Clone();
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maximumBytes,
        bool failWhenExceeded,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long length && length > maximumBytes && failWhenExceeded)
        {
            throw new DecodeException($"VediSMM response exceeds the {maximumBytes} byte buffer limit.");
        }

        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream(Math.Min(maximumBytes, 16 * 1024));
        byte[] buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            int remaining = maximumBytes + 1;
            while (remaining > 0)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (output.Length > maximumBytes)
        {
            if (failWhenExceeded)
            {
                throw new DecodeException($"VediSMM response exceeds the {maximumBytes} byte buffer limit.");
            }

            output.SetLength(maximumBytes);
        }

        return output.ToArray();
    }

    private static ReadOnlyDictionary<string, IReadOnlyList<string>> SnapshotHeaders(HttpResponseMessage response)
    {
        var all = response.Headers.Concat(response.Content.Headers)
            .ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
                StringComparer.OrdinalIgnoreCase);
        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(all);
    }

    private static string? FirstHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    private sealed record CallSnapshot(
        IReadOnlyDictionary<string, string> Path,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Query,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
        byte[]? JsonBody,
        byte[]? RawBody,
        Func<CancellationToken, ValueTask<HttpContent>>? ContentFactory,
        string? ContentType,
        string? IdempotencyKey,
        string? IfMatch,
        TimeSpan? Timeout,
        Stream? ResponseStream);
}
