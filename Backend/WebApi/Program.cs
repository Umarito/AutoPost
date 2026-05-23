using Application;
using Infrastructure;
using WebApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWebApi(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseWebApiPipeline();
app.Run();
