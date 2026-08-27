namespace Dispatch.Api.OpenApi;

// Nomes das categorias do Swagger, num lugar só — usados tanto no .WithTags(...) de cada
// endpoint quanto nas descrições registradas em TagDescriptionsDocumentTransformer.
internal static class OpenApiTags
{
    public const string Autenticacao = "Autenticação";
    public const string Conferentes = "Conferentes";
    public const string Protocolos = "Protocolos";
    public const string Importacao = "Importação";
    public const string CentralDeRegras = "Central de Regras";
    public const string MinhaFila = "Minha Fila";
    public const string Sistema = "Sistema";
}
