<!-- source: documentation/choosing-an-integration.md blob 66bb4be4f7cf | translated: 2026-09-07 | reviewed: - -->
# Escolhendo uma integração

[English](../choosing-an-integration.md) | [Español](../es/elegir-una-integracion.md) | **Português** | [简体中文](../zh-CN/选择集成方式.md)

O NarrativeTrace oferece várias formas de rastrear uma chamada. Esta
página é uma ajuda para decidir, não um tutorial — para o código por trás
de cada caminho, veja o
[Guia de instalação](../guides/pt-BR/guia-de-instalacao.md).

## Você quer... / Comece por...

| Você quer | Comece por |
|---|---|
| Traces em um teste, com o mínimo de cerimônia | `NarrativeFixture` do xUnit ou `NarrativeTestBase` do NUnit |
| Controle explícito sobre exatamente o que é encapsulado, em .NET puro | `NarrativeTraceProxy.Create<T>` (um `DispatchProxy`) |
| Todo serviço com interface registrado no MS.DI sob um namespace, rastreado automaticamente | `AddNarrativeTracing` (o equivalente em .NET do tracing de beans do Spring/Micronaut) |
| Ciclo de vida de requisições HTTP em produção no ASP.NET Core | Middleware do `NarrativeTrace.AspNetCore` |
| Um gate de qualidade de nomenclatura em CI sobre um assembly compilado, sem precisar rodar testes | `dotnet-narrativetrace clarity-scan` + `clarity-check`, ou o pacote `NarrativeTrace.MSBuild` |
| Traces roteados para seu pipeline `ILogger` já existente | `NarrativeTrace.Logging` (`AddNarrativeLogging()`) |
| Spans do OpenTelemetry, em lote ou ao vivo | `NarrativeTrace.Observability` |
| Diagramas de sequência (Mermaid/PlantUML) junto com uma trace | `NarrativeTrace.Diagrams` |
| Zero alterações de código — um app cuja montagem você não controla | **Ainda não disponível.** Planejado (Free) — nenhum análogo no CLR de um `-javaagent` do Java foi decidido; a questão em aberto é um spike entre gerador de código fonte e IL weaving. Veja o [Guia de funcionalidades](../feature-guide.md). |

## A decisão

Todo caminho nesta implementação se conecta em um **limite de interface** — hoje
não há tecelagem de bytecode nem instrumentação sem código (a linha
acima). A pergunta real é *como* você alcança essa chamada de interface:

```
Você está escrevendo um teste?
  sim -> xUnit?  -> NarrativeFixture
         NUnit?  -> NarrativeTestBase (derive dela)

  não -> Você constrói/possui você mesmo a instância que será encapsulada?
           sim -> Está registrada no MS.DI por interface?
                    sim -> AddNarrativeTracing (encapsulamento automático por namespace)
                    não -> NarrativeTraceProxy.Create<T> diretamente
           não -> É um limite de requisição HTTP do ASP.NET Core?
                    sim -> Middleware do NarrativeTrace.AspNetCore
                    não -> a instrumentação sem código está Planejada,
                           não disponível — veja o Guia de funcionalidades
                           antes de presumir que ela existe
```

Sobre isso, independentemente da resposta acima:

- Quer a trace também no seu fluxo de log existente? Adicione o
  `NarrativeTrace.Logging` junto ao caminho de captura que você escolheu.
- Quer spans do OpenTelemetry? Adicione o `NarrativeTrace.Observability` —
  ele exporta a partir de uma `TraceTree` completa (em lote) ou de um
  fluxo de eventos ao vivo, então ele se sobrepõe a qualquer um dos
  caminhos acima em vez de substituí-los.
- Quer um gate de qualidade de nomenclatura como parte do build,
  independente de os testes rodarem? O `NarrativeTrace.MSBuild`/a CLI
  trabalham a partir de um **assembly compilado** via reflexão, não de
  traces capturadas — uma fonte de dados diferente de tudo o mais nesta
  página.

## Uma coisa que todo caminho compartilha

Não importa o que você encapsule, apenas chamadas de **interface** são
visíveis — o `DispatchProxy` é limitado a interfaces por construção,
então chamadas privadas e internas dentro de uma implementação nunca são
rastreadas individualmente. Se você quer uma narração mais granular,
separe o comportamento que importa por trás de sua própria interface. A
ocultação, os limites e o isolamento de exceções são idênticos em todos
os caminhos, também — veja
[Privacidade e ocultação](privacidade-e-ocultacao.md); não existe uma
integração "mais confiável" que os relaxe.

## Ressalvas por caminho

- **`NarrativeTraceProxy.Create<T>`** — `T` precisa ser uma interface e o
  destino precisa implementá-la; uma incompatibilidade aparece como a
  exceção de reflexão subjacente do .NET
  (`DispatchProxy.Create`/`MethodInfo.Invoke`), não uma específica do
  NarrativeTrace. Veja [Solução de problemas](solucao-de-problemas.md).
- **`AddNarrativeTracing`** — chame-o **por último**, depois que todo
  serviço que ele deve enxergar já estiver registrado; o encapsulamento
  só toca os descritores presentes no momento da chamada. A
  correspondência de namespace é por limite de ponto (`MyApp.Services`
  nunca corresponde a `MyApp.ServicesExtra`) e é verificada contra o
  namespace da **interface** para serviços registrados por fábrica, já
  que o tipo de implementação é opaco no momento do registro. Serviços
  com chave e genéricos abertos são deixados sem encapsulamento
  silenciosamente.
- **Middleware do ASP.NET Core** — coloque
  `UseMiddleware<NarrativeTraceMiddleware>()` perto da borda externa do
  pipeline. Código que chama `HttpContext.GetNarrativeContext()` antes de
  o middleware rodar (ou em uma rota excluída) recebe um contexto
  silencioso sem operação, não uma exceção — a requisição se completa
  normalmente, apenas sem ser rastreada.
- **`NarrativeFixture` do xUnit** — um fixture guarda um único contexto,
  então atende a um teste por vez; `IClassFixture<NarrativeFixture>` é
  seguro porque o xUnit não paraleliza dentro de uma classe, mas
  compartilhar uma instância entre classes que rodam em paralelo mescla
  seus spans em uma única trace.
- **`NarrativeTestBase` do NUnit** — derive dela; a conexão de
  setup/teardown é automática, e `OnTraceComplete` é seu ponto de
  sobrescrita para escrever arquivos ou rodar a análise de clareza.
- **`clarity-scan`** — precisa de um caminho para um assembly .NET real e
  compilado; nunca executa o assembly, apenas reflete sobre seus
  metadados.

## Tetos por plataforma

- `net10.0` — superfície completa, incluindo o `NarrativeTrace.AspNetCore`
  (apenas `net10.0` por enquanto).
- `netstandard2.0` — tudo exceto o middleware do ASP.NET Core.
- `net48` — via `NarrativeTrace.Legacy`; compila em qualquer plataforma,
  mas a validação em tempo de execução no Windows ainda está pendente
  (veja o [Guia de funcionalidades](../feature-guide.md)).

## Receitas

Código completo e executável para cada caminho acima vive no
[Guia de instalação](../guides/pt-BR/guia-de-instalacao.md#escolha-um-caminho-de-integracao).
Para o caminho de menor cerimônia do início ao fim — um serviço, um
teste, saída real — veja
[Primeiros 10 minutos](primeiros-10-minutos.md).
