<!-- source: documentation/privacy-and-redaction.md blob 07afc780d0bb | translated: 2026-09-11 | reviewed: - -->
# Privacidad y ocultación

[English](../privacy-and-redaction.md) | **Español** | [Português](../pt-BR/privacidade-e-ocultacao.md) | [简体中文](../zh-CN/隐私与脱敏.md)

NarrativeTrace se ejecuta dentro de tu proceso y escribe archivos que tu
equipo compartirá — artefactos de CI, salida de traza local, lo que sea
que tu pipeline de logging reenvíe. Esta página dice exactamente qué se
oculta y qué no, verificado contra el código de esta implementación (no
asumido, ni heredado de otra), para que puedas decidir si es seguro
para tus datos antes de conectarlo.

## Ocultación, superficie por superficie

Toda integración distribuida en esta implementación renderiza los valores de
parámetros y de retorno a través del mismo motor (`ValueRenderer`,
`NarrativeInterceptor`), que siempre resuelve a
`RedactionPolicy.Default` — ninguna de ellas expone un ajuste de
configuración para desactivarlo:

| Superficie | ¿Puede desactivar la ocultación integrada? | Por qué |
|---|---|---|
| `NarrativeTraceProxy.Create<T>` (captura cruda de `DispatchProxy`) | No | Renderiza todo argumento/valor de retorno sin ningún argumento `RenderOptions` — siempre `RedactionPolicy.Default`. |
| Auto-envoltura de DI (`AddNarrativeTracing`) | No | Envuelve con `NarrativeTraceProxy.Create` internamente; `NarrativeTracingDiOptions` no tiene ningún campo de ocultación. |
| Middleware de ASP.NET Core | No | `NarrativeTraceOptions` no tiene ningún campo de ocultación; las trazas vienen de cualquier vía de proxy que las produjo. |
| `NarrativeFixture` de xUnit | No | No hay parámetro `RedactionPolicy`/`RenderOptions` en ningún sitio del tipo. |
| `NarrativeTestBase` de NUnit | No | Misma forma que el fixture de xUnit. |
| Marcadores de plantilla de `[Narrated]` / `[OnError]` (`NarrationResolver`) | No | Clase completamente estática, con `RedactionPolicy.Default` fijado — consulta el hueco más estrecho más abajo. |
| Proyección JSON canónico / JSON estructural | N/D — nada que desactivar | Consume cadenas ya renderizadas (ya ocultadas); la proyección estructural además elide todo valor incondicionalmente. |
| Artefacto estructural `.nt` | N/D — no existen valores | `StructuralTraceRenderer` emite solo nombres, jerarquía y tipo de resultado, nunca un valor. |
| `dotnet-narrativetrace clarity-scan` | N/D — nunca lee valores | Solo reflexión sobre un `MetadataLoadContext`: nunca construye una instancia ni invoca nada, así que no hay ningún valor que ocultar. |
| Una llamada personalizada a `ValueRenderer.Render(value, options)` en **tu propio código** | Sí | La única vía de escape en la base de código: pasa tú mismo `new RenderOptions(Redaction: RedactionPolicy.Disabled)`. `[NotTraced]` sigue ocultando incluso entonces. |

Esa última fila es la única excepción honesta, y es deliberada, no un
descuido: `RedactionPolicy.Disabled` existe como primitiva de la
biblioteca (`RedactionPolicy.Disabled = new([], valueShapesEnabled: false)`),
pero ninguna integración distribuida la conecta. Alcanzarla significa
escribir tu propia llamada a `ValueRenderer.Render`/`RenderStructured` con
un `RenderOptions` explícito — un acto deliberado y revisable en tu propio
código fuente, nunca un flag o una variable de entorno.

## Qué atrapa la lista de denegación, y qué la supera en rango

`RedactionPolicy.Default` coincide con 26 patrones de nombre sin distinguir
mayúsculas de minúsculas, no el conjunto más corto "password, cvv, ssn,
token, secret, authorization" que podrías suponer con una lectura rápida
de las preguntas frecuentes:

