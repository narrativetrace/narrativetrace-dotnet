<!-- source: documentation/guides/annotations.md blob 8e8081d83657 | translated: 2026-09-18 | reviewed: - -->
# NarrativeTrace .NET — Guia de atributos

[English](../annotations.md) | [Español](../es/guia-de-atributos.md) | **Português** | [简体中文](../zh-CN/特性指南.md)

NarrativeTrace segue a filosofia **O código é o log**: os nomes de
métodos, os nomes de parâmetros e os valores de retorno já deveriam
contar a história da execução. Mantenha a lógica de negócio limpa e
expressiva primeiro, e só então recorra aos atributos de forma
*excepcional* — para narração pontual, contexto de erro, ocultação ou
renderização de valores personalizada.

Os cinco atributos narrativos vivem em `NarrativeTrace.Core.Annotation` —
um único `using` para todos eles, e o mesmo pacote do qual já vem seu
modelo de trace. São metadados puros: o interceptor `DispatchProxy`
(`NarrativeTrace.Proxy`) lê `[Narrated]`, `[OnError]` e `[NotTraced]` nas
chamadas que intercepta, enquanto `[NarrativeSummary]`, `[NarrativeElements]`
e `[NotTraced]` são respeitados em qualquer lugar onde valores sejam
renderizados. `[Traced]` é a única exceção — um marcador específico do
`DispatchProxy`, então ele permanece em `NarrativeTrace.Proxy`.

```csharp
using NarrativeTrace.Core.Annotation;
```

## Inventário

| Atributo | Namespace | Destino | Finalidade |
|---|---|---|---|
| `[NarrativeSummary]` | `NarrativeTrace.Core.Annotation` | Método ou propriedade | Renderização de resumo preferencial para o tipo declarante. |
| `[NarrativeElements]` | `NarrativeTrace.Core.Annotation` | Classe, struct | Declara os próprios elementos de um tipo não pertencente à plataforma como seguros para enumerar durante a renderização. |
| `[Narrated]` | `NarrativeTrace.Core.Annotation` | Método | Adiciona texto de narração legível a um método rastreado. |
| `[OnError]` | `NarrativeTrace.Core.Annotation` | Método (repetível) | Anexa texto de erro contextual a um método. |
| `[NotTraced]` | `NarrativeTrace.Core.Annotation` | Parâmetro, propriedade, campo (em um método compila mas é rejeitado ao criar o proxy) | Oculta um valor na saída do trace, incluindo membros de objetos introspeccionados. |
| `[Traced]` | `NarrativeTrace.Proxy` | Método | Sobrescreve posicionalmente os nomes de parâmetros capturados. |

## `[Narrated]`

Use `[Narrated]` quando quiser uma frase explícita no trace, em vez de
depender apenas do nome do método e dos parâmetros.

```csharp
public interface IOrderService
{
    [Narrated("Placing order of {quantity} units for customer {customerId}")]
    OrderResult PlaceOrder(string customerId, string productId, int quantity);
}
```

- Os marcadores usam **nomes de parâmetros**: `{customerId}`.
- Um nível de acesso a propriedades é suportado com membros em
  **PascalCase**: `{customer.Name}` lê a propriedade `Name` do argumento
  `customer`.
- Um marcador desconhecido é deixado **literal** na saída (`{custmerId}`
  sobrevive) — um sinal embutido de erro de digitação.

**Onde aparece:** a narração é renderizada por `MarkdownRenderer` e (em
nós bem-sucedidos) `ProseRenderer`. Está sempre disponível
programaticamente como `node.Signature.Narration`.

## `[OnError]`

Anexe texto específico de contexto para exceções. O atributo é
**repetível**; defina `ExceptionType` para restringir um template a um
tipo de exceção (o padrão é `typeof(Exception)`).

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

Como funciona no .NET:

- O template é **resolvido quando a exceção é lançada**, usando as
  mesmas regras de marcadores do `[Narrated]`, e é armazenado como
  `node.Signature.ErrorContext`.
- Apenas os atributos cujo `ExceptionType` declarado **corresponde à
  exceção lançada** (`IsInstanceOfType`) competem; entre as
  correspondências, vence o tipo declarado mais específico. Um
  `[OnError("…")]` isolado (implicitamente `typeof(Exception)`) corresponde
  a tudo.
- Se **nenhum** tipo declarado corresponder à exceção lançada, nenhum
  contexto de erro é anexado. Um método que retorna normalmente nunca
  carrega um contexto de erro.

