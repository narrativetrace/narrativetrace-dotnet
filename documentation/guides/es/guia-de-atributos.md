<!-- source: documentation/guides/annotations.md blob f0252fb2ac2b | translated: 2026-08-31 | reviewed: 2026-09-03 -->
# NarrativeTrace .NET — Guía de atributos

[English](../annotations.md) | **Español** | [Português](../pt-BR/guia-de-atributos.md) | [简体中文](../zh-CN/特性指南.md)

NarrativeTrace sigue la filosofía **El código es el log**: los nombres de
métodos, los nombres de parámetros y los valores de retorno ya deberían
contar la historia de la ejecución. Primero mantén la lógica de negocio
limpia y expresiva, y luego recurre a los atributos de forma
*excepcional* — para narración puntual, contexto de error, ocultación o
renderizado de valores a medida.

Los cuatro atributos narrativos viven en `NarrativeTrace.Core.Annotation` —
un solo `using` para todos ellos, y el mismo paquete del que ya viene tu
modelo de trazas. Son metadatos puros: el interceptor `DispatchProxy`
(`NarrativeTrace.Proxy`) lee `[Narrated]`, `[OnError]` y `[NotTraced]` en las
llamadas que intercepta, mientras que `[NarrativeSummary]` y `[NotTraced]` se
honran allí donde se rendericen valores. `[Traced]` es la única excepción: un
marcador específico de `DispatchProxy` sin equivalente en la JVM, así que se
queda en `NarrativeTrace.Proxy`.

```csharp
using NarrativeTrace.Core.Annotation;
```

## Inventario

| Atributo | Namespace | Destino | Propósito |
|---|---|---|---|
| `[NarrativeSummary]` | `NarrativeTrace.Core.Annotation` | Método o propiedad | Renderizado de resumen preferido para el tipo que lo declara. |
| `[Narrated]` | `NarrativeTrace.Core.Annotation` | Método | Añade texto de narración legible a un método trazado. |
| `[OnError]` | `NarrativeTrace.Core.Annotation` | Método (repetible) | Asocia texto de error contextual a un método. |
| `[NotTraced]` | `NarrativeTrace.Core.Annotation` | Parámetro, propiedad, campo | Oculta un valor en la salida de trazas, incluidos los miembros de objetos introspeccionados. |
| `[Traced]` | `NarrativeTrace.Proxy` | Método | Sobrescribe posicionalmente los nombres de parámetros capturados. |

## `[Narrated]`

Usa `[Narrated]` cuando quieras una frase explícita en la traza en lugar de
depender solo del nombre del método y sus parámetros.

```csharp
public interface IOrderService
{
    [Narrated("Placing order of {quantity} units for customer {customerId}")]
    OrderResult PlaceOrder(string customerId, string productId, int quantity);
}
```

- Los marcadores de posición usan **nombres de parámetros**:
  `{customerId}`.
- Se admite un nivel de acceso a propiedades con miembros en
  **PascalCase**: `{customer.Name}` lee la propiedad `Name` del argumento
  `customer`.
- Un marcador desconocido se deja **literal** en la salida (`{custmerId}`
  sobrevive) — una señal de errata incorporada.

**Dónde aparece:** la narración la renderizan `MarkdownRenderer` y (en
nodos correctos) `ProseRenderer`. Siempre está disponible
programáticamente como `node.Signature.Narration`.

## `[OnError]`

Asocia texto específico de contexto para las excepciones. El atributo es
**repetible**; define `ExceptionType` para acotar una plantilla a un tipo
de excepción (por defecto `typeof(Exception)`).

```csharp
public interface IPaymentService
{
    [OnError("Payment declined for {customerId}, amount was {amount}",
             ExceptionType = typeof(PaymentDeclinedException))]
    [OnError("Temporary payment failure for {customerId}",
             ExceptionType = typeof(ExternalServiceException))]
    PaymentConfirmation Charge(
        string customerId, decimal amount, [NotTraced] string token);
}
```

Cómo funciona en .NET (igual que en la edición JVM):

- La plantilla se **resuelve cuando se lanza la excepción**, con las mismas
  reglas de marcadores que `[Narrated]`, y se guarda como
  `node.Signature.ErrorContext`.
- Solo compiten los atributos cuyo `ExceptionType` declarado **coincide con
  la excepción lanzada** (`IsInstanceOfType`); entre las coincidencias gana
  el tipo declarado más específico. Un `[OnError("…")]` a secas
  (implícitamente `typeof(Exception)`) coincide con todo.
