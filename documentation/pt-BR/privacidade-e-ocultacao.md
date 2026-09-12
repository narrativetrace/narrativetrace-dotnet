<!-- source: documentation/privacy-and-redaction.md blob 07afc780d0bb | translated: 2026-09-11 | reviewed: - -->
# Privacidade e ocultação

[English](../privacy-and-redaction.md) | [Español](../es/privacidad-y-ocultacion.md) | **Português** | [简体中文](../zh-CN/隐私与脱敏.md)

O NarrativeTrace roda dentro do seu processo e escreve arquivos que sua
equipe vai compartilhar — artefatos de CI, saída de trace local, o que
quer que seu pipeline de logging encaminhe. Esta página diz exatamente o
que é e o que não é ocultado, verificado contra o código desta implementação
(não presumido, nem herdado de outra), para que você possa
decidir se é seguro para seus dados antes de conectá-lo.

## Ocultação, superfície por superfície

Toda integração distribuída nesta implementação renderiza valores de parâmetros e
de retorno através do mesmo motor (`ValueRenderer`,
`NarrativeInterceptor`), que sempre resolve para
`RedactionPolicy.Default` — nenhuma delas expõe um botão de configuração
para desativá-la:

| Superfície | Pode desativar a ocultação embutida? | Por quê |
|---|---|---|
| `NarrativeTraceProxy.Create<T>` (captura crua do `DispatchProxy`) | Não | Renderiza todo argumento/valor de retorno sem nenhum argumento `RenderOptions` — sempre `RedactionPolicy.Default`. |
| Encapsulamento automático de DI (`AddNarrativeTracing`) | Não | Encapsula com `NarrativeTraceProxy.Create` internamente; `NarrativeTracingDiOptions` não tem nenhum campo de ocultação. |
| Middleware do ASP.NET Core | Não | `NarrativeTraceOptions` não tem nenhum campo de ocultação; as traces vêm de qualquer caminho de proxy que as produziu. |
| `NarrativeFixture` do xUnit | Não | Não há parâmetro `RedactionPolicy`/`RenderOptions` em lugar nenhum do tipo. |
| `NarrativeTestBase` do NUnit | Não | Mesma forma do fixture do xUnit. |
| Marcadores de template de `[Narrated]` / `[OnError]` (`NarrationResolver`) | Não | Classe totalmente estática, fixada em `RedactionPolicy.Default` — veja a lacuna mais estreita abaixo. |
| Projeção JSON canônico / JSON estrutural | N/D — nada para desativar | Consome strings já renderizadas (já ocultadas); a projeção estrutural também elide todo valor incondicionalmente. |
| Artefato estrutural `.nt` | N/D — não existem valores | `StructuralTraceRenderer` emite apenas nomes, hierarquia e tipo de resultado, nunca um valor. |
| `dotnet-narrativetrace clarity-scan` | N/D — nunca lê valores | Apenas reflexão sobre um `MetadataLoadContext`: nunca constrói uma instância nem invoca nada, então não há valor algum para ocultar. |
| Uma chamada personalizada a `ValueRenderer.Render(value, options)` no **seu próprio código** | Sim | A única válvula de escape na base de código: passe você mesmo `new RenderOptions(Redaction: RedactionPolicy.Disabled)`. `[NotTraced]` ainda oculta mesmo assim. |

Essa última linha é a única exceção honesta, e é deliberada, não um
descuido: `RedactionPolicy.Disabled` existe como primitiva da biblioteca
(`RedactionPolicy.Disabled = new([], valueShapesEnabled: false)`), mas
nenhuma integração distribuída a conecta. Alcançá-la significa escrever
sua própria chamada a `ValueRenderer.Render`/`RenderStructured` com um
`RenderOptions` explícito — um ato deliberado e revisável no seu próprio
código-fonte, nunca uma flag ou variável de ambiente.

## O que a lista de negação captura, e o que a supera em prioridade

