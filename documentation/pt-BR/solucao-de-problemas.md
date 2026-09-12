<!-- source: documentation/troubleshooting.md blob 599e20b64830 | translated: 2026-09-11 | reviewed: - -->
# Solução de problemas

[English](../troubleshooting.md) | [Español](../es/solucion-de-problemas.md) | **Português** | [简体中文](../zh-CN/故障排查.md)

Sintoma → causa → correção, colhidos do próprio código e dos próprios
testes desta implementação. Onde algo é uma aspereza conhecida e não coberta em
vez de uma garantia demonstrada, isso é indicado como tal — esta página
diz o que é realmente verdade hoje, não o que seria bom prometer.

## Um parâmetro é renderizado como `arg0`, `arg1` em vez de seu nome real

**Causa:** o código de captura lê `ParameterInfo.Name`, que é `null`
apenas quando os metadados compilados nunca carregaram nomes de
parâmetros — delegates construídos dinamicamente
(`System.Reflection.Emit`) ou algumas interfaces geradas por interop.
**Código C#/F# comum compilado pelo `dotnet build` sempre mantém os nomes
dos parâmetros**, inclusive em builds Release e sob trimming, então isso
não deveria acontecer em uso normal.

**Correção:** se isso acontecer, verifique se a interface veio de um
gerador de código ou de um caminho de assembly dinâmico que descartou os
metadados de parâmetros. Esta é a única diferença em relação à JVM: o
.NET não precisa de nenhuma flag `-parameters` do compilador para código
comum.

## `NarrativeTraceProxy.Create<T>` lança uma exceção ao iniciar

**Causa:** `T` não é uma interface, ou o destino não a implementa. Esta
implementação não adiciona nenhuma cláusula de guarda própria aqui — a exceção que
você vê é a exceção de reflexão subjacente do .NET
(`DispatchProxy.Create`), não uma específica do NarrativeTrace.

**Correção:** confirme que `T` é um tipo interface e que a instância que
você está encapsulando realmente a implementa.

## Um serviço registrado em DI não está sendo rastreado

**Causa**, qualquer uma destas:

- Não está registrado por **interface** — o `AddNarrativeTracing` só
  encapsula registros tipados por interface.
- O namespace de sua implementação não corresponde a um prefixo
  configurado. A correspondência é por limite de ponto: `MyApp.Services`
  nunca corresponde a `MyApp.ServicesExtra`.
- É um **serviço com chave** ou um **genérico aberto** — ambos são
  deixados sem encapsulamento silenciosamente.
- O `AddNarrativeTracing` foi chamado **antes** de o serviço ser
  registrado — o encapsulamento só toca os descritores presentes no
  momento da chamada.
- Está registrado por fábrica, e a correspondência recorreu ao namespace
  da **interface** (o tipo de implementação é opaco no momento do
  registro) — que pode diferir do namespace da implementação real.

**Correção:** chame `AddNarrativeTracing` por último, verifique
novamente o prefixo de namespace, e evite registros com chave/genéricos
abertos para serviços que você precisa rastrear.

## Requisições do ASP.NET Core não mostram trace, e nenhum erro

**Causa:** `HttpContext.GetNarrativeContext()` retorna um contexto
silencioso sem operação sempre que o middleware não guardou nada — uma
rota excluída, ou código que roda antes de
`UseMiddleware<NarrativeTraceMiddleware>()` no pipeline. A requisição se
completa normalmente; simplesmente não fica rastreada.

**Correção:** coloque o middleware perto da borda externa do pipeline, e
verifique `ExcludedPaths` se uma rota específica for a que está ficando
silenciosa.

## O trabalho de um `Task.Run` em segundo plano está faltando, ou aparece como uma raiz separada

**Causa:** o contexto `AsyncLocal` flui corretamente para o trabalho
iniciado *e* observado dentro de um escopo `RunAsync`. Um `Task.Run` puro
que ainda está em execução quando seu escopo fecha se conecta no
**momento da execução, não no do envio** — uma divergência documentada,
não um bug — então pode aparecer como uma segunda raiz em vez de se
aninhar sob a chamada que o lançou.

**Correção:** use `AsyncNarrativeContext.RunAsync`, `ForkJoinGroup` ou
`FireAndForgetGroup` para a garantia de linhagem que você realmente
precisa, em vez de um `Task.Run` puro.

## `CaptureTrace()` retorna uma trace vazia quando você esperava conteúdo

**Causa:** ler a captura de fora de qualquer escopo de
`AsyncNarrativeContext` — ou depois de dois escopos rodarem
concorrentemente — retorna deliberadamente uma árvore **vazia** em vez
de uma alheia. Uma narrativa ausente é honesta; a narrativa da requisição
de outra pessoa não seria.

**Correção:** capture de dentro do escopo que produziu o trabalho, ou
garanta que você aguardou (`await`) o trabalho antes de capturar.

## Uma falha de fire-and-forget nunca aparece em lugar nenhum

**Causa:** o `FireAndForgetGroup` engole uma exceção no trabalho lançado
por design — é isso que torna seguro disparar e esquecer — e exclui o
ramo com falha de `ChildRoots`. Este é um isolamento intencional, não uma
funcionalidade que falta.

