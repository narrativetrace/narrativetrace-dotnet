<!-- source: documentation/recipes/follow-a-flow-across-threads.md blob f1abd00eb8da | translated: 2026-10-18 | reviewed: - -->
# Acompanhe um fluxo entre threads

[English](../../recipes/follow-a-flow-across-threads.md) | [Español](../../es/recetas/sigue-un-flujo-entre-hilos.md) | **Português** | [简体中文](../../zh-CN/实用方案/跨线程追踪一个流程.md)

## O problema

> O checkout entrega as partes lentas para outras threads. O trace para no
> submit e não consigo ver o que o trabalho em segundo plano fez.

Uma thread não sabe nada sobre o span em que outra thread está, então entregar
trabalho a um worker entrega o trabalho e mais nada. O trace também precisa ser
entregue — explicitamente, no momento do envio. Abaixo está uma execução que faz
isso e outra que não faz.

## Configuração mínima

Nada além da [instalação](../../guides/pt-BR/guia-de-instalacao.md):
`ForkJoinGroup` e `FireAndForgetGroup` estão em `NarrativeTrace.Runtime`, ao
lado dos contextos. Qual usar depende do que quem chama faz a seguir:

| A chamada que dispara | O helper | O passo final |
|---|---|---|
| Espera o trabalho em segundo plano (esta página) | `ForkJoinGroup` | `await group.JoinAsync()` — ele enxerta o que os ramos registraram sob o span que disparou |
| Retorna sem esperar | `FireAndForgetGroup` | Nenhum. Os filhos são um registro separado, lido por `ChildRoots()` |

Um ramo recebe um **contexto filho isolado** e deve rastrear através desse
argumento: um colaborador envolvido com
`NarrativeTraceProxy.Create(service, branch)` dentro do ramo é o que registra o
trabalho. Se o seu código permanece em um único fluxo lógico através de `await`,
`AsyncNarrativeContext.RunAsync` carrega o trace ambiente para você e você não
escreve nenhum dos dois acima — o
[Guia de Funcionalidades](../../feature-guide.md) é a versão linha a linha.

As linhas de log usam o mesmo logger de
[Acompanhe uma requisição entre serviços](acompanhe-uma-requisicao-entre-servicos.md),
com o id bruto do trace entre colchetes depois da categoria — nesta página o
colchete vazio diz tanto quanto os preenchidos.

## O cenário

**O checkout, com os dois passos lentos fora da sua própria thread.** A entrega é
`group.Fork(branch => …)` em volta de cada tarefa e `await group.JoinAsync()`
depois que as duas foram iniciadas. O inventário e a verificação de fraude são
passados sem envoltório, de modo que a classe decide por qual contexto cada ramo
rastreia:

```csharp
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace Checkout;

/// <summary>
/// The same checkout with the two slow steps off the caller's thread: reserve the stock and run the
/// fraud check as two branches of a <see cref="ForkJoinGroup"/>, wait for both, then charge and save.
/// A branch runs on a pool thread that knows nothing about the caller's trace, so each branch is
/// handed an isolated child context of the group and traces through it; <c>JoinAsync</c> grafts what
/// the branches recorded back under the span that launched them. Skip the join and the branches stay
/// out of the caller's trace. The inventory and the fraud check are passed unwrapped for that reason:
/// this class decides which context each call is traced through.
/// </summary>
public sealed class ParallelCheckout(
    INarrativeContext context,
    IInventoryService inventory,
    IFraudCheckService fraud,
    IPaymentService payments,
    IOrderRepository orders) : IOrderService
{
    private const int UnitPriceCents = 3999;

    public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
    {
        var amountCents = UnitPriceCents * quantity;
        var group = ForkJoinGroup.Create(context);
        var reserving = group.Fork(branch =>
        {
            NarrativeTraceProxy.Create(inventory, branch).Reserve(sku, quantity);
            return true;
        });
        var scoring = group.Fork(branch => NarrativeTraceProxy.Create(fraud, branch).Approve(customerId, amountCents));
        group.JoinAsync().GetAwaiter().GetResult();

        if (!scoring.Result)
        {
            NarrativeTraceProxy.Create(inventory, context).Release(sku, quantity);
            throw new PaymentDeclinedException("the fraud check refused the order");
        }

        _ = reserving.Result;
        payments.Charge(customerId, amountCents, cardNumber);
        var orderId = $"ORD-{customerId}-{sku}-{quantity}";
        orders.Save(new Order(orderId, customerId, sku, quantity, amountCents));
        return orderId;
    }
}
```

