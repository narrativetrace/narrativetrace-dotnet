<!-- source: documentation/first-10-minutes.md blob 94cabad4ff51 | translated: 2026-09-09 | reviewed: 2026-09-03 -->
# Primeiros 10 minutos

[English](../first-10-minutes.md) | [Español](../es/primeros-10-minutos.md) | **Português** | [简体中文](../zh-CN/前10分钟.md)

Um serviço minúsculo, um teste xUnit, saída real em cada etapa. Tudo
abaixo foi executado de verdade contra os próprios pacotes deste
repositório — nenhuma saída imaginada.

## 1. Adicione os pacotes

```xml
<PackageReference Include="NarrativeTrace.Core" Version="0.1.3" />
<PackageReference Include="NarrativeTrace.Runtime" Version="0.1.3" />
<PackageReference Include="NarrativeTrace.Proxy" Version="0.1.3" />
<PackageReference Include="NarrativeTrace.Testing.Xunit" Version="0.1.3" />
```

## 2. Adicione uma interface de serviço e sua implementação

```csharp
using NarrativeTrace.Core.Annotation;

public interface IOrderService
{
    string PlaceOrder(
        string customerId, string productId, int quantity,
        [NotTraced] string internalNote);
}

public sealed class OrderService : IOrderService
{
    public string PlaceOrder(
        string customerId, string productId, int quantity, string internalNote)
        => $"confirmed:{customerId}:{productId}:{quantity}";
}
```

`[NotTraced]` vai no parâmetro da **interface** — o proxy despacha contra
os metadados do método da interface, então é de lá que os atributos são
lidos.

## 3. Adicione um teste xUnit

```csharp
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

public sealed class OrderServiceTests : IClassFixture<NarrativeFixture>
{
    private readonly NarrativeFixture _fixture;
    public OrderServiceTests(NarrativeFixture fixture) => _fixture = fixture;

    [Fact]
    public void Places_an_order()
    {
        _fixture.Run(nameof(Places_an_order), ctx =>
        {
            var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), ctx);
            orders.PlaceOrder("cust-1", "book-123", 2, "gift wrap");
        });

        _fixture.WriteArtifacts(nameof(OrderServiceTests), nameof(Places_an_order), failed: false);
    }
}
```

`WriteArtifacts` é o passo explícito que escreve arquivos — sem ele, o
`NarrativeFixture` fica inerte, então não custa nada em uma execução de
testes normal.

## 4. Execute a suíte

```bash
export NARRATIVETRACE_OUTPUT=true
dotnet test
```

## 5. Abra a narrativa

```text
narrativetrace-output/traces/OrderServiceTests/places_an_order.md
```

Você verá a chamada renderizada com todos os argumentos exceto
`internalNote` (coberto no passo 7), o valor de retorno e o tempo de
execução — gerado inteiramente a partir dos nomes de método e parâmetros
acima, sem nenhuma instrução de log escrita à mão.

## 6. Renomeie `PlaceOrder` para `Process` e observe a clareza cair

Renomeie o método (interface e implementação) para `Process`, mantenha
tudo o mais igual, e pontue a mesma trace capturada em linha — sem CLI,
sem etapa de assembly compilado, apenas o analisador sobre a trace que
você já tem:

```csharp
using NarrativeTrace.Clarity;

var before = ClarityAnalyzer.Analyze(_fixture.CaptureTrace());
Console.WriteLine(before.Overall); // PlaceOrder: um verbo específico do domínio
```

Execute novamente após a renomeação e imprima `Overall` de novo — ela cai,
porque `Process` é exatamente o tipo de verbo genérico e sem conteúdo que
o analisador foi construído para penalizar. Nada mais na chamada mudou; só
o nome.

## 7. Adicione `[NotTraced]` e veja a ocultação

Abra novamente `places_an_order.md` do passo 5: `internalNote` nunca
aparece com seu valor real. Para ver o *marcador* explicitamente,
renderize a trace em texto em vez de ler o arquivo Markdown:

```csharp
using NarrativeTrace.Core;

Console.WriteLine(IndentedTextRenderer.Render(_fixture.CaptureTrace()));
```

O argumento `internalNote` é renderizado como `[REDACTED]` — substituído
antes mesmo de o valor ser lido, não apenas ocultado depois do fato.
Remova `[NotTraced]` e execute novamente para ver o valor real aparecer
em seu lugar, assim a diferença fica inequívoca.

## 8. Veja o artefato estrutural seguro para IA

Junto ao arquivo Markdown, a mesma execução escreveu um segundo arquivo,
sem valores:

```text
narrativetrace-output/structural/OrderServiceTests/places_an_order.nt
```

Abra-o: apenas nomes, hierarquia de chamadas e tipo de resultado — sem
`internalNote`, sem `customerId`, sem valor de retorno. Este é o arquivo
seguro para entregar a uma ferramenta de IA ou colar em um ticket sem uma
revisão de ocultação, porque não há nada para ocultar em primeiro lugar.
Veja [Privacidade e ocultação](privacidade-e-ocultacao.md#garantias) para
o que está verificado sobre ele, e
[O que incluir no commit](o-que-incluir-no-commit.md) para saber se vale a
pena mantê-lo por perto.

## Para onde ir depois

- [Escolhendo uma integração](escolhendo-uma-integracao.md) — proxy, DI,
  ASP.NET Core ou um framework de testes: qual escolher para seu app.
- [Guia de instalação](../guides/pt-BR/guia-de-instalacao.md) — todos os
  pacotes e caminhos de integração por completo.
- [Guia de configuração](../guides/pt-BR/guia-de-configuracao.md) — níveis
  de tracing, formato de saída e todas as variáveis `NARRATIVETRACE_*`
  usadas acima.
- [Solução de problemas](solucao-de-problemas.md) — sintoma → causa →
  correção para os modos de falha que as pessoas realmente encontram.