**Correção:** se você precisa observar a falha, adicione seu próprio
tratamento dentro do trabalho lançado; não dependa da trace para
revelá-la.

## Os ramos de um `ForkJoinGroup` rodaram mas seus spans estão faltando

**Causa:** pular `await fork.JoinAsync()` significa que os spans dos
ramos nunca chegam à trace pai — o trabalho continua rodando, mas a
narrativa o perde.

**Correção:** sempre dê `await` no join.

## As traces de dois testes xUnit aparecem misturadas

**Causa:** um `NarrativeFixture` guarda um único contexto, então atende a
um teste por vez. `IClassFixture<NarrativeFixture>` é seguro porque o
xUnit nunca paraleliza dentro de uma classe — mas compartilhar uma
instância de fixture entre classes (ou via uma coleção) que *rodam* em
paralelo mescla seus spans em uma única trace.

**Correção:** deixe cada classe de teste ter sua própria instância de
fixture; não compartilhe uma entre coleções de testes paralelas.

## `clarity-scan --assembly` falha em vez de imprimir um erro limpo

**Causa:** um arquivo ausente é tratado de forma limpa (`error: assembly
not found`, código de saída `1`). Um arquivo **existente mas inválido** —
não um assembly .NET real — não é: ele é carregado via
`MetadataLoadContext` sem nenhuma guarda, então um arquivo malformado
aparece como uma exceção .NET não tratada, crua, em vez de um dos
códigos de saída documentados. Esta é uma aspereza conhecida e não
coberta, não um comportamento documentado.

**Correção:** verifique novamente se o caminho realmente aponta para um
assembly .NET compilado. Se você encontrar a falha, vale a pena reportar
em vez de contornar.

## Um erro de digitação em `NARRATIVETRACE_*` não faz nada, silenciosamente

**Causa:** por design, toda variável de ambiente `NARRATIVETRACE_*`
degrada para seu valor padrão em vez de lançar uma exceção diante de um
valor não reconhecido — "configuração incorreta nunca quebra a captura".
`NARRATIVETRACE_LEVEL=Detial` (erro de digitação) resolve tranquilamente
para `Detail`, não um erro.

**Correção:** não confie que um erro de digitação será detectado —
verifique a ortografia novamente, ou registre a configuração resolvida
ao iniciar se precisar ter certeza.

## Nenhum arquivo de trace aparece, mesmo com a saída ativada por padrão

**Causa**, uma destas:

- `NARRATIVETRACE_OUTPUT=false` está definida em algum lugar anterior (uma
  variável de CI, uma sobrescrita de ambiente em `.runsettings`, um shell
  pai) — o único interruptor que desativa o escritor, que por padrão está
  ativado.
- `NARRATIVETRACE_LEVEL` é `Off` — nada foi capturado.
- A trace realmente está vazia. **Uma trace vazia não escreve nada, por
  design** — um artefato ausente significa "nada foi capturado", não "a
  escrita falhou". Isso geralmente significa que o teste chamou o
  serviço cru, sem encapsulamento, em vez do encapsulado pelo proxy.
- Você está olhando no lugar errado: sem uma sobrescrita de
  `NARRATIVETRACE_OUTPUT_DIR`, os arquivos caem em
  `TestResults/narrativetrace/` relativo ao diretório de trabalho da
  execução de testes, não à raiz do repositório.

**Correção:** confirme que `NARRATIVETRACE_OUTPUT` não está definida como
`false`, confirme que o nível não é `Off`, confirme que você está chamando
através de `NarrativeTraceProxy.Create<T>` (ou um serviço de DI com
encapsulamento automático) em vez da implementação nua, e verifique
`TestResults/narrativetrace/` dentro do projeto de teste.

## `clarity-report.md` não corresponde ao que eu espero de `NARRATIVETRACE_OUTPUT`

**Causa:** o `clarity-results.json`/`clarity-report.md` no nível da suíte
são escritos sempre que o fixture da suíte roda e acumula pelo menos uma
entrada — **independentemente de `NARRATIVETRACE_OUTPUT`**. Apenas os
arquivos `.md`/`.json`/`.mmd`/`.nt` por teste estão sujeitos a essa flag
(ativada por padrão; `false` a desativa).

**Correção:** não trate "sem arquivos de trace por teste" como "sem
relatório de clareza" — eles são condicionados por duas condições
diferentes.

## A coleta do glossário nunca escreve nada

**Causa:** a coleta é opcional pela **presença de arquivo** — o
`GlossarySuiteReporter` não faz nada silenciosamente a menos que
`glossary.json` já exista no local resolvido (uma busca ascendente nos
diretórios, `NARRATIVETRACE_GLOSSARY`, ou o valor literal `off`).

**Correção:** faça commit de um `glossary.json` inicial se você quer que
a coleta rode — veja
[O que incluir no commit](o-que-incluir-no-commit.md#a-coleta-do-glossario-e-opcional-pela-presenca-de-arquivo).

## Referenciar `NarrativeTrace.MSBuild` o coloca nas dependências do meu pacote

**Causa:** ele é pensado para ser uma dependência apenas em tempo de
build.

**Correção:** referencie-o com `PrivateAssets="all"` para que ele não
flua de forma transitiva para os consumidores do seu próprio pacote
NuGet.
