using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http;

namespace TestAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();


            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapGet("/ok", (HttpContext httpContext) =>
            {
                return TypedResults.Ok(new { });
            });

            app.MapPost("/ok", (HttpContext httpContext) =>
            {
                return TypedResults.Ok(new { });
            });

            app.MapPost("/slow", async (HttpContext httpContext) =>
            {
                await Task.Delay(new Random().Next(2000, 12000));

                return TypedResults.Ok(new { });
            });

            app.MapPost("/hang", async (HttpContext httpContext) =>
            {
                await Task.Delay(60000*5);

                return TypedResults.Ok(new { });
            });

            app.MapPost("/buggy", (HttpContext httpContext) =>
            {
                var rnd = new Random().Next(100);
                if (rnd < 10)
                    return (IResult)TypedResults.StatusCode(500);
                else if (new Random().Next(100) < 20)
                    return (IResult)TypedResults.BadRequest();
                else if (new Random().Next(100) < 30)
                    return (IResult)TypedResults.Forbid();
                else if (new Random().Next(100) < 40)
                    return (IResult)TypedResults.NotFound();

                return (IResult)TypedResults.Ok(new { });
            });

            app.Run();
        }
    }
}
