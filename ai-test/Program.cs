using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["Azure:ApiKey"]
    ?? throw new InvalidOperationException("Azure:ApiKey is not set.");
var endpoint = builder.Configuration["Azure:Endpoint"]
    ?? throw new InvalidOperationException("Azure:Endpoint is not set.");
var deploymentName = builder.Configuration["Azure:DeploymentName"]
    ?? throw new InvalidOperationException("Azure:DeploymentName is not set.");

IChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient();
builder.Services.AddSingleton(chatClient);

var taskAgent = builder.AddAIAgent(
    "taskAnalyzer",
    instructions: """
        You analyze text and detect if it contains any date or time reference.
        If the text contains a date or time (e.g. "7pm", "2nd January", "tomorrow", "next Monday"),
        respond ONLY in this exact format: task on {date} at {time}
        - If only a date is mentioned with no time, use "unspecified time"
        - If only a time is mentioned with no date, use "today"
        If the text contains NO date or time reference, respond ONLY with: this is a note
        Do not add any other text or explanation.
        """);

var app = builder.Build();

app.MapPost("/analyze", async (
    AnalyzeRequest request,
    [FromKeyedServices("taskAnalyzer")] AIAgent agent) =>
{
    var response = await agent.RunAsync(request.Text);
    var text = response.Messages
        .SelectMany(m => m.Contents)
        .OfType<Microsoft.Extensions.AI.TextContent>()
        .FirstOrDefault()?.Text ?? "no response";
    return Results.Ok(new { result = text });
});

app.Run();

record AnalyzeRequest(string Text);
