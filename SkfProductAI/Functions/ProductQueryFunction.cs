using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SkfProductAI.Services;
using System.Net;

namespace SkfProductAI.Functions;

/// <summary>
/// HTTP trigger function exposing the AskProduct endpoint.
/// Route supports GET/POST with inline {question} segment.
/// </summary>
public class ProductQueryFunction
{
    private readonly QueryHandler _handler;
    public ProductQueryFunction(QueryHandler handler) => _handler = handler;

    [Function("AskProduct")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "AskProduct/{*question}")] HttpRequestData req,
        string question)
    {
        // Fallback: if route param empty, try query string ?q=
      
        var response = req.CreateResponse();
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        if (string.IsNullOrWhiteSpace(question))
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync("Question not provided.");
            return response;
        }

        var res = await _handler.AnswerAsync(question);
        response.StatusCode = HttpStatusCode.OK;
        await response.WriteStringAsync(res);
        return response;
    }
}