- Si **ningún** tipo declarado coincide con la excepción lanzada, no se
  asocia contexto de error. Un método que retorna con normalidad nunca
  lleva contexto de error.

**Dónde aparece:** en un nodo que lanza, el contexto de error resuelto lo
renderizan `MarkdownRenderer` y `ProseRenderer` (después de la excepción),
y siempre está disponible como `node.Signature.ErrorContext`. El
`IndentedTextRenderer`, que solo muestra estructura, no lo enseña.

## `[NotTraced]`

Marca un **parámetro, propiedad o campo** sensible para que su valor quede
oculto en todos los sitios donde la traza se renderice o exporte. El nombre
sigue visible; el valor se sustituye por `[REDACTED]`.

```csharp
public interface IAuthService
{
    Session Login(string username, [NotTraced] string password);
}
```

Los secretos **anidados dentro de un objeto trazado** están cubiertos de
dos maneras, en línea con la postura de ocultación por defecto de la
edición JVM:

- **Lista de denegación por nombre** — el renderizado reflexivo oculta
  automáticamente los nombres de miembro sensibles habituales (`password`,
  `token`, `cvv`, …) vía `RedactionPolicy` (consulta la
  [Guía de configuración](guia-de-configuracion.md#6-ocultación)).
- **`[NotTraced]` en el miembro** — para secretos cuyo nombre no coincide
  con ningún patrón, anota la propiedad, el campo o el parámetro posicional
  del record; el renderizador sustituye el marcador durante la
  introspección, con independencia de la lista de denegación (se mantiene
  incluso con `RedactionPolicy.Disabled`):

```csharp
public sealed record Card(string Last4, [NotTraced] string Pan);

public sealed class Payment
{
    public decimal Amount { get; init; }

    [NotTraced]
    public string ProcessorReference { get; init; } = "";
}
```

- Usos típicos: contraseñas, tokens, secretos, datos de tarjetas.

**El ocultado gana sobre una plantilla que lo nombre.** `[Narrated]` y
`[OnError]` resuelven rutas `{param.Property}` sobre los argumentos
crudos, y una ruta que alcanza una propiedad ocultada se resuelve como
`[REDACTED]` — nunca su valor, nunca el marcador literal. Nombrar una ruta
nunca debilita las reglas que se aplican al valor directamente. Si
necesitas el valor en una narración, quita `[NotTraced]` de la propiedad;
esa eliminación es la decisión deliberada y revisable, y queda a la vista
en el diff.

## `[Traced]`

`.NET` conserva los nombres de parámetros en los metadatos por defecto,
así que — a diferencia de la JVM — rara vez lo necesitarás. Usa `[Traced]`
para **sobrescribir** posicionalmente los nombres de parámetros
capturados, por ejemplo para dar un nombre de dominio más claro que el
identificador del código fuente:

```csharp
public interface IMessageBus
{
    [Traced("messageId", "payload")]
    void Publish(string id, object body);
}
```

El parámetro `id` se captura como `messageId` y `body` como `payload`.
Poner menos nombres que parámetros es válido — las posiciones no listadas
conservan su nombre reflejado.

## `[NarrativeSummary]`

Proporciona un resumen breve y cuidado para el renderizado de valores.
Aplícalo a un **método sin parámetros o propiedad** público; su resultado
(vía `ToString()`) se usa siempre que se renderice una instancia del tipo.

```csharp
public sealed record Customer(string Id, string Name, CustomerTier Tier)
{
    [NarrativeSummary]
    public string Summary => $"Customer[id={Id}, tier={Tier}]";
}
```

- `ValueRenderer` busca un miembro `[NarrativeSummary]` antes de recurrir
  al renderizado de records, a la introspección reflexiva de propiedades y
  campos o a `ToString()`.
- El miembro debe ser público y no tomar parámetros.
- El atributo se **hereda** — el resumen de un tipo base se aplica a los
  tipos derivados salvo que se sobrescriba.
- Si al invocarlo lanza una excepción, se aplica el respaldo normal del
  renderizador. Esto aflora allí donde se rendericen valores (todos los
  renderizadores y exportadores).

## El contrato de pureza — efectos secundarios durante el tracing

NarrativeTrace puede invocar un conjunto pequeño y fijo de caminos de
código sobre tus objetos mientras renderiza una traza. Mantén esos miembros
**puros** — sin efectos secundarios como carga perezosa, contadores de
acceso, poblado de caché o E/S — exactamente como harías para un depurador
o un serializador. Esto importa más en .NET que en otras plataformas
porque el estado idiomático en C# vive detrás de *propiedades*, y el getter
de una propiedad es un método: leerlo puede ejecutar código arbitrario.

Qué se invoca durante el renderizado:

- **La introspección reflexiva lee propiedades y campos.** Un getter con
  efectos secundarios (un contador, una inicialización perezosa, un viaje a
  la base de datos) *se ejecutará* cuando se renderice una instancia sin un
  `ToString()` cuidado ni un miembro `[NarrativeSummary]`. Las .NET
  Framework Design Guidelines ya exigen que los getters no tengan efectos
  secundarios; NarrativeTrace se apoya en esa convención.
- También se invocan: un `ToString()` propio, un miembro
  `[NarrativeSummary]` y las rutas de propiedades nombradas en las
  plantillas de `[Narrated]`/`[OnError]` (`{order.Total}`).

Cómo se contiene la exposición:

- **Acotada** — los límites de longitud de cadena, de elementos de
  colección y de profundidad de introspección limitan cuánto código puede
  ejecutarse.
- **Aislada de excepciones** — un getter o un `ToString()` que lanza nunca
  hace fallar la llamada de negocio trazada; las plantillas recurren al
  `{marcador}` literal y el renderizado recurre a un marcador con el nombre
  del tipo.
- **Eager y determinista** — los valores se renderizan en el sitio de la
  llamada, así que cualquier efecto secundario ocurre una vez, en un
  momento predecible y en el hilo llamante.
- **Omitible** — `[NotTraced]` en una propiedad o campo significa que su
  valor no se lee en absoluto (se emite el marcador `[REDACTED]` en su
  lugar); un `ToString()`/`[NarrativeSummary]` cuidado tiene precedencia
  sobre la introspección, así que controlas exactamente a qué se accede; y
  con `TracingLevel.Off` (y para los valores de parámetros con `Summary`)
  no se renderiza nada en absoluto.

Si un miembro no puede ser puro, márcalo con `[NotTraced]` u ocúltalo tras
un resumen cuidado. No cuentes con que el tracing esté desactivado.

## Ejemplo completo

```csharp
public interface ITransferService
{
    [Narrated("Transferring {amount} from {fromAccountId} to {toAccountId}")]
    [OnError("Transfer rejected for source account {fromAccountId}",
             ExceptionType = typeof(InvalidOperationException))]
    TransferResult Transfer(
        string fromAccountId,
        string toAccountId,
        decimal amount,
        [NotTraced] string authToken);
}
```

Un solo método que combina narración, contexto de error puntual y
ocultación de parámetros.

## Erratas en las plantillas

Las plantillas de `[Narrated]` y `[OnError]` usan marcadores `{paramName}`
y `{paramName.Property}`. Un marcador que no coincide con ningún parámetro
se deja literal en el texto resuelto (`{custmerId}` sobrevive), y una
propiedad desconocida se renderiza como `{propertyName}`.

**Una propiedad ocultada se resuelve como `[REDACTED]`** — ni a su valor,
ni al marcador literal. Ambas mitades de la regla de ocultado se aplican:
`[NotTraced]` sobre la propiedad (incluido un parámetro posicional de
registro), y la lista de denegación basada en nombres sobre una propiedad
que ningún atributo cubre. Un marcador que no nombra ningún parámetro, o
una propiedad que su dueño no declara, se sigue conservando literalmente —
eso es una errata de escritura, no hay ningún valor detrás.

El fixture de xUnit y la clase base de NUnit escanean cada traza capturada
después de una prueba e imprimen como advertencias los marcadores que
sobrevivan, así que una errata en una plantilla aflora durante las pruebas:

```
WARNING: Unresolved template placeholder(s) detected:
  - OrderService.PlaceOrder: {custmerId} in narration
```

Usa `TemplateWarningCollector.Collect(tree)` para ejecutar tú mismo el
mismo escaneo.

## Véase también

- [Guía de instalación](guia-de-instalacion.md) — paquetes y vías de integración
- [Guía de configuración](guia-de-configuracion.md) — política de ocultación y niveles
- [Guía de claridad](guia-de-claridad.md) — puntuar nombres limpios (la primera línea de defensa)
