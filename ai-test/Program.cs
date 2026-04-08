using System.ComponentModel;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

var endpoint = builder.Configuration["Azure:Endpoint"]
    ?? throw new InvalidOperationException("Azure:Endpoint is not set.");
var deploymentName = builder.Configuration["Azure:AiModels:TaskGeneration:DeploymentId"]
    ?? throw new InvalidOperationException("Azure:AiModels:TaskGeneration:DeploymentId is not set.");

AIFunction createTaskFn = AIFunctionFactory.Create(
    (
        [Description("The title or description of the task")] string title,
        [Description("The date reference, e.g. 'tomorrow', '2nd January', 'next Monday'")] string date,
        [Description("The time reference, e.g. '3pm', '15:00'. Use 'unspecified' if no time mentioned.")] string time
    ) => new TaskResult("task", title, date, time),
    name: "create_task",
    description: "Call this when the input contains a date or time reference — it is a schedulable task.");

AIFunction createNoteFn = AIFunctionFactory.Create(
    (
        [Description("The full content of the note")] string content
    ) => new NoteResult("note", content),
    name: "create_note",
    description: "Call this when the input has no date or time reference — it is a plain note.");

AIAgent agent = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())
    .AsAIAgent(
        model: deploymentName,
        instructions: """
            You classify user input by calling the appropriate function.
            Always call either create_task or create_note. Never respond with plain text.
            """,
        tools: [createTaskFn, createNoteFn]);

var app = builder.Build();

app.MapPost("/analyze", async (AnalyzeRequest request) =>
{
    var response = await agent.RunAsync(request.Text);

    var result = response.Messages
        .SelectMany(m => m.Contents)
        .OfType<FunctionResultContent>()
        .FirstOrDefault()?.Result;

    return result is not null
        ? Results.Ok(result)
        : Results.Problem("Agent did not call a function.");
});

app.Run();

record AnalyzeRequest(string Text);
record TaskResult(string Type, string Title, string Date, string Time);
record NoteResult(string Type, string Content);
