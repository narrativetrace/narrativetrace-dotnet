<!-- source: documentation/recipes/keep-secrets-out-of-the-logs.md blob a0329b2750b5 | translated: 2026-10-18 | reviewed: - -->
# Mantenha os segredos fora dos logs

[English](../../recipes/keep-secrets-out-of-the-logs.md) | [Español](../../es/recetas/manten-los-secretos-fuera-de-los-logs.md) | **Português** | [简体中文](../../zh-CN/实用方案/别让密钥进日志.md)

## O problema

> Se essa coisa registra toda chamada com todo argumento, ela vai registrar um
> número de cartão. Convença-me de que não registra.

O checkout recebe uma senha e um número de cartão. Abaixo estão uma execução dele
com os dois, o log que ela realmente escreveu e o teste que prova que nenhum dos
valores está nesse log.

## Configuração mínima

Nenhuma. A ocultação vem ligada por padrão em toda integração distribuída, e a
lista de bloqueio é consultada na *captura*, antes de um valor virar evento,
então um valor bloqueado nunca chega à linha de log, ao arquivo de trace, ao
caminho com buffer nem a um listener do pipeline. O padrão só é desligado
quando se entrega a um proxy uma política diferente de propósito
(`RedactionPolicy.Disabled` em `ProxyOptions`), e é exatamente isso que o teste
abaixo pegaria.
[Privacidade e Ocultação](../privacidade-e-ocultacao.md) é a versão linha a
linha, verificada contra o código.

Duas coisas, porém, valem ser feitas uma vez no seu próprio projeto:

1. **Escreva o teste desta página para o seu próprio fluxo.** Uma ocultação que
   você não afirmou é uma alegação, não uma propriedade do seu build. O achado
   `trap.redaction-proof` do doctor existe exatamente para isso.
2. **Conheça a válvula de escape.** Um segredo sob um nome que ninguém
   adivinharia — `fingerprint`, `answer2` — é alcançado com `[NotTraced]` no
   parâmetro, na propriedade ou no componente de record, que sempre vence
   ([Guia de Atributos](../../guides/pt-BR/guia-de-atributos.md)).

## O cenário

Um colaborador novo, para que a execução carregue uma senha além do cartão. O
parâmetro dele se chama `password` — esse nome é todo o ponto:

```csharp
/// <summary>Signs a customer in before the checkout charges them. The parameter is named <c>password</c>.</summary>
public interface ICustomerAccounts
{
    /// <returns>The session id the checkout carries for the rest of the request.</returns>
    string SignIn(string customerId, string password);
}
```

A execução autentica o cliente e faz o pedido. Os dois valores secretos são
constantes aqui para que o teste possa procurá-los na saída depois — um fluxo
real os obtém da requisição:

