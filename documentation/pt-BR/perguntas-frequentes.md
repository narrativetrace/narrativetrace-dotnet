<!-- source: documentation/faq.md blob 7bf23139d0b6 | translated: 2026-09-18 | reviewed: - -->
# NarrativeTrace .NET — Perguntas frequentes

[English](../faq.md) | [Español](../es/preguntas-frecuentes.md) | **Português** | [简体中文](../zh-CN/常见问题.md)

## Quem vence: o nível de tracing ou o nível do meu logger?

**Eu defini o nível de tracing como `Detail`, mas nada aparece nos meus logs. Ou: defini meu
logger como `Warning` e o trace ainda aparece completo em `CaptureTrace()`. Qual configuração
vence?**

As duas, porque respondem perguntas diferentes. O NarrativeTrace tem dois seletores
independentes, e um evento capturado pode chegar a um logger por dois caminhos diferentes.

**O seletor 1, `TracingLevel`, decide o que é capturado.** `Off`, `Errors`, `Summary`,
`Narrative`, `Detail` — a própria configuração do NarrativeTrace (`NarrativeTraceConfig.Level`),
do mais baixo ao mais alto. Ele fica na frente da captura: uma chamada que esse nível filtra nunca
se torna parte da árvore de trace. Ele não existe para `CaptureTrace()`, para nenhum renderizador
nem para nenhum logger, e nenhuma outra configuração pode trazê-la de volta. Esse seletor também é
o único que muda o custo do tracing: `Off` pula a captura completamente — `EnterMethod` retorna
imediatamente e nenhum evento é criado. Todo outro nível intercepta toda chamada — `Errors` e
`Summary` ainda capturam um evento completo de entrada/saída, e então `TraceTreeBuilder` poda a
árvore montada depois (um nó de erro mantém toda a sua subárvore; uma árvore `Summary` colapsa
para as folhas e os frames de erro) — enquanto `Narrative` e `Detail` mantêm a árvore sem filtro,
e `Detail` também captura os valores de parâmetros.

**O seletor 2, o nível do seu logger, decide o que é impresso.** `TraceLoggingOptions` mapeia
tipos de evento para níveis do `Microsoft.Extensions.Logging`: eventos de entrada e do ciclo de
vida de fork/join/fire-and-forget em `Trace` (`EnterLevel`), um retorno bem-sucedido em `Trace`
(`ReturnLevel`), uma saída por exceção em `Warning` (`ExceptionLevel`) — os padrões tanto para
`LoggingNarrativeContext` quanto para a ponte de fluxo de eventos `AddNarrativeLogging()`. (Um
host ASP.NET Core ganha ainda uma linha de nível `Information` por trace de *requisição*
concluída, vinda do `LoggerTraceExporter`, categoria de logger `NarrativeTrace.Export` — um resumo
separado, por requisição, que não faz parte de `TraceLoggingOptions`.) O nível mínimo do seu
próprio logger então faz o que sempre faz: aumentá-lo silencia linhas. Ele nunca captura mais, e
nunca captura menos.

**Agora os dois caminhos pelos quais um evento capturado pode chegar a um logger — é daqui que vem
a confusão.** `DualPathPipeline` é o nome do tipo: um fan-out com um slot síncrono e um slot com
buffer, ambos opcionais.

- O **caminho síncrono** roda em linha, na thread de quem chama, antes que a chamada rastreada
  retorne — ou você conecta `LoggingTraceEventListener.OnEvent` diretamente no próprio slot
  síncrono do `DualPathPipeline`, ou usa `LoggingNarrativeContext`, um decorador de contexto com a
  mesma propriedade de "antes que a chamada retorne", que não passa pelo `DualPathPipeline` de
  jeito nenhum. De qualquer forma, a linha de log é escrita antes que qualquer coisa mais adiante
  veja o resultado.
- O **caminho com buffer** é o outro slot do `DualPathPipeline`: um `BufferedEventConsumer` — um
  anel limitado, sem locks, drenado em uma thread em segundo plano, que descarta carga sob pressão
  para nunca bloquear quem chama. `services.AddNarrativeLogging()` inscreve automaticamente o
  `LoggingTraceEventListener` nele em uma app hospedada por DI; o listener ao vivo do
  OpenTelemetry (`OtelTraceEventListener`, `NarrativeTrace.Observability`) pode se inscrever da
  mesma forma.

Os dois slots — na verdade, todo o pipeline — são opt-in: os registros de fábrica
`AddNarrativeTracing`/`AddNarrativeTrace` constroem um `SyncNarrativeContext` simples, sem nenhum
sink, então nada flui ao vivo até que um host conecte um.

**Nenhum dos dois caminhos é de onde `CaptureTrace()` lê.** O arquivo de trace, o `TraceTree`
retornado por `CaptureTrace()`, uma linha de base de aprovação e a exportação para OpenTelemetry
do `TraceActivityExporter` vêm todos da própria lista de captura de um contexto — sempre ativa,
síncrona, em memória — presente esteja ou não algum sink do pipeline conectado, e nunca consultada
por nenhum logger. Então: um logger em `Warning` e um nível de tracing em `Detail` te dão um log
silencioso e um resultado completo de `CaptureTrace()`. Um nível de tracing em `Summary` e um
logger em `Trace` te dão um log barulhento de um trace fino. Um nível de tracing em `Off` não te
dá nada em lugar nenhum, porque nada foi capturado.

**Regras práticas.**

| Objetivo | Ajuste |
|---|---|
| Reduzir o volume de logs | Aumente o nível mínimo do seu logger para a categoria `NarrativeTrace` (ou restrinja `TraceLoggingOptions`); o arquivo de trace e `CaptureTrace()` ficam intactos. |
| Reduzir o tamanho do trace | Diminua o `TracingLevel` (`Detail` → `Narrative` → `Summary` → `Errors`). |
| Reduzir CPU/memória | Diminua o `TracingLevel` — `Off` pula a captura completamente; o limiar do logger não muda nada no custo de captura. |
| Manter o tracing ativo em produção, mas fora dos logs | Deixe o `TracingLevel` em `Summary` ou `Narrative`; ou não conecte `AddNarrativeLogging()`/`LoggingNarrativeContext` de jeito nenhum, ou aumente a categoria `NarrativeTrace` acima de `Warning` — `CaptureTrace()` e qualquer exportador continuam vendo o quadro completo. |

Veja o [Guia de configuração §8, "Dois seletores, dois caminhos"](../guides/pt-BR/guia-de-configuracao.md#8-dois-seletores-dois-caminhos)
para saber onde cada seletor vive, arquivo por arquivo.
