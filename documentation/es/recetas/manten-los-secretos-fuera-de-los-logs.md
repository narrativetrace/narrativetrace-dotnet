<!-- source: documentation/recipes/keep-secrets-out-of-the-logs.md blob a0329b2750b5 | translated: 2026-10-18 | reviewed: - -->
# Mantén los secretos fuera de los logs

[English](../../recipes/keep-secrets-out-of-the-logs.md) | **Español** | [Português](../../pt-BR/receitas/mantenha-os-segredos-fora-dos-logs.md) | [简体中文](../../zh-CN/实用方案/别让密钥进日志.md)

## El problema

> Si esto registra cada llamada con cada argumento, va a registrar un número de
> tarjeta. Convénceme de que no lo hace.

El checkout recibe una contraseña y un número de tarjeta. Abajo hay una
ejecución con ambos, el log que escribió de verdad y la prueba que demuestra
que ninguno de los dos valores está en ese log.

## Configuración mínima

Nada. La ocultación (redaction) está activada por defecto en cada integración
que se distribuye, y la lista de denegación se consulta en la *captura*, antes
de que un valor se convierta en evento, así que un valor denegado nunca llega a
la línea del log, al archivo de traza, al camino con búfer ni a un listener del
pipeline. El valor por defecto solo se desactiva entregando a un proxy otra
política a propósito (`RedactionPolicy.Disabled` en `ProxyOptions`), y eso es
exactamente lo que detectaría la prueba de abajo.
[Privacidad y ocultación](../privacidad-y-ocultacion.md) es la versión fila por
fila, verificada contra el código.

Aun así, hay dos cosas que vale la pena hacer una vez en tu propio proyecto:

1. **Escribe la prueba de esta página para tu propio flujo.** Una ocultación
   que no has verificado con una aserción es una afirmación, no una propiedad
   de tu build. El hallazgo `trap.redaction-proof` del doctor existe
   exactamente para esto.
2. **Conoce la salida de emergencia.** Un secreto bajo un nombre que nadie
   podría adivinar —`fingerprint`, `answer2`— se alcanza con `[NotTraced]` en
   el parámetro, la propiedad o el componente de record, que siempre gana
   ([Guía de atributos](../../guides/es/guia-de-atributos.md)).

## El escenario

Un colaborador nuevo, para que la ejecución lleve una contraseña además de una
tarjeta. Su parámetro se llama `password`: ese nombre es todo el punto:

```csharp
/// <summary>Signs a customer in before the checkout charges them. The parameter is named <c>password</c>.</summary>
public interface ICustomerAccounts
{
    /// <returns>The session id the checkout carries for the rest of the request.</returns>
    string SignIn(string customerId, string password);
}
```

La ejecución inicia sesión del cliente y hace el pedido. Los dos valores
secretos son constantes aquí para que la prueba pueda buscarlos después en la
salida; un flujo real los obtiene de la petición:

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

## La salida real

**El log que escribió la ejecución**, a través del logger del propio proyecto:

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

**La traza de la misma ejecución**, que es la otra superficie a la que podría
haber llegado un secreto: el renderizado Markdown que contiene un archivo de
traza:

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

## Cómo leerla

**Qué regla atrapó qué valor.** Tres valores han desaparecido, por dos
mecanismos independientes:

| Línea | Valor | Atrapado por |
|---|---|---|
| `#1` `ICustomerAccounts.SignIn(…, password: [REDACTED])` | `orchid-trombone-77` | Su **nombre**. `password` está en la lista de denegación siempre activa, que coincide con nombres de parámetro, de propiedad y de componente de record —en varios idiomas— en cualquier clase, incluidas las tuyas. El valor en sí no coincide con ningún patrón; nada salvo el nombre podría haberlo salvado |
| `#2` `IOrderService.PlaceOrder(…, cardNumber: [REDACTED])` | `4242424242424242` | Su **nombre** otra vez: `cardNumber` está en la misma lista |
| `#2.2` `IPaymentService.Charge(…, card: [REDACTED])` | el mismo número, una llamada más abajo | Su **valor**. El parámetro se llama `card`, que la lista de denegación no conoce, y el número es válido según Luhn, así que la comprobación independiente de forma del valor lo atrapó de todos modos |