```
password, passwd, secret, token, apikey, api_key, cvv, ssn, authorization,
credential, privatekey, private_key, cardnumber, card_number, jwt, cookie,
setcookie, set_cookie, sessionid, session_id, accountnumber, account_number,
routingnumber, routing_number, pan, iban
```

La coincidencia es por subcadena y deliberadamente sesgada hacia la
sobre-ocultación (un patrón de `token` también atrapa `apiTokenValue`) —
excepto `pan` e `iban`, que de otro modo ocultarían campos de negocio
ordinarios (`companyName`, `planId`, `spanCount`, `japaneseAddress`) como
subcadenas, así que esos dos se comparan por límites de token del
identificador en su lugar.

Con independencia del nombre del campo, un segundo eje reconoce tres
**formas de valor** y las oculta sin importar cómo se llame el campo: un
JWT (tres segmentos base64url que empiezan por `eyJ`), un número de
tarjeta de pago válido según Luhn de 13 a 19 dígitos, y una cadena con
forma de `Set-Cookie` HTTP. Por eso un campo llamado `data` o `note`
igualmente se oculta cuando resulta que contiene algo que parece un
número de tarjeta o un token bearer.

`[NotTraced]` en un parámetro, propiedad o campo siempre gana, con
independencia de la lista de denegación:

- En una propiedad/campo, el getter del valor **nunca se invoca en
  absoluto** — la comprobación de ocultación se ejecuta antes de que la
  reflexión toque el miembro, no después.
- En un parámetro de proxy, el argumento ya fue evaluado por quien llama
  (inevitable para una llamada de método real), pero NarrativeTrace en sí
  nunca lo renderiza — el marcador se sustituye antes de que
  `ValueRenderer` llegue a verlo.
- Supera en rango a un `ToString()` curado y a un método
  `[NarrativeSummary]` en el miembro *contenedor*: la ocultación se
  comprueba antes de alcanzar cualquiera de los dos.
- Las **claves** de diccionario pasan por la misma vía protegida que los
  valores — una clave con nombre sensible se oculta igual que lo haría una
  propiedad con nombre sensible, nunca renderizada vía un `ToString()` a
  secas.

El hueco más estrecho, ya conocido: la resolución de marcadores de
plantilla (`[Narrated]`/`[OnError]`) es una vía de código completamente
estática y siempre usa `RedactionPolicy.Default`. Si el código de la
aplicación alguna vez conecta una `RedactionPolicy` *personalizada* en
`ValueRenderer` directamente (la fila de vía de escape de arriba, en
sentido inverso — endureciendo en lugar de desactivar), esa política
personalizada se respeta en todas partes donde `ValueRenderer` se llama a
mano, pero **no** dentro de las plantillas `[Narrated]`/`[OnError]`, que
siguen usando la lista de denegación por defecto sin importar qué. Ninguna
integración distribuida conecta una política personalizada hoy en día, así
que esto solo importa si construyes directamente contra la API de
renderizado de `NarrativeTrace.Core` tú mismo.

## Límites y escapado

Todo valor renderizado tiene un tope y se sanea, con independencia de la
ocultación:

| Control | Por defecto |
|---|---|
| Longitud máxima de cadena | 200 caracteres, luego un sufijo estilo `"...(truncated)"` |
| Máximo de elementos de colección | 5, luego un resumen `(N total)` |
| Máximo de claves de objeto | 5 |
| Profundidad máxima de anidamiento | 4 |
| Detección de ciclos | Por identidad de referencia, independiente de la profundidad |
| Escapado de caracteres de control / inyección de log | Los caracteres de control y los surrogates sin pareja se escapan como `\uXXXX` (las comillas/barras invertidas son cosa del serializador, no del renderizador) |

## Garantías

