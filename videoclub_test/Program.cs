using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

// Helper method for model validation
static IEnumerable<string> ValidateModel(object model)
{
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(model);
    Validator.TryValidateObject(model, validationContext, validationResults, true);
    return validationResults.Select(vr => vr.ErrorMessage ?? "");
}


var builder = WebApplication.CreateBuilder(args);

// Configure JSON options to ignore extra properties
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});


var app = builder.Build();

// Logging middleware
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
    await next.Invoke();
    Console.WriteLine($"Response: {context.Response.StatusCode}");
});

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("An unexpected error occurred.");
    });
});


// In-memory data store
var movies = new Dictionary<int, Movie>
{
    { 1, new Movie { Id = 1, Title = "The Matrix" } },
    { 2, new Movie { Id = 2, Title = "Inception" } },
    { 3, new Movie { Id = 3, Title = "Interstellar" } }
};

// Get all movies
app.MapGet("/movies", () => Results.Ok(movies.Values));

// Get a movie by ID
app.MapGet("/movies/{id:int}", (int id) =>
{
    if (movies.TryGetValue(id, out var movie))
    {
        return Results.Ok(movie);
    }
    return Results.NotFound($"Movie with ID {id} not found.");
});

// Add a new movie
app.MapPost("/movies", ([FromBody] Movie movie) =>
{
    var validationResults = ValidateModel(movie);
    if (validationResults.Any())
    {
        return Results.BadRequest(validationResults);
    }

    if (movies.ContainsKey(movie.Id))
    {
        return Results.Conflict($"Movie with ID {movie.Id} already exists.");
    }
    movies[movie.Id] = movie;
    return Results.Created($"/movies/{movie.Id}", movie);
});

// Update an existing movie
app.MapPut("/movies/{id:int}", ([FromBody] Movie updatedMovie) =>
{
    var validationResults = ValidateModel(updatedMovie);
    if (validationResults.Any())
    {
        return Results.BadRequest(validationResults);
    }

    if (!movies.ContainsKey(updatedMovie.Id))
    {
        return Results.NotFound($"Movie with ID {updatedMovie.Id} not found.");
    }
    movies[updatedMovie.Id] = updatedMovie;
    return Results.Ok(updatedMovie);
});

// Delete a movie
app.MapDelete("/movies/{id:int}", (int id) =>
{
    if (!movies.Remove(id))
    {
        return Results.NotFound($"Movie with ID {id} not found.");
    }
    return Results.Ok($"Movie with ID {id} deleted.");
});

app.Run();

public class Movie
{
    [Range(1, int.MaxValue, ErrorMessage = "Id must be a positive integer.")]
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public required string Title { get; set; }
}