Esa tercera fila es la que vale la pena recordar: `card` es como se llama un
parámetro en el código real, y el eje del nombre por sí solo lo habría
impreso. El eje de la forma conoce un número de tarjeta válido según Luhn, un
JWT, una cadena `Set-Cookie`, un número de la Seguridad Social estadounidense
con guiones y un número de identidad nacional que pasa su propia suma de
verificación.

**Dónde viven las reglas.** En la biblioteca, no en tu configuración:
`RedactionPolicy.Default`, consultada por el único `ValueRenderer` que toda
integración distribuida construye de la misma manera. Ningún nivel de log ni
clave de appsettings llega hasta ella;
[Privacidad y ocultación](../privacidad-y-ocultacion.md) enumera las
superficies que pueden recibir otra política y qué prevalece sobre qué.
Por cierto, `customerId: "C-1234"` *sí* se escribe completo: un id de cliente
es identificativo, no secreto, y NarrativeTrace no adivina. Si el tuyo no debe
registrarse, eso es `[NotTraced]`, que es una decisión en tu código fuente y
no un estado que un despliegue pueda cambiar.

**Por qué la prueba es la demostración, no la página.** La prueba lee lo que
escribió el logger, y la traza que está a su lado, y afirma dos cosas: que los
tres parámetros ocultos están presentes *como* `[REDACTED]`, y que ninguna de
las dos cadenas secretas aparece en ningún sitio. Una página solo puede
mostrarte bytes; una prueba hace fallar el build el día en que un refactor
renombra `password` a algo que la lista no conoce, o alguien entrega al proxy
un `RedactionPolicy.Disabled`. Esa es la diferencia entre "era seguro cuando se
escribió esto" y "es seguro ahora". Conserva la aserción de ausencia —el
`DoesNotContain`— incluso cuando la de presencia parezca suficiente: un valor
que desaparece porque la llamada dejó de trazarse por completo seguiría
satisfaciendo la primera.

## Qué no se captura

- **El código que no se traza no es de NarrativeTrace para ocultar.** Arriba
  se envuelven cuatro interfaces; un secreto que pasa entre dos clases sin
  envolver no se ve y, por tanto, ni se oculta ni se registra. Eso no es una
  garantía de seguridad: es una garantía de silencio.
- **Un secreto que escribe tu propia instrucción de log es tuyo.**
  NarrativeTrace oculta lo que *él* renderiza.
  `logger.LogInformation("charging {Card}", cardNumber)` es tu línea, en tu
  código, y nada aquí la filtra. El contrato de ocultación trata de la
  narrativa generada, no de todo lo que escribe el proceso.
- **Un valor de retorno no se compara por nombre**, porque no tiene. `SignIn`
  devuelve `"SESSION-C-1234"` y se imprime. Un token de sesión devuelto por un
  método trazado necesita que lo atrapen las reglas de forma, o un tipo de
  retorno más estrecho.
- **Nombres que la lista no conoce.** La lista es grande y multilingüe, no
  infinita. Un secreto llamado `q3` solo lo atrapa su forma, si tiene una.
  Afirma, no asumas: ese es todo el punto de esta página.
- **Lo que pasa después de escribirse la línea del log.** El envío, la
  retención y el acceso al archivo son de tu plataforma; el `.nt` estructural
  no lleva ningún valor y es el artefacto pensado para confirmarse en el
  repositorio y para entregarse a un modelo
  ([Revisa un flujo complejo antes de cambiarlo](revisa-un-flujo-complejo-antes-de-cambiarlo.md)).

**La skill para esto:** `narrativetrace-doctor`: una de sus comprobaciones es
exactamente "ocultación nunca demostrada en una prueba", y nombra la solución.