- **La ocultación es incondicional en toda integración distribuida.**
  `[NotTraced]` y la lista de denegación de 26 patrones (más las tres
  formas de valor) se aplican a toda vía de salida que esta implementación
  distribuye — captura por proxy, auto-envoltura de DI, middleware de
  ASP.NET Core, salida de pruebas de xUnit/NUnit, marcadores de plantilla y
  todo formato de exportación. Ningún flag, variable de entorno o
  propiedad de MSBuild los desactiva.
- **Los artefactos estructurales no llevan ningún valor en tiempo de
  ejecución.** Tanto el artefacto de texto `.nt` como el array de entradas
  opcional `.structural.json` eliminan todo valor de parámetro, valor de
  retorno y mensaje de excepción — una prueba de propiedades siembra
  contenido hostil en cada campo de valor de una entrada capturada y falla
  si algo de eso sobrevive a la proyección.
- **El propio `ToString()` de un valor nunca es de confianza una vez que su
  tipo tiene estado público.** Un record o una clase que expone al menos
  una propiedad o campo público siempre se renderiza reflexionando sobre
  esos miembros a través de la misma vía comprobada por ocultación — un
  miembro en la lista de denegación o `[NotTraced]` se sustituye por el
  marcador *antes* de que nada lea su valor, y el propio `ToString()` del
  tipo nunca se invoca para producir la salida, a cualquier profundidad de
  anidamiento. Un `ToString()` escrito a mano que interpola un campo en la
  lista de denegación directamente (`return "Account[password=" + password
  + "]";`) no puede saltarse esto: en su lugar, la reflexión renderiza el
  objeto, y ese texto escrito a mano nunca se alcanza. La única vía de
  entrada más allá de la reflexión es `[NarrativeSummary]` en un miembro
  que tú mismo nombraste — consulta la no garantía más abajo. Un tipo sin
  ningún miembro público en absoluto (nada que recorrer) es el único caso
  en el que se usa su propio `ToString()`, y aun así el resultado se sanea
  y se limita en longitud como cualquier otra cadena.
- **El renderizado no puede hacer fallar tu aplicación.** La captura y el
  renderizado están aislados de excepciones en los tres puntos que
  alcanzan código escrito por quien llama: un `ToString()` personalizado
  que lanza, un miembro `[NarrativeSummary]` que lanza, y un getter de
  propiedad o campo que lanza al ser alcanzado por la introspección
  reflexiva (incluido uno nombrado en una plantilla). Cada uno degrada
  *esa única parte* a un marcador de posición tipado `<error: TypeName>`
  — el nombre del propio tipo de la excepción capturada, p. ej.
  `<error: InvalidOperationException>`, nunca su `.Message` (un mensaje
  puede llevar consigo el mismísimo valor que el renderizado intentaba
  proteger) — sin abortar la llamada, la colección o la traza. Un
  `[NarrativeSummary]` que lanza degrada de la misma manera en lugar de
  recaer en los campos del tipo: el resumen se curó precisamente para que
  los campos no se mostraran en crudo. Esto es una protección de
  `try`/`catch`, no una protección de profundidad de pila — una
  `StackOverflowException` de un `ToString()` que recurre es un fallo no
  gestionado del CLR que ningún manejador administrado puede capturar, así
  que nunca se alcanza por esta vía. Lo que realmente detiene a un tipo
  que recurre es el tope `MaxDepth` de la reflexión y su protección de
  ciclos por identidad de referencia, que es también la razón por la que
  el propio `ToString()` de un tipo solo se invoca para un valor hoja sin
  miembros públicos que recorrer, nunca para el compuesto que está
  recurriendo. Un enumerador o diccionario hostil es un riesgo aparte,
  igualmente protegido pero fuera de esos tres puntos de entrada
  nombrados, y degrada a un `<error>` a secas en lugar de la forma tipada.
- **El uso de recursos está acotado.** La longitud, el tamaño de colección,
  el ancho de objeto y la profundidad de anidamiento están todos limitados,
  con detección de ciclos por identidad de referencia independiente de la
  profundidad.