`RedactionPolicy.Default` corresponde a 26 padrões de nome sem
diferenciar maiúsculas de minúsculas, não o conjunto mais curto
"password, cvv, ssn, token, secret, authorization" que você poderia
supor com uma leitura rápida do FAQ:

```
password, passwd, secret, token, apikey, api_key, cvv, ssn, authorization,
credential, privatekey, private_key, cardnumber, card_number, jwt, cookie,
setcookie, set_cookie, sessionid, session_id, accountnumber, account_number,
routingnumber, routing_number, pan, iban
```

A correspondência é por substring e deliberadamente enviesada para
super-ocultação (um padrão de `token` também captura `apiTokenValue`) —
exceto `pan` e `iban`, que de outra forma ocultariam campos de negócio
comuns (`companyName`, `planId`, `spanCount`, `japaneseAddress`) como
substrings, então esses dois são comparados por limites de token do
identificador em vez disso.

Independentemente do nome do campo, um segundo eixo reconhece três
**formatos de valor** e os oculta não importa como o campo se chame: um
JWT (três segmentos base64url começando com `eyJ`), um número de cartão
de pagamento válido pelo algoritmo de Luhn com 13 a 19 dígitos, e uma
string no formato de `Set-Cookie` HTTP. É por isso que um campo chamado
`data` ou `note` ainda é ocultado quando ele contém algo que se parece
com um número de cartão ou um token bearer.

`[NotTraced]` em um parâmetro, propriedade ou campo sempre vence,
independentemente da lista de negação:

- Em uma propriedade/campo, o getter do valor **nunca é invocado**  — a
  verificação de ocultação roda antes de a reflexão tocar o membro, não
  depois.
- Em um parâmetro de proxy, o argumento já foi avaliado por quem chamou
  (inevitável para uma chamada de método real), mas o próprio
  NarrativeTrace nunca o renderiza — o marcador é substituído antes de o
  `ValueRenderer` sequer vê-lo.
- Ele supera em prioridade um `ToString()` curado e um método
  `[NarrativeSummary]` no membro *contêiner*: a ocultação é verificada
  antes de qualquer um dos dois ser alcançado.
- **Chaves** de dicionário passam pelo mesmo caminho protegido que os
  valores — uma chave com nome sensível é ocultada da mesma forma que
  uma propriedade com nome sensível seria, nunca renderizada via um
  `ToString()` puro.

A lacuna mais estreita, já conhecida: a resolução de marcadores de
template (`[Narrated]`/`[OnError]`) é um caminho de código totalmente
estático e sempre usa `RedactionPolicy.Default`. Se o código da
aplicação algum dia conectar uma `RedactionPolicy` *personalizada* no
`ValueRenderer` diretamente (a linha da válvula de escape acima, ao
contrário — endurecendo em vez de desativar), essa política
personalizada é respeitada em todo lugar onde `ValueRenderer` é chamado
manualmente, mas **não** dentro dos templates `[Narrated]`/`[OnError]`,
que continuam usando a lista de negação padrão independentemente disso.
Nenhuma integração distribuída conecta uma política personalizada hoje,
então isso só importa se você construir diretamente contra a API de
renderização do `NarrativeTrace.Core` você mesmo.

## Limites e escaping

Todo valor renderizado tem um teto e é sanitizado, independentemente da
ocultação:

| Controle | Padrão |
|---|---|
| Comprimento máximo de string | 200 caracteres, depois um sufixo no estilo `"...(truncated)"` |
| Máximo de itens de coleção | 5, depois um resumo `(N total)` |
| Máximo de chaves de objeto | 5 |
| Profundidade máxima de aninhamento | 4 |
| Detecção de ciclos | Por identidade de referência, independente da profundidade |
| Escaping de caracteres de controle / injeção de log | Caracteres de controle e surrogates sem par são escapados como `\uXXXX` (aspas/barras invertidas são responsabilidade do serializador, não do renderizador) |

## Garantias