**Onde aparece:** em um nó que lança exceção, o contexto de erro
resolvido é renderizado por `MarkdownRenderer` e `ProseRenderer` (após a
exceção), e está sempre disponível como `node.Signature.ErrorContext`. O
`IndentedTextRenderer`, que mostra apenas estrutura, não o exibe.

## `[NotTraced]`

Marque um **parâmetro, propriedade ou campo** sensível para que seu
valor seja ocultado em todos os lugares onde o trace for renderizado ou
exportado. O nome permanece visível; o valor é substituído por
`[REDACTED]`.

```csharp
public interface IAuthService
{
    Session Login(string username, [NotTraced] string password);
}
```

Segredos **aninhados dentro de um objeto rastreado** são cobertos de
duas formas:

- **Lista de negação baseada em nome** — a renderização reflexiva oculta
  automaticamente nomes de membros sensíveis comuns (`password`, `token`,
  `cvv`, …) via `RedactionPolicy` (consulte o
  [Guia de configuração](guia-de-configuracao.md#6-ocultação)).
- **`[NotTraced]` no membro** — para segredos cujos nomes não
  correspondem a nenhum padrão, anote a propriedade, o campo ou o parâmetro
  posicional do record; o renderizador substitui o marcador durante a
  introspecção, independentemente da lista de negação (isso vale mesmo sob
  `RedactionPolicy.Disabled`):

```csharp
public sealed record Card(string Last4, [NotTraced] string Pan);

public sealed class Payment
{
    public decimal Amount { get; init; }

    [NotTraced]
    public string ProcessorReference { get; init; } = "";
}
```

- Usos típicos: senhas, tokens, segredos, dados de cartão.

**Não é válido em um método inteiro.** `[NotTraced]` não tem o significado
de "a chamada inteira fica oculta" — ele sempre nomeia um parâmetro, uma
propriedade ou um componente de record, nunca o método em si. Colocá-lo
em um método compila, mas um proxy criado sobre uma interface que o faz
lança `InvalidOperationException` no momento da criação do proxy *(desde
0.1.5, não publicado)*, nomeando o atributo, o método afetado e a correção:

```csharp
public interface IAccountService
{
    [NotTraced] // lança em NarrativeTraceProxy.Create/.Create<T>
    void Authenticate(string user, string password);
}
```

```
[NotTraced] is not supported on a method (found on IAccountService.Authenticate);
it has no whole-call meaning here. Annotate each sensitive parameter, property,
or record component individually instead.
```

Anote o parâmetro `password` diretamente em vez disso.

**A ocultação vence sobre um template que a nomeia.** `[Narrated]` e
`[OnError]` resolvem caminhos `{param.Property}` sobre os argumentos
brutos, e um caminho que alcança uma propriedade oculta é resolvido como
`[REDACTED]` — nunca o seu valor, nunca o marcador literal. Nomear um
caminho nunca enfraquece as regras que se aplicam diretamente ao valor. Se
você precisar do valor em uma narrativa, remova `[NotTraced]` da
propriedade — essa remoção é a decisão deliberada e revisável, e aparece
no diff.

**Os templates respeitam a própria política de ocultação do proxy**
*(since 0.1.5)*: um template `[Narrated]`/`[OnError]`
resolvido em um proxy construído com um `new ProxyOptions(Redaction: ...)`
personalizado consulta essa mesma política nos dois eixos acima — um
placeholder simples como `{password}` e um caminho como
`{policy.HolderName}` igualmente — em vez de sempre recorrer a
`RedactionPolicy.Default`. `[NotTraced]` continua vencendo
independentemente da política. Deixe `Redaction` sem definir e os
templates se comportam exatamente como antes.

## `[Traced]`

O `.NET` mantém os nomes de parâmetros nos metadados por padrão, então
você raramente vai precisar disso. Use
`[Traced]` para **sobrescrever** posicionalmente os nomes de parâmetros
capturados, por exemplo para dar um nome de domínio mais claro do que o
identificador do código-fonte:

```csharp
public interface IMessageBus
{
    [Traced("messageId", "payload")]
    void Publish(string id, object body);
}
```

O parâmetro `id` é capturado como `messageId`, e `body` como `payload`.
Ter menos nomes do que parâmetros é aceitável — as posições não listadas
mantêm seu nome refletido.

## `[NarrativeSummary]`

Forneça um resumo curto e cuidadosamente elaborado para a renderização
de valores. Aplique-o a um **método ou propriedade público sem
parâmetros**; seu resultado (via `ToString()`) é usado sempre que uma
instância do tipo for renderizada.

```csharp
public sealed record Customer(string Id, string Name, CustomerTier Tier)
{
    [NarrativeSummary]
    public string Summary => $"Customer[id={Id}, tier={Tier}]";
}
```

- `ValueRenderer` procura um membro `[NarrativeSummary]` antes de
  recorrer à renderização de records, à introspecção reflexiva de
  propriedades e campos, ou a `ToString()`.
- O membro deve ser público e não receber parâmetros.
- O atributo é **herdado** — o resumo de um tipo base se aplica aos
  tipos derivados, a menos que seja sobrescrito.
- Se a invocação lançar uma exceção, aplica-se o fallback normal do
  renderizador. Isso se manifesta em todos os lugares onde valores são
  renderizados (todos os renderizadores e exportadores).

## `[NarrativeElements]`

A renderização lê o **estado** de um valor, nunca seu comportamento (veja
[o contrato de pureza](#o-contrato-de-pureza--efeitos-colaterais-durante-o-tracing)
abaixo) — o que significa que uma coleção só é percorrida quando seu tipo em
tempo de execução é um tipo de **plataforma** (`List<T>`, `Dictionary<K,V>`,
um array, …): um `IEnumerable`/`IEnumerable<T>` feito à mão nunca é
enumerado, porque seu próprio `GetEnumerator()` é código no qual o
renderizador não confia por padrão. Duas exceções preservam a riqueza sem
executar comportamento. Uma subclasse do usuário de uma coleção de
plataforma é percorrida através do **estado próprio do ancestral de
plataforma** (os campos internos de `List<T>`), nunca a sobrescrita da
subclasse — nada a declarar, isso é automático. Um tipo que não é em
absoluto uma subclasse de coleção de plataforma — uma `Collection`/
`IEnumerable` implementada do zero — pode voltar a ser habilitado com
`[NarrativeElements]`:

```csharp
[NarrativeElements]
public sealed class RecentOrders : IEnumerable<Order>
{
    private readonly List<Order> _orders = new();

    public IEnumerator<Order> GetEnumerator() => _orders.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

- **A declaração é a promessa de pureza do autor.** `[NarrativeElements]`
  garante que `GetEnumerator()` (e o enumerador que ele retorna) é uma
  leitura de estado pura — sem carregamento tardio, sem contadores, sem
  E/S — a mesma promessa que `[NarrativeSummary]` e o `ToString()` de uma
  folha sem estado já carregam como os outros dois ganchos de renderização
  sancionados. Não existe nenhum atributo que habilite um iterador *não
  confiável*; a promessa é o que sustenta a garantia.
- **O não declarado permanece não declarado.** Sem o atributo, um
  `IEnumerable` feito do zero é renderizado como seu nome de tipo e, quando
  gratuito obtê-la, sua contagem de elementos — nunca seus elementos, e
  `GetEnumerator()` nunca é chamado.
- **Percorrido exatamente como uma coleção de plataforma uma vez
  declarado** — sob a guarda de renderização, limitado pelo mesmo teto de
  elementos (`RenderOptions.MaxArrayItems`) de qualquer outro percurso de
  coleção, e um enumerador que lança degrada para o marcador de falha
  tipado como qualquer outro gancho, em vez de se propagar.
- **Não é herdado.** Aplique-o ao tipo que de fato declara o iterador; um
  tipo derivado que não adiciona lógica de enumeração própria só precisa da
  sua própria declaração se ainda não for uma subclasse de coleção de
  plataforma ou um ancestral anotado.

## O contrato de pureza — efeitos colaterais durante o tracing

O NarrativeTrace pode invocar um pequeno conjunto fixo de caminhos de
código nos seus objetos ao renderizar um trace. Mantenha esses membros
**puros** — livres de efeitos colaterais como carregamento tardio (lazy
loading), contadores de acesso, preenchimento de cache ou E/S — exatamente
como você faria para um depurador ou um serializador. Isso importa mais no
.NET do que em outras plataformas porque o estado idiomático em C# vive
por trás de *propriedades*, e o getter de uma propriedade é um método:
lê-lo pode executar código arbitrário.

**A renderização lê estado, nunca executa comportamento.** Os valores vêm
de campos e — para uma propriedade automática ou um componente de record —
do campo interno gerado pelo compilador, lido diretamente; o *corpo* do
getter de uma propriedade nunca é invocado pela renderização, então um
getter hostil ou simplesmente descuidado (um contador, um efeito colateral
que preenche um cache) não pode ser observado através do tracing de forma
alguma. O único código do usuário que a renderização chega a executar são
três ganchos documentados, cada um sob a guarda de renderização: um membro
`[NarrativeSummary]`, o `ToString()` próprio de uma folha sem estado e — o
mais novo dos três — o `GetEnumerator()` próprio de um tipo declarado
`[NarrativeElements]`. Tudo abaixo desta linha descreve esses três ganchos
e suas garantias; não há um quarto.

O que é invocado durante a renderização:

- A introspecção reflexiva lê **campos internos**, não os getters das
  propriedades — um getter com um efeito colateral (um contador, uma
  inicialização tardia, uma ida e volta ao banco de dados) nunca é
  executado apenas porque uma instância é renderizada.
- Também são invocados, cada um sob a guarda de renderização: um membro
  `[NarrativeSummary]`, o `ToString()` próprio de uma folha sem estado, o
  `GetEnumerator()` próprio de um tipo declarado `[NarrativeElements]`, e
  caminhos de propriedade nomeados em templates de `[Narrated]`/
  `[OnError]` (`{order.Total}`) — que igualmente se resolvem contra essas
  mesmas leituras de campos internos, não contra o corpo do getter.

Como a exposição é contida:

- **Limitada** — limites de comprimento de string, de itens de coleção e
  de profundidade de introspecção restringem quanto código pode ser
  executado.
- **Isolada de exceções** — um getter ou `ToString()` que lança exceção
  nunca faz a chamada de negócio rastreada falhar; os templates recorrem ao
  `{marcador}` literal, e a renderização recorre a um marcador com o nome
  do tipo.
- **Antecipada (eager) e determinística** — os valores são renderizados
  no local da chamada, então qualquer efeito colateral acontece uma única
  vez, em um momento previsível, na thread chamadora.
- **Evitável** — `[NotTraced]` em uma propriedade ou campo significa que
  seu valor nunca é lido (o marcador `[REDACTED]` é emitido em vez disso);
  um `ToString()`/`[NarrativeSummary]` cuidadosamente elaborado tem
  precedência sobre a introspecção, então você controla exatamente o que é
  acessado; e em `TracingLevel.Off` (e para valores de parâmetro em
  `Summary`) nenhuma renderização acontece.

Se um membro não puder ser puro, marque-o com `[NotTraced]` ou o
esconda atrás de um resumo cuidadosamente elaborado. Não conte com o
tracing estar desativado.

## Exemplo completo

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

Um único método, combinando narração, contexto de erro pontual e
ocultação de parâmetro.

## Erros de digitação em templates

Templates de `[Narrated]` e `[OnError]` usam marcadores `{paramName}` e
`{paramName.Property}`. Um marcador que não corresponde a nenhum parâmetro
é deixado literal no texto resolvido (`{custmerId}` sobrevive), e uma
propriedade desconhecida é renderizada como `{propertyName}`.

**Uma propriedade oculta é resolvida como `[REDACTED]`** — não para o
seu valor, e não para o marcador literal. As duas metades da regra de
ocultação se aplicam: `[NotTraced]` na propriedade (incluindo um parâmetro
posicional de record), e a lista de negação baseada em nome em uma
propriedade que nenhum atributo cobre. Um marcador que não nomeia nenhum
parâmetro, ou uma propriedade que seu proprietário não declara, ainda é
preservado literalmente — isso é um erro de digitação de autoria, não há
nenhum valor por trás dele.

O fixture do xUnit e a classe base do NUnit escaneiam cada trace
capturado após um teste e imprimem quaisquer marcadores sobreviventes como
avisos, então um erro de digitação em um template aflora durante os
testes:

```
WARNING: Unresolved template placeholder(s) detected:
  - OrderService.PlaceOrder: {custmerId} in narration
```

Use `TemplateWarningCollector.Collect(tree)` para executar você mesmo a
mesma varredura.

## Veja também

- [Guia de instalação](guia-de-instalacao.md) — pacotes e caminhos de integração
- [Guia de configuração](guia-de-configuracao.md) — política e níveis de ocultação
- [Guia de clareza](guia-de-clareza.md) — pontuação de nomes limpos (a primeira linha de defesa)
</content>
</invoke>