- **La salida no se puede falsificar.** Los caracteres de control y los
  surrogates sin pareja se escapan antes de que un valor renderizado
  llegue a un archivo, así que un valor hostil no puede inyectar una línea
  de log falsa ni corromper la sintaxis de Markdown/JSON/diagrama.
- **Los artefactos de prueba caen en un lugar efímero, no en el control de
  versiones.** Las integraciones de prueba de xUnit y NUnit escriben, por
  defecto, en `TestResults/narrativetrace/` bajo el proyecto de pruebas —
  la misma ubicación que `dotnet test`/Visual Studio/Rider ya tratan como
  salida desechable, y que el propio `.gitignore` de este repositorio
  excluye. Define `NARRATIVETRACE_OUTPUT=false` para dejar de escribir del
  todo, o `NARRATIVETRACE_OUTPUT_DIR` para redirigirla; consulta
  [Qué incluir en el commit](que-incluir-en-el-commit.md) para saber qué
  hay dentro y si algo de eso pertenece a tu propio repositorio.

## No garantías

- **Ninguna integración distribuida te deja desactivar la ocultación.** La
  única vía de escape es código de aplicación que construye su propia
  llamada a `ValueRenderer.Render` con `RedactionPolicy.Disabled` — un acto
  deliberado en tu propio código fuente, nunca un estado de configuración.
  `[NotTraced]` sigue ocultando incluso bajo `Disabled`.
- **Los marcadores de plantilla no respetan una `RedactionPolicy`
  personalizada.** La resolución de `[Narrated]`/`[OnError]` siempre usa la
  lista de denegación por defecto, incluso si has conectado una política
  personalizada en `ValueRenderer` en otro sitio.
- **La detección es por nombre y por forma, no estadística.** No hay
  heurística de entropía ni de "parece aleatorio" — un secreto en un campo
  con nombre inocuo y una forma no reconocida no se atrapa. Esto es una red
  de seguridad, no una garantía de que se encuentre todo secreto.
- **Un miembro `[NarrativeSummary]` es código de confianza, una vez que lo
  has nombrado.** Esta es la única excepción deliberada a la garantía de
  arriba: anotar un miembro saca a ese tipo por completo de la reflexión, y
  el renderizador muestra solo lo que devuelve el resumen — a diferencia de
  un `ToString()` simple (nunca de confianza para un tipo con estado
  público), un miembro `[NarrativeSummary]` *sí* es de confianza,
  precisamente porque nombrarlo fue un acto deliberado en tu propio código
  fuente. Si el propio resumen devuelve de vuelta un campo, eso es el
  resumen que tú escribiste eligiendo exponerlo, el mismo modelo de
  confianza que un `ToString()` escrito a mano que leerías en un depurador
  — no un hueco que introdujo el renderizador. Si en cambio lanza, el
  valor degrada al marcador tipado `<error: TypeName>`, nunca una fuga de
  lo que habría mostrado. Anota el *miembro* con `[NotTraced]` en su lugar
  si no se puede confiar en el resumen propio de un tipo con un campo.
- **Ningún bucle de comparación con línea base en producción vuelve a leer
  el artefacto estructural todavía.** El archivo `.nt` es determinista y
  sin valores por construcción, pero esta implementación no distribuye nada que lo
  compare con una ejecución anterior (consulta
  [Qué incluir en el commit](que-incluir-en-el-commit.md)).

## Lo que esta página no cubre

- El contrato completo de atributos (`[Narrated]`, `[OnError]`,
  `[NotTraced]`, `[NarrativeSummary]`, y las expectativas de pureza sobre
  lo que invocan) — consulta la
  [Guía de atributos](../guides/es/guia-de-atributos.md).
- Configuración programática de niveles de tracing y salida — consulta la
  [Guía de configuración](../guides/es/guia-de-configuracion.md).
- Qué ocurre cuando el propio proxy de NarrativeTrace se apila con el
  proxy o interceptor de otra biblioteca — consulta las
  [preguntas frecuentes](../../README.md#faq) del README raíz (en inglés).
