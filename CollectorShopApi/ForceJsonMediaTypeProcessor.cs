using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace CollectorShopApi;

public class ForceJsonMediaTypeProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        foreach (var response in context.OperationDescription.Operation.Responses.Values)
        {
            if (response.Content.TryGetValue("application/octet-stream", out NSwag.OpenApiMediaType? value))
            {
                response.Content.Remove("application/octet-stream");

                response.Content["application/json"] = new NSwag.OpenApiMediaType
                {
                    Schema = new NJsonSchema.JsonSchema { Type = NJsonSchema.JsonObjectType.Object }
                };
            }
        }
        return true;
    }
}