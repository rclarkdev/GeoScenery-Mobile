using System.Security.Claims;
using System.Text.Json;
using GeoScenery.Api.Logging;
using GeoScenery.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GeoScenery.Tests;

[TestFixture]
public sealed class SqlRequestLoggingMiddlewareTests
{
    [Test]
    public async Task GivenAnAuthenticatedRequest_WhenItCompletes_ThenTheAuditLogReceivesTheUserAndCorrelationId()
    {
        var auditLog = new CapturingAuditLog();
        var context = CreateContext("/api/scenes", "client-correlation-id", 42);
        var middleware = new SqlRequestLoggingMiddleware(_ => Task.CompletedTask, NullLogger<SqlRequestLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context, auditLog);

        Assert.That(auditLog.Entry!.UserId, Is.EqualTo(42));
        Assert.That(auditLog.Entry.CorrelationId, Is.EqualTo("client-correlation-id"));
        Assert.That(auditLog.Entry.Level, Is.EqualTo("Information"));
        Assert.That(auditLog.Entry.Properties!["outcome"], Is.EqualTo("Success"));
        Assert.That(auditLog.Entry.Properties["durationMilliseconds"], Is.TypeOf<long>());
    }

    [Test]
    public async Task GivenAnInvalidCorrelationId_WhenARequestCompletes_ThenAReplacementCorrelationIdIsUsed()
    {
        var auditLog = new CapturingAuditLog();
        var context = CreateContext("/api/scenes", new string('x', 65));
        var middleware = new SqlRequestLoggingMiddleware(_ => Task.CompletedTask, NullLogger<SqlRequestLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context, auditLog);

        Assert.That(context.Response.Headers["X-Correlation-ID"].ToString(), Is.Not.EqualTo(new string('x', 65)));
        Assert.That(auditLog.Entry!.CorrelationId, Is.EqualTo(context.Response.Headers["X-Correlation-ID"].ToString()));
    }

    [Test]
    public async Task GivenAClientError_WhenARequestCompletes_ThenTheAuditLogUsesWarningLevel()
    {
        var auditLog = new CapturingAuditLog();
        var context = CreateContext("/api/scenes/404");
        var middleware = new SqlRequestLoggingMiddleware(httpContext =>
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }, NullLogger<SqlRequestLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context, auditLog);

        Assert.That(auditLog.Entry!.Level, Is.EqualTo("Warning"));
        Assert.That(auditLog.Entry.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(auditLog.Entry.Properties!["outcome"], Is.EqualTo("Rejected"));
    }