**A execução — e o mesmo trabalho sem a entrega.** O primeiro teste roda esse
checkout. O segundo entrega uma chamada rastreada a uma thread worker sem
nenhum grupo, que é como o código era antes de a entrega ser adicionada: o
worker é uma thread que já estava rodando, e fica retido até que a chamada que
disparou tenha retornado, de modo que a ordem dos eventos é a mesma a cada
execução:

```csharp
using System.Collections.Concurrent;
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/follow-a-flow-across-threads.md: the two branches the checkout hands to a
/// <see cref="ForkJoinGroup"/> come home inside the caller's trace — same trace id, grafted under the
/// span that launched them, recorded on other threads — and a task handed to a worker without the
/// group is not under that span at all (rule 8, docs as tests).
/// </summary>
public sealed class AcrossThreadsRecipeTests
{
    private const string Slug = "follow-a-flow-across-threads";

    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    [Fact]
    public void The_background_tasks_stay_in_the_customers_trace()
    {
        var log = new StringWriter { NewLine = "\n" };
        var context = CallerContext(log);
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var checkout = ParallelCheckoutOver(context, stock, repository);

        var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        Assert.Equal(3, stock.AvailableUnits("SKU-KB"));
        Assert.Single(repository.Saved);
        var trace = context.CaptureTrace();
        AssertTheChildrenCameHome(trace);
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "with-handover.nt", StructuralTraceRenderer.RenderDocument(trace, "The background tasks stay in the customers trace"));
    }

    [Fact]
    public async Task A_task_handed_to_a_worker_without_the_handover_is_not_under_the_checkout()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent)));
        var stock = new InMemoryInventory("SKU-KB", 5);
        var jobs = new BlockingCollection<Action>();
        var launched = new ManualResetEventSlim();
        var worker = StartWorker(jobs, launched);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new HandsOverWithoutTheGroup(NarrativeTraceProxy.Create<IInventoryService>(stock, context), jobs), context);

        checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");
        launched.Set();
        await worker;

        Assert.Equal(3, stock.AvailableUnits("SKU-KB"));
        var trace = context.CaptureTrace();
        AssertTheWorkIsADetachedRoot(trace);
        CapturedOutput.Write(Slug, "without-handover.nt", StructuralTraceRenderer.RenderDocument(trace, "A task handed to a worker without the handover"));
    }

    private static LoggingNarrativeContext CallerContext(TextWriter log)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        return new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(DemoTraceparent))),
            loggerFactory.CreateLogger("NarrativeTrace"));
    }

    /// <summary>The checkout with the stock and the fraud check unwrapped: the group decides what each branch traces through.</summary>
    private static IOrderService ParallelCheckoutOver(
        INarrativeContext context, InMemoryInventory stock, InMemoryOrderRepository repository)
    {
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        return NarrativeTraceProxy.Create<IOrderService>(
            new ParallelCheckout(context, stock, new RuleBasedFraudCheck(), payments, orders), context);
    }

    /// <summary>
    /// The branches are in the caller's tree, in the customer's trace, name the launching span as their
    /// parent, and ran on other threads.
    /// </summary>
    private static void AssertTheChildrenCameHome(TraceTree trace)
    {
        var root = Assert.Single(trace.Roots);
        Assert.Equal(
            ["IInventoryService.Reserve", "IFraudCheckService.Approve", "IPaymentService.Charge", "IOrderRepository.Save"],
            root.Children.Select(n => $"{n.Signature.ClassName}.{n.Signature.MethodName}"));
        var branches = root.Children.Where(n => n.Concurrency is not null).ToList();
        Assert.Equal(2, branches.Count);
        Assert.All(branches, branch => Assert.Equal(root.SpanContext!.TraceId, branch.SpanContext!.TraceId));
        Assert.All(branches, branch => Assert.Equal(root.SpanContext!.SpanId, branch.SpanContext!.ParentSpanId));
        Assert.All(branches, branch => Assert.NotEqual(Environment.CurrentManagedThreadId, branch.Concurrency!.ThreadId));
        Assert.Single(branches.Select(branch => branch.Concurrency!.GroupId).Distinct());
    }

    /// <summary>The work happened, but as a second root: nothing joined it to the call that launched it.</summary>
    private static void AssertTheWorkIsADetachedRoot(TraceTree trace)
    {
        Assert.Equal(
            ["IOrderService.PlaceOrder", "IInventoryService.Reserve"],
            trace.Roots.Select(n => $"{n.Signature.ClassName}.{n.Signature.MethodName}"));
        Assert.Empty(trace.Roots[0].Children);
        Assert.Null(trace.Roots[1].Concurrency);
    }

    /// <summary>
    /// A long-lived worker thread, named so a failure names it. It takes one job, then waits for the
    /// launching call to have returned before running it: the order is the point of the page, and a
    /// race would make the output differ from run to run.
    /// </summary>
    private static Task StartWorker(BlockingCollection<Action> jobs, ManualResetEventSlim launched)
    {
        var finished = new TaskCompletionSource();
        var worker = new Thread(() =>
        {
            var job = jobs.Take();
            launched.Wait();
            job();
            finished.SetResult();
        })
        { IsBackground = true, Name = "checkout-worker-1" };
        worker.Start();
        return finished.Task;
    }

    /// <summary>The checkout as it looked before the handover was added: the reservation goes to a worker, the trace does not.</summary>
    private sealed class HandsOverWithoutTheGroup(IInventoryService inventory, BlockingCollection<Action> jobs) : IOrderService
    {
        public string PlaceOrder(string customerId, string sku, int quantity, string cardNumber)
        {
            jobs.Add(() => inventory.Reserve(sku, quantity));
            return $"ORD-{customerId}-{sku}-{quantity}";
        }
    }
}
```

