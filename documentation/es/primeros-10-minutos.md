<!-- source: documentation/first-10-minutes.md blob cb4479791e79 | translated: 2026-09-03 | reviewed: 2026-09-03 -->
# Primeros 10 minutos

[English](../first-10-minutes.md) | **Español** | [Português](../pt-BR/primeiros-10-minutos.md) | [简体中文](../zh-CN/前10分钟.md)

Un servicio diminuto, una prueba de xUnit, salida real en cada paso. Todo lo
que sigue se ejecutó de verdad contra los propios paquetes de este
repositorio — sin salida imaginada.

## 1. Añade los paquetes

```xml
<PackageReference Include="NarrativeTrace.Core" Version="0.1.0" />
<PackageReference Include="NarrativeTrace.Runtime" Version="0.1.0" />
<PackageReference Include="NarrativeTrace.Proxy" Version="0.1.0" />
<PackageReference Include="NarrativeTrace.Testing.Xunit" Version="0.1.0" />
```

## 2. Añade una interfaz de servicio y su implementación

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

`[NotTraced]` va en el parámetro de la **interfaz** — el proxy despacha
contra los metadatos del método de la interfaz, así que es de ahí de donde
se leen los atributos.

## 3. Añade una prueba de xUnit

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

`WriteArtifacts` es el paso explícito que escribe archivos — sin él,
`NarrativeFixture` es inerte, así que no cuesta nada en una ejecución de
pruebas normal.

## 4. Ejecuta la suite

```bash
export NARRATIVETRACE_OUTPUT=true
dotnet test
```

## 5. Abre la narrativa

```text
narrativetrace-output/traces/OrderServiceTests/places_an_order.md
```

Verás la llamada renderizada con todos los argumentos excepto
`internalNote` (lo cubre el paso 7), el valor de retorno y los tiempos —
generado por completo a partir de los nombres de método y parámetros de
arriba, sin ninguna sentencia de log escrita a mano.

## 6. Renombra `PlaceOrder` a `Process` y observa cómo cae la claridad

Renombra el método (interfaz e implementación) a `Process`, deja todo lo
demás igual, y puntúa la misma traza capturada en línea — sin CLI, sin paso
de ensamblado compilado, solo el analizador sobre la traza que ya tienes:

```csharp
using NarrativeTrace.Clarity;

var before = ClarityAnalyzer.Analyze(_fixture.CaptureTrace());
Console.WriteLine(before.Overall); // PlaceOrder: un verbo específico del dominio
```

Vuelve a ejecutar tras el renombrado e imprime `Overall` de nuevo — cae,
porque `Process` es exactamente el tipo de verbo genérico y sin contenido
que el analizador está construido para penalizar. Nada más de la llamada
cambió; solo el nombre.

## 7. Añade `[NotTraced]` y observa la ocultación

Vuelve a abrir `places_an_order.md` del paso 5: `internalNote` nunca
aparece con su valor real. Para ver el *marcador* explícitamente, renderiza
la traza a texto en lugar de leer el archivo Markdown:

```csharp
using NarrativeTrace.Core;

Console.WriteLine(IndentedTextRenderer.Render(_fixture.CaptureTrace()));
```

El argumento `internalNote` se renderiza como `[REDACTED]` — sustituido
antes de que el valor llegara a leerse, no simplemente ocultado después del
hecho. Quita `[NotTraced]` y vuelve a ejecutar para ver aparecer el valor
real en su lugar, así la diferencia queda inequívoca.

## 8. Mira el artefacto estructural seguro para IA

Junto al archivo Markdown, la misma ejecución escribió un segundo archivo,
sin valores:

```text
narrativetrace-output/structural/OrderServiceTests/places_an_order.nt
```

Ábrelo: solo nombres, jerarquía de llamadas y tipo de resultado — sin
`internalNote`, sin `customerId`, sin valor de retorno. Este es el archivo
seguro para entregar a una herramienta de IA o pegar en un ticket sin una
revisión de ocultación, porque no hay nada que ocultar en primer lugar.
Consulta [Privacidad y ocultación](privacidad-y-ocultacion.md#garantías) para
lo que está verificado sobre él, y [Qué incluir en el commit](que-incluir-en-el-commit.md)
para saber si conservarlo.

## Adónde ir después

- [Elegir una integración](elegir-una-integracion.md) — proxy, DI, ASP.NET
  Core o un framework de pruebas: cuál conviene a tu app.
- [Guía de instalación](../guides/es/guia-de-instalacion.md) — todos los
  paquetes y vías de integración al completo.
- [Guía de configuración](../guides/es/guia-de-configuracion.md) — niveles
  de tracing, formato de salida y todas las variables `NARRATIVETRACE_*`
  usadas arriba.
- [Solución de problemas](solucion-de-problemas.md) — síntoma → causa →
  arreglo para los fallos que la gente realmente encuentra.