    [Test]
    public void GivenTheRequestDelegateThrows_WhenLoggingRuns_ThenTheOriginalExceptionIsRethrown()
    {
        var auditLog = new CapturingAuditLog { ThrowOnWrite = true };
        var context = CreateContext("/api/scenes");
        var expected = new InvalidOperationException("request failed");
        var middleware = new SqlRequestLoggingMiddleware(_ => throw expected, NullLogger<SqlRequestLoggingMiddleware>.Instance);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, auditLog));

        Assert.That(exception, Is.SameAs(expected));
        Assert.That(auditLog.Entry!.Level, Is.EqualTo("Error"));
    }

    [Test]
    public async Task GivenAnUnhandledException_WhenTheExceptionHandlerAndRequestLoggerRun_ThenOneGenericFailureIsRecorded()
    {
        var auditLog = new CapturingAuditLog();
        var context = CreateContext("/api/scenes");
        context.Response.Body = new MemoryStream();
        var exceptionHandler = new ApiExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("sensitive internal detail"));
        var requestLogger = new SqlRequestLoggingMiddleware(
            exceptionHandler.InvokeAsync,
            NullLogger<SqlRequestLoggingMiddleware>.Instance);

        await requestLogger.InvokeAsync(context, auditLog);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            Assert.That(context.Response.ContentType, Is.EqualTo("application/problem+json"));
            Assert.That(auditLog.WriteCount, Is.EqualTo(1));
            Assert.That(auditLog.Entry!.Level, Is.EqualTo("Error"));
            Assert.That(auditLog.Entry.Properties!["outcome"], Is.EqualTo("Failure"));
            Assert.That(auditLog.Entry.Properties["exceptionType"], Is.EqualTo(nameof(InvalidOperationException)));
            Assert.That(auditLog.Entry.Message, Is.EqualTo("HTTP request failed."));
            Assert.That(auditLog.Entry.PropertiesJson, Does.Not.Contain("sensitive internal detail"));
            Assert.That(responseBody, Does.Not.Contain("sensitive internal detail"));
            Assert.That(responseBody, Does.Contain("traceId"));
        });
    }

    [Test]
    public async Task GivenSqlLoggingFails_WhenARequestCompletes_ThenTheRequestStillCompletes()
    {
        var auditLog = new CapturingAuditLog { ThrowOnWrite = true };
        var context = CreateContext("/api/scenes");
        var middleware = new SqlRequestLoggingMiddleware(_ => Task.CompletedTask, NullLogger<SqlRequestLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context, auditLog);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    [Test]
    public async Task GivenAHealthRequest_WhenItCompletes_ThenNoAuditEntryIsWritten()
    {
        var auditLog = new CapturingAuditLog();
        var context = CreateContext("/health/ready");
        var middleware = new SqlRequestLoggingMiddleware(_ => Task.CompletedTask, NullLogger<SqlRequestLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context, auditLog);

        Assert.That(auditLog.Entry, Is.Null);
    }

    [Test]
    public async Task GivenAFailingHealthRequest_WhenItCompletes_ThenTheFailureIsLogged()
    {
        var auditLog = new CapturingAuditLog();
        var context = CreateContext("/health/ready");
        var middleware = new SqlRequestLoggingMiddleware(httpContext =>
        {
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        }, NullLogger<SqlRequestLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context, auditLog);

        Assert.That(auditLog.Entry!.Level, Is.EqualTo("Error"));
        Assert.That(auditLog.Entry.Properties!["outcome"], Is.EqualTo("Failure"));
    }

    [Test]
    public async Task GivenAnInvalidCorrelationHeader_WhenARequestCompletes_ThenItIsReplacedWithSafeCharacters()
    {
        var auditLog = new CapturingAuditLog();
        var context = CreateContext("/api/scenes", "bad\r\nheader");
        var middleware = new SqlRequestLoggingMiddleware(_ => Task.CompletedTask, NullLogger<SqlRequestLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context, auditLog);

        var correlationId = context.Response.Headers["X-Correlation-ID"].ToString();
        Assert.Multiple(() =>
        {
            Assert.That(correlationId, Is.Not.EqualTo("bad\r\nheader"));
            Assert.That(correlationId, Does.Match("^[A-Za-z0-9]+$"));
            Assert.That(auditLog.Entry!.CorrelationId, Is.EqualTo(correlationId));
        });
    }

    [Test]
    public async Task GivenARequestWithSensitiveQueryParameters_WhenItCompletes_ThenTheQueryIsNotLogged()
    {
        var auditLog = new CapturingAuditLog();
        var context = CreateContext("/auth/reset-password");
        context.Request.QueryString = new QueryString("?token=do-not-log-this");
        var middleware = new SqlRequestLoggingMiddleware(_ => Task.CompletedTask, NullLogger<SqlRequestLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context, auditLog);

        Assert.That(auditLog.Entry!.RequestPath, Is.EqualTo("/auth/reset-password"));
        Assert.That(auditLog.Entry.RequestPath, Does.Not.Contain("do-not-log-this"));
    }

    private static DefaultHttpContext CreateContext(string path, string? correlationId = null, long? userId = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (correlationId is not null)
        {
            context.Request.Headers["X-Correlation-ID"] = correlationId;
        }

        if (userId is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
            ], "test"));
        }

        return context;
    }

    private sealed class CapturingAuditLog : ISqlAuditLog
    {
        public AuditEntry? Entry { get; private set; }

        public int WriteCount { get; private set; }

        public bool ThrowOnWrite { get; init; }

        public Task WriteAsync(HttpContext? httpContext, string level, string eventName, string message, long? userId = null, IReadOnlyDictionary<string, object?>? properties = null, CancellationToken cancellationToken = default)
        {
            WriteCount++;
            Entry = new AuditEntry(
                level,
                eventName,
                message,
                httpContext?.Response.Headers["X-Correlation-ID"].ToString() ?? string.Empty,
                httpContext?.Request.Path.Value,
                httpContext?.Response.StatusCode,
                userId,
                properties);
            if (ThrowOnWrite)
            {
                throw new InvalidOperationException("SQL unavailable");
            }

            return Task.CompletedTask;
        }
    }

    private sealed record AuditEntry(string Level, string EventName, string Message, string CorrelationId,
        string? RequestPath, int? StatusCode, long? UserId, IReadOnlyDictionary<string, object?>? Properties)
    {
        public string PropertiesJson => JsonSerializer.Serialize(Properties);
    }
}