## A saída real

**O log da execução que mantém o trace:**

```text
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderService.PlaceOrder(customerId: "C-1234", sku: "SKU-KB", quantity: 2, cardNumber: [REDACTED])
TRACE [NarrativeTrace] [] - ⑂ fork group created [groupId: fork-1]
TRACE [NarrativeTrace] [] - Graft IInventoryService.Reserve
TRACE [NarrativeTrace] [] - Graft IFraudCheckService.Approve
TRACE [NarrativeTrace] [] - ⑃ fork joined [groupId: fork-1, members: 2]
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderRepository.Save(order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998))
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "ORD-C-1234-SKU-KB-2"
```

**O trace estrutural dessa execução:**

```text
scenario: The background tasks stay in the customers trace

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
  ~ fork [2]
    #1.1 - IFraudCheckService.Approve(customerId, amountCents) → value
    #1.2 - IInventoryService.Reserve(sku, quantity)
  #1.3 - IPaymentService.Charge(customerId, amountCents, card) → value
  #1.4 - IOrderRepository.Save(order)
```

**E o trace estrutural da execução sem a entrega:**

```text
scenario: A task handed to a worker without the handover

#1 - IOrderService.PlaceOrder(customerId, sku, quantity, cardNumber) → value
#2 - IInventoryService.Reserve(sku, quantity)
```

O artefato em Markdown da primeira execução traz mais três coisas — a thread em
que cada ramo rodou, o tempo de relógio do join e uma dica de "async
sequencial" quando os ramos não se sobrepuseram — e não está embutido aqui
porque os ids de thread e os tempos mudam a cada execução.

## Como ler

- **A entrega é visível, duas vezes.**
  `⑂ fork group created [groupId: fork-1]` é o grupo sendo criado, antes de
  qualquer ramo começar; `⑃ fork joined [groupId: fork-1, members: 2]` é o
  `JoinAsync` informando o que voltou. Entre os dois há trabalho em segundo
  plano que pertence a este checkout. Essas linhas carregam um id de trace vazio
  — o último colchete é `[]` — porque não são eventos de span: são o ciclo de
  vida do próprio grupo, publicado pelo contexto e não por uma chamada
  rastreada.
- **As duas linhas `Graft` são os ramos voltando para casa.** Um ramo rastreia
  através do seu contexto filho isolado, então não escreve nenhuma linha de log
  própria enquanto roda; no join, a chamada registrada por cada ramo é
  enxertada sob o span que disparou e a ponte registra isso —
  `Graft IInventoryService.Reserve`, `Graft IFraudCheckService.Approve` — antes
  de `Charge` começar. Leia o log como a ordem em que as coisas *aconteceram*:
  o grupo, os enxertos, o join e, então, as chamadas sequenciais que dependem
  deles.
