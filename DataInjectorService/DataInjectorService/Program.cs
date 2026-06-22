#pragma warning disable SA1200 // Using directives should be placed correctly
using DataInjectorService.Extensions;
#pragma warning restore SA1200 // Using directives should be placed correctly

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCorsConfiguration(builder.Configuration);

builder.Services.AddControllers();

builder.Services.AddSwaggerGenConfiguration();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowOrigins");

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
