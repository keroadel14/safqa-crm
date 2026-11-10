using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Safqa.Application;
namespace Safqa.Infrastructure;

/// Maps domain exceptions to RFC 7807 problem details (409 for optimistic-lock conflicts).
public class ExceptionMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            var (status, code) = ex switch
            {
                ConflictException c => (409, c.Code),
                NotFoundException => (404, "NOT_FOUND"),
                DomainRuleException d => (d.Code == "FORBIDDEN" ? 403 : 422, d.Code),
                FluentValidation.ValidationException => (422, "VALIDATION"),
                _ => (500, "INTERNAL")
            };
            ctx.Response.StatusCode = status; ctx.Response.ContentType = "application/problem+json";
            var pd = new ProblemDetails { Status = status, Title = code, Detail = status == 500 ? "Unexpected error" : ex.Message };
            pd.Extensions["code"] = code;
            if (ex is FluentValidation.ValidationException v) pd.Extensions["fields"] = v.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.First().ErrorMessage);
            await ctx.Response.WriteAsJsonAsync(pd);
        }
    }
}
