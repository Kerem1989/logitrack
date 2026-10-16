var builder = WebApplication.CreateBuilder(args);


    builder.Services.AddOpenApi();
    builder.Services.AddControllers();
    builder.Services.AddDbContext<LogiTrackContext>();
    builder.Services.AddProblemDetails();

    var app = builder.Build();

    // Unhandled exceptions -> 500 ProblemDetails; bodyless error status codes -> ProblemDetails
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.MapControllers();



    app.Run();