- **A ocultação é incondicional em toda integração distribuída.**
  `[NotTraced]` e a lista de negação de 26 padrões (mais os três formatos
  de valor) se aplicam a todo caminho de saída que esta implementação distribui —
  captura por proxy, encapsulamento automático de DI, middleware do
  ASP.NET Core, saída de testes xUnit/NUnit, marcadores de template e
  todo formato de exportação. Nenhuma flag, variável de ambiente ou
  propriedade do MSBuild os desativa.
- **Os artefatos estruturais não carregam nenhum valor em tempo de
  execução.** Tanto o artefato de texto `.nt` quanto o array de entradas
  opcional `.structural.json` removem todo valor de parâmetro, valor de
  retorno e mensagem de exceção — um teste de propriedades semeia
  conteúdo hostil em todo campo de valor de uma entrada capturada e
  falha se algo disso sobrevive à projeção.
- **O próprio `ToString()` de um valor nunca é confiável assim que seu
  tipo tem estado público.** Um record ou uma classe que expõe pelo menos
  uma propriedade ou campo público é sempre renderizado refletindo sobre
  esses membros pelo mesmo caminho verificado pela ocultação — um membro
  na lista de negação ou `[NotTraced]` é substituído pelo marcador *antes*
  de qualquer coisa ler seu valor, e o próprio `ToString()` do tipo nunca
  é invocado para produzir a saída, em qualquer profundidade de
  aninhamento. Um `ToString()` escrito à mão que interpola um campo da
  lista de negação diretamente (`return "Account[password=" + password +
  "]";`) não consegue burlar isso: em vez disso, a reflexão renderiza o
  objeto, e aquele texto escrito à mão nunca é alcançado. A única via de
  entrada além da reflexão é `[NarrativeSummary]` em um membro que você
  mesmo nomeou — veja a não garantia abaixo. Um tipo sem nenhum membro
  público (nada para percorrer) é o único caso em que seu próprio
  `ToString()` é usado, e mesmo assim o resultado é sanitizado e limitado
  em comprimento como qualquer outra string.
- **A renderização não pode fazer sua aplicação falhar.** A captura e a
  renderização são isoladas de exceções nos três pontos que alcançam
  código escrito por quem chama: um `ToString()` personalizado que lança,
  um membro `[NarrativeSummary]` que lança, e um getter de propriedade ou
  campo que lança ao ser alcançado pela introspecção reflexiva (incluindo
  um nomeado em um template). Cada um degrada *essa única parte* para um
  marcador de posição tipado `<error: TypeName>` — o nome do próprio tipo
  da exceção capturada, ex. `<error: InvalidOperationException>`, nunca
  sua `.Message` (uma mensagem pode carregar o próprio valor que a
  renderização tentava proteger) — sem abortar a chamada, a coleção ou a
  trace. Um `[NarrativeSummary]` que lança degrada da mesma forma em vez
  de recair sobre os campos do tipo: o resumo foi curado precisamente
  para que os campos não fossem mostrados crus. Isso é uma proteção de
  `try`/`catch`, não uma proteção de profundidade de pilha — uma
  `StackOverflowException` de um `ToString()` que recorre é uma falha não
  gerenciada do CLR que nenhum manipulador gerenciado consegue capturar,
  então nunca é alcançada por este caminho. O que de fato impede um tipo
  que recorre é o limite `MaxDepth` da reflexão e sua proteção de ciclos
  por identidade de referência, que também é a razão pela qual o próprio
  `ToString()` de um tipo só é invocado para um valor folha sem membros
  públicos para percorrer, nunca para o composto que está recorrendo. Um
  enumerador ou dicionário hostil é um risco à parte, igualmente
  protegido mas fora desses três pontos de entrada nomeados, e degrada
  para um `<error>` simples em vez da forma tipada.
- **O uso de recursos é limitado.** Comprimento, tamanho de coleção,
  largura de objeto e profundidade de aninhamento são todos limitados,
  com detecção de ciclos por identidade de referência independente da
  profundidade.
