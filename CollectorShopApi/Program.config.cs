namespace CollectorShopApi;

public static class ApplicationSetup
{
    private static readonly string allowedFront = "FrontVue";
    private static readonly string[] allowedFrontOrigin = ["*"];

    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder ConfigureServices()
        {
            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(allowedFront, policy => policy.WithOrigins(allowedFrontOrigin).AllowAnyHeader().AllowAnyMethod());
            });

            return builder;
        }
    }

    extension(WebApplication app)
    {
        public WebApplication ConfigurePipeline()
        {
            app.UseCors(allowedFront);

            if (true || app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            return app;
        }
    }
}