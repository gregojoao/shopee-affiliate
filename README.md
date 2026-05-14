# Shopee.Affiliate

Cliente .NET 10 para a Shopee Affiliate Open API, focado em gerar links de afiliado e consultar ofertas de produto via GraphQL.

## O que a API da Shopee entrega

A Central de Ajuda oficial da Shopee descreve que a Affiliate API permite consultar listas de ofertas/produtos, gerar short links e consultar relatórios. A documentação pública brasileira disponível no portal de afiliados usa GraphQL no endpoint `https://open-api.affiliate.shopee.com.br/graphql`.

Para produto, o endpoint `productOfferV2` retorna os dados que normalmente precisamos no bot:

- `productName`
- `productLink`
- `offerLink`
- `imageUrl`
- `priceMin`
- `priceMax`
- `priceDiscountRate`
- `itemId`
- `shopId`

Observação: a documentação pública não expõe um campo de "preço antigo" pronto. Esta lib calcula `ProductOriginalPrice` a partir de `priceMin` e `priceDiscountRate`, com a mesma regra usada no bot: `original = atual / (1 - desconto / 100)`.

## Instalação local

```bash
dotnet add package Shopee.Affiliate
```

Enquanto o pacote não estiver publicado, gere o `.nupkg` localmente:

```bash
dotnet pack -c Release
```

## Uso

```csharp
using Shopee.Affiliate;

var options = new ShopeeAffiliateOptions
{
    AppId = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_APP_ID")!,
    Secret = Environment.GetEnvironmentVariable("SHOPEE_AFFILIATE_SECRET")!,
    SubIds = new[] { "telegram", "bot" }
};

using var httpClient = new HttpClient();
var client = new ShopeeAffiliateClient(httpClient);

var result = await client.GenerateAffiliateLinkAsync(
    "https://shopee.com.br/produto-i.627750190.23798776965",
    options);

Console.WriteLine(result.AffiliateUrl);
Console.WriteLine(result.ProductTitle);
Console.WriteLine(result.ProductPrice);
Console.WriteLine(result.ProductOriginalPrice);
Console.WriteLine(result.ProductImageUrl);
```

## Fluxo implementado

1. Resolve URL curta da Shopee, quando habilitado.
2. Extrai `shopId` e `itemId` de URLs como `/product/{shopId}/{itemId}`, `/opaanlp/{shopId}/{itemId}`, `-i.{shopId}.{itemId}` e `?shopid=&itemid=`.
3. Consulta `productOfferV2` para buscar metadados e `offerLink`.
4. Se não encontrar oferta, usa `generateShortLink` como fallback.
5. Retorna um modelo padronizado com link afiliado, título, preço, imagem e URL final.

## Autenticação

A assinatura segue o formato usado pela Shopee Affiliate Open API:

```text
Authorization: SHA256 Credential={AppId}, Timestamp={Timestamp}, Signature={Signature}
Signature = SHA256(AppId + Timestamp + Payload + Secret)
```

O `Timestamp` é Unix time em segundos e o `Payload` é o JSON exato enviado no body.

## Publicação

1. Atualize `RepositoryUrl` em `src/Shopee.Affiliate/Shopee.Affiliate.csproj`.
2. Atualize a versão do pacote.
3. Rode:

```bash
dotnet test
dotnet pack -c Release
dotnet nuget push src/Shopee.Affiliate/bin/Release/Shopee.Affiliate.*.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
```

## Licença

MIT