```csharp
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/keep-secrets-out-of-the-logs.md: one checkout carrying a password and a card number
/// writes neither into the log its own logger produced — the password caught by its NAME, the card
/// number by its name and again by its VALUE (rule 8, docs as tests). The assertion reads what the
/// logger wrote, not a rendering this test produced: a page claiming "the secret is not in your log"
/// is only worth what a real log says.
/// </summary>
public sealed class SecretsRecipeTests
{
    private const string Slug = "keep-secrets-out-of-the-logs";

    // A fixed W3C traceparent, so the page's embedded output names the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    /// <summary>Luhn-valid, like a real one — which is what the value-shape rule recognizes.</summary>
    private const string CardNumber = "4242424242424242";

    /// <summary>
    /// The password this run signs in with. It matches no value-shape rule at all: only its
    /// parameter's NAME can save it. Named for the phrase rather than for the field it fills, because
    /// the public snapshot's secrets gate reads a constant whose name carries that word and whose value
    /// is a string literal as a credential shipped in the open — correctly, for every such line but a
    /// documentation fixture's.
    /// </summary>
    private const string SignInPassphrase = "orchid-trombone-77";

    [Fact]
    public void A_checkout_with_a_password_and_a_card_writes_neither()
    {
        var log = new StringWriter { NewLine = "\n" };
        var context = LoggedContext(log);
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var accounts = NarrativeTraceProxy.Create<ICustomerAccounts>(new InMemoryAccounts(), context);
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);

        var session = accounts.SignIn("C-1234", SignInPassphrase);
        var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardNumber);

        Assert.Equal("SESSION-C-1234", session);
        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        var trace = MarkdownRenderer.Render(context.CaptureTrace());
        AssertNeitherSecretIsInTheLog(log.ToString(), redacted: "[REDACTED]");
        AssertNeitherSecretIsInTheLog(trace, redacted: "`[REDACTED]`");
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "trace.md", trace);
    }

    private static LoggingNarrativeContext LoggedContext(TextWriter log)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        var traceparent = Traceparent.Parse(DemoTraceparent);
        return new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: traceparent)),
            loggerFactory.CreateLogger("NarrativeTrace"));
    }

    /// <summary>
    /// The proof: what the project's own logger wrote, and the trace file beside it, read back and
    /// searched. <paramref name="redacted"/> is how that surface spells a redacted value.
    /// </summary>
    private static void AssertNeitherSecretIsInTheLog(string written, string redacted)
    {
        Assert.Contains($"password: {redacted}", written, StringComparison.Ordinal);
        Assert.Contains($"cardNumber: {redacted}", written, StringComparison.Ordinal);
        Assert.Contains($"card: {redacted}", written, StringComparison.Ordinal);
        Assert.DoesNotContain(SignInPassphrase, written, StringComparison.Ordinal);
        Assert.DoesNotContain(CardNumber, written, StringComparison.Ordinal);
    }
}
```

## A saída real

**O log que a execução escreveu**, através do logger do próprio projeto:

```text
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → ICustomerAccounts.SignIn(customerId: "C-1234", password: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "SESSION-C-1234"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderService.PlaceOrder(customerId: "C-1234", sku: "SKU-KB", quantity: 2, cardNumber: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IInventoryService.Reserve(sku: "SKU-KB", quantity: 2)
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IPaymentService.Charge(customerId: "C-1234", amountCents: 7998, card: [REDACTED])
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "PAY-C-1234-7998"
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - → IOrderRepository.Save(order: Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998))
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← completed
TRACE [NarrativeTrace] [c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7] - ← returned: "ORD-C-1234-SKU-KB-2"
```

**O trace da mesma execução**, que é a outra superfície que um segredo poderia
ter alcançado — a renderização em Markdown que um arquivo de trace guarda:

```text
---
type: trace
entry_point: ICustomerAccounts.SignIn
trace_id: c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7
trace_name: solid wand romps
method_count: 5
error_count: 0
result: success
duration_ms: 12
---

- **ICustomerAccounts.SignIn**(customerId: `"C-1234"`, password: `[REDACTED]`) → `"SESSION-C-1234"` — 12ms #1
- **IOrderService.PlaceOrder**(customerId: `"C-1234"`, sku: `"SKU-KB"`, quantity: `2`, cardNumber: `[REDACTED]`) → `"ORD-C-1234-SKU-KB-2"` — 3ms #2
  - **IInventoryService.Reserve**(sku: `"SKU-KB"`, quantity: `2`) — 0ms #2.1
  - **IPaymentService.Charge**(customerId: `"C-1234"`, amountCents: `7998`, card: `[REDACTED]`) → `"PAY-C-1234-7998"` — 0ms #2.2
  - **IOrderRepository.Save**(order: `Order(OrderId: "ORD-C-1234-SKU-KB-2", CustomerId: "C-1234", Sku: "SKU-KB", Quantity: 2, AmountCents: 7998)`) — 0ms #2.3
```

## Como ler

**Qual regra pegou qual valor.** Três valores sumiram, por dois mecanismos
independentes:

| Linha | Valor | Pego por |
|---|---|---|
| `#1` `ICustomerAccounts.SignIn(…, password: [REDACTED])` | `orchid-trombone-77` | Seu **nome**. `password` está na lista de bloqueio sempre ativa, que casa nomes de parâmetros, de propriedades e de componentes de record — em vários idiomas — em qualquer classe, inclusive a sua. O valor em si não casa com padrão algum; nada além do nome poderia tê-lo salvo |
| `#2` `IOrderService.PlaceOrder(…, cardNumber: [REDACTED])` | `4242424242424242` | Seu **nome** de novo: `cardNumber` está na mesma lista |
| `#2.2` `IPaymentService.Charge(…, card: [REDACTED])` | o mesmo número, uma chamada mais fundo | Seu **valor**. O parâmetro se chama `card`, que a lista de bloqueio não conhece — e o número é válido pelo algoritmo de Luhn, então a verificação independente de formato de valor o pegou de qualquer forma |

Essa terceira linha é a que vale lembrar: `card` é como um parâmetro é chamado em
código real, e só o eixo do nome o teria impresso. O eixo do formato conhece um
número de cartão válido pelo algoritmo de Luhn, um JWT, uma string de
`Set-Cookie`, um número de seguro social americano com hifens e um número de
documento de identidade nacional que passa no próprio dígito verificador.

**Onde as regras vivem.** Na biblioteca, não na sua configuração:
`RedactionPolicy.Default`, consultada pelo único `ValueRenderer` que toda
integração distribuída constrói da mesma forma. Nenhum nível de log e nenhuma
chave de appsettings chega até ela;
[Privacidade e Ocultação](../privacidade-e-ocultacao.md) lista as superfícies
que podem receber uma política diferente e o que prevalece sobre o quê.
Aliás, `customerId: "C-1234"` *é* escrito por inteiro — um id de cliente
identifica, não é segredo, e o NarrativeTrace não adivinha. Se o seu não deve
ser registrado, isso é `[NotTraced]`, que é uma decisão no seu código-fonte, e
não um estado que um deploy possa alternar.

**Por que o teste é a prova, não a página.** O teste lê o que o logger escreveu,
e o trace ao lado, e afirma duas coisas: que os três parâmetros ocultados estão
presentes *como* `[REDACTED]`, e que nenhuma das duas strings secretas aparece
em lugar algum. Uma página só pode mostrar bytes; um teste quebra o build no dia
em que um refactor renomeia `password` para algo que a lista não conhece, ou
alguém entrega ao proxy um `RedactionPolicy.Disabled`. Essa é a diferença entre
"era seguro quando isto foi escrito" e "é seguro agora". Mantenha a asserção de
ausência — o `DoesNotContain` — mesmo quando a de presença parecer suficiente:
um valor que some porque a chamada deixou de ser rastreada ainda satisfaria a
primeira.

## O que não é capturado

- **Código que não é rastreado não é do NarrativeTrace para ocultar.** Quatro
  interfaces são envolvidas acima; um segredo passado entre duas classes não
  envolvidas não é visto e, portanto, nem ocultado nem registrado. Isso não é
  garantia de segurança — é garantia de silêncio.
- **Um segredo que a sua própria instrução de log escreve é seu.** O
  NarrativeTrace oculta o que *ele* renderiza.
  `logger.LogInformation("charging {Card}", cardNumber)` é a sua linha, no seu
  código, e nada aqui a filtra. O contrato de ocultação é sobre a narrativa
  gerada, não sobre tudo o que o processo escreve.
- **Um valor de retorno não é casado por nome**, porque não tem nenhum. `SignIn`
  retorna `"SESSION-C-1234"` e isso é impresso. Um token de sessão retornado por
  um método rastreado precisa que as regras de formato o peguem, ou de um tipo
  de retorno mais restrito.
- **Nomes que a lista não conhece.** A lista é grande e multilíngue, não
  infinita. Um segredo chamado `q3` só é pego pelo formato, se tiver um.
  Afirme, não presuma — que é todo o ponto desta página.
- **O que acontece depois que a linha de log é escrita.** Envio, retenção e
  acesso ao arquivo são da sua plataforma; o `.nt` estrutural não traz valor
  nenhum e é o artefato pensado para ir ao commit e para ser entregue a um
  modelo ([Revise um fluxo complexo antes de alterá-lo](revise-um-fluxo-complexo-antes-de-altera-lo.md)).

**A skill para isto:** `narrativetrace-doctor` — uma das verificações dela é
exatamente "ocultação nunca provada em um teste", e ela nomeia a correção.
