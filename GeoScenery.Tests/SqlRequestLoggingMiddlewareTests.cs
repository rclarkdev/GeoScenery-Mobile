using System.Security.Claims;
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

        public bool ThrowOnWrite { get; init; }

        public Task WriteAsync(HttpContext? httpContext, string level, string eventName, string message, long? userId = null, IReadOnlyDictionary<string, object?>? properties = null, CancellationToken cancellationToken = default)
        {
            Entry = new AuditEntry(
                level,
                eventName,
                httpContext?.Response.Headers["X-Correlation-ID"].ToString() ?? string.Empty,
                httpContext?.Request.Path.Value,
                httpContext?.Response.StatusCode,
                userId);
            if (ThrowOnWrite)
            {
                throw new InvalidOperationException("SQL unavailable");
            }

            return Task.CompletedTask;
        }
    }

    private sealed record AuditEntry(string Level, string EventName, string CorrelationId, string? RequestPath, int? StatusCode, long? UserId);
}