- **Os filhos estão no trace do cliente, na árvore, uma vez.** O trace
  estrutural guarda os dois ramos dentro de `~ fork [2]`, `#1.1` e `#1.2`, sob
  `#1`, e depois `Charge` e `Save` como `#1.3` e `#1.4`: o fork fica onde o
  join rodou, antes das chamadas que dependem dele, então a árvore se lê na
  ordem em que as coisas aconteceram. Os membros de um grupo são listados em
  uma ordem fixa, não na ordem em que as threads terminaram, e é isso que
  mantém o arquivo estável byte a byte.
- **O que o teste afirma e um log não consegue mostrar.** O id de span é gerado
  por span e muda a cada execução, e o id de thread também, então
  `AssertTheChildrenCameHome` afirma, em vez disso, o que eles teriam mostrado:
  cada ramo carrega o id de trace da raiz e nomeia `#1` como seu span pai, a
  thread registrada de cada ramo não é a de quem chama, e os dois compartilham
  um id de grupo. Essa asserção é a prova desta página. O pai é definido
  quando o contexto do ramo é criado, então um consumidor que segue os ids de
  span pai — o listener do OpenTelemetry ao vivo, por exemplo — vê cada ramo
  sob `#1` desde o primeiro evento, e não como uma raiz.
- **Como a segunda execução difere.** A mesma chamada rastreada, entregue a um
  worker sem grupo, aconteceu: o estoque foi reservado. Só que ela não está sob
  o checkout. O trace estrutural tem duas raízes — `#1 PlaceOrder` sem filhos e
  `#2 Reserve` ao lado — porque o worker rodou a chamada depois de o span que
  disparou ter sido fechado, e nada a uniu de volta. O segundo teste afirma
  exatamente isso: `PlaceOrder` não tem filhos e `Reserve` é uma raiz sem grupo
  de concorrência. O id do trace é o mesmo, porque este cenário compartilha um
  único contexto; o que falta é o *lugar* na história.
- **Por que o worker é retido.** Se o worker for liberado enquanto `PlaceOrder`
  ainda está aberto, a chamada cai sob ele por acaso de temporização, e um
  segundo worker fazendo o mesmo no mesmo contexto compartilhado intercala seus
  spans com os do primeiro. O grupo existe para que nenhum dos dois fique à
  mercê da temporização: cada ramo recebe seu próprio contexto, e o join é o
  único lugar em que eles voltam.
- **Por que um só worker.** O log e o trace estrutural são bytes de execuções
  reais, e duas threads se intercalam de forma diferente a cada vez. O código de
  entrega é o mesmo para qualquer número de ramos; o que mais threads
  acrescentam é saída não determinística, não um segundo mecanismo.

## O que não é capturado

- **Trabalho iniciado sem a entrega não está no span que disparou.** Essa é a
  segunda metade desta página: sem grupo, sem join, sem lugar sob `#1`. Nada
  avisa você, porque, do ponto de vista da biblioteca, uma chamada rastreada em
  outra thread é uma chamada comum.
- **Um ramo que não rastreia através do próprio contexto não está isolado.**
  Capturar através do contexto externo a partir de um ramo anula o isolamento e
  intercala spans; o argumento que o ramo recebe é o que deve ser usado.
- **Um grupo nunca unido não coleta nada no trace de quem chama.** Os ramos
  rodaram e, depois, nada os enxertou: o estoque foi reservado e a árvore de
  quem chama não diz isso. [Solução de Problemas](../solucao-de-problemas.md)
  tem a entrada.
- **Um ramo que lança exceção não volta para casa.** O join leva a falha até
  quem chama e os spans do ramo que falhou não são enxertados; observar isso é
  o `try`/`catch` de quem chama.
- **Trabalho que sobrevive à chamada é outro helper.** O `FireAndForgetGroup`
  não tem join por design — o método que disparou já retornou, então não sobrou
  ninguém em quem mesclar — e engole a falha de uma tarefa disparada.
- **Intercalação.** Com um pool real, os ramos terminam na ordem em que as
  threads forem escalonadas, e é por isso que um log se lê pelo id do trace e
  não pela posição. A estrutura enxertada não é afetada: ela segue o grupo, não
  o relógio.

**A skill para isto:** `narrativetrace-verify` — "roda async" e "atravessa
threads" são dois dos casos para os quais ela existe, e ela lê o trace
estrutural contra a intenção antes de qualquer coisa ser dada por concluída.