- **A saída não pode ser forjada.** Caracteres de controle e surrogates
  sem par são escapados antes de um valor renderizado chegar a um
  arquivo, então um valor hostil não pode injetar uma linha de log falsa
  nem corromper a sintaxe de Markdown/JSON/diagrama.
- **Os artefatos de teste caem em um lugar efêmero, não no controle de
  versão.** As integrações de teste do xUnit e do NUnit escrevem, por
  padrão, em `TestResults/narrativetrace/` sob o projeto de teste — o
  mesmo local que `dotnet test`/Visual Studio/Rider já tratam como saída
  descartável, e que o próprio `.gitignore` deste repositório exclui.
  Defina `NARRATIVETRACE_OUTPUT=false` para parar de escrever
  completamente, ou `NARRATIVETRACE_OUTPUT_DIR` para redirecioná-la;
  veja [O que incluir no commit](o-que-incluir-no-commit.md) para saber o
  que há dentro e se algo disso pertence ao seu próprio repositório.

## Não garantias

- **Nenhuma integração distribuída permite que você desative a
  ocultação.** A única válvula de escape é código de aplicação que
  constrói sua própria chamada a `ValueRenderer.Render` com
  `RedactionPolicy.Disabled` — um ato deliberado no seu próprio
  código-fonte, nunca um estado de configuração. `[NotTraced]` ainda
  oculta mesmo sob `Disabled`.
- **Os marcadores de template não respeitam uma `RedactionPolicy`
  personalizada.** A resolução de `[Narrated]`/`[OnError]` sempre usa a
  lista de negação padrão, mesmo que você tenha conectado uma política
  personalizada no `ValueRenderer` em outro lugar.
- **A detecção é baseada em nome e formato, não estatística.** Não há
  heurística de entropia ou "parece aleatório" — um segredo em um campo
  com nome inocente e um formato não reconhecido não é capturado. Isso é
  uma rede de segurança, não uma garantia de que todo segredo será
  encontrado.
- **Um membro `[NarrativeSummary]` é código confiável, uma vez que você o
  nomeou.** Esta é a única exceção deliberada à garantia acima: anotar um
  membro tira aquele tipo completamente da reflexão, e o renderizador
  mostra apenas o que o resumo retorna — diferente de um `ToString()`
  simples (nunca confiável para um tipo com estado público), um membro
  `[NarrativeSummary]` *é* confiável, precisamente porque nomeá-lo foi um
  ato deliberado no seu próprio código-fonte. Se o próprio resumo devolver
  um campo, isso é o resumo que você escreveu escolhendo expô-lo, o mesmo
  modelo de confiança de um `ToString()` escrito à mão que você leria em
  um depurador — não uma brecha que o renderizador introduziu. Se em vez
  disso ele lançar, o valor degrada para o marcador tipado
  `<error: TypeName>`, nunca um vazamento do que ele mostraria. Anote o
  *membro* com `[NotTraced]` em vez disso se o resumo próprio de um tipo
  não puder ser confiado com um campo.
- **Nenhum loop de comparação com linha de base em produção lê o
  artefato estrutural de volta ainda.** O arquivo `.nt` é determinístico
  e sem valores por construção, mas esta implementação não distribui nada que o
  compare com uma execução anterior (veja
  [O que incluir no commit](o-que-incluir-no-commit.md)).

## O que esta página não cobre

- O contrato completo de atributos (`[Narrated]`, `[OnError]`,
  `[NotTraced]`, `[NarrativeSummary]`, e as expectativas de pureza sobre
  o que eles invocam) — veja o
  [Guia de atributos](../guides/pt-BR/guia-de-atributos.md).
- Configuração programática de níveis de tracing e saída — veja o
  [Guia de configuração](../guides/pt-BR/guia-de-configuracao.md).
- O que acontece quando o próprio proxy do NarrativeTrace se empilha com
  o proxy ou interceptador de outra biblioteca — veja o
  [FAQ](../../README.md#faq) do README raiz (em inglês).
