<!-- source: documentation/what-to-commit.md blob a44173433831 | translated: 2026-09-13 | reviewed: - -->
# O que incluir no commit

[English](../what-to-commit.md) | [Español](../es/que-incluir-en-el-commit.md) | **Português** | [简体中文](../zh-CN/应提交的内容.md)

Depois que o tracing estiver rodando, você terá arquivos gerados em
disco. Esta página diz quais são saída descartável e quais são feitos
para serem revisados e commitados — verificado contra o que os writers
desta implementação realmente produzem, não presumido.

## Os artefatos, um por um

| Artefato | Commit? | Por quê |
|---|---|---|
| `<output-dir>/traces/<Class>/<slug>.md` | Não | Regenerado a cada execução; a trace legível por humanos de um teste. |
| `<output-dir>/traces/<Class>/<slug>.json` | Não | A mesma trace como documento de capítulo JSON — regenerado a cada execução. |
| `<output-dir>/diagrams/<Class>/<slug>.mmd` | Não | Diagrama de sequência Mermaid que a acompanha — regenerado a cada execução. |
| `<output-dir>/manifest.json` | Não | Regenerado a cada execução; seu objeto `run` de nível superior (`id`, `name` — a frase de três palavras própria da execução) nomeia *esta execução*, não um cenário, então muda a cada execução mesmo quando mais nada muda *(since 0.1.4)*. |
| `<output-dir>/structural/<Class>/<slug>.nt` | Não | Trace estrutural sem valores (apenas nomes, hierarquia e tipo de resultado). O arquivo em disco é a **última linha de base correta (last green)** *(since 0.1.4)*: uma execução verde a avança, uma execução não verde é comparada com ela (a linha "Since last green" do resumo da suíte, o delta do relatório de falha) mas nunca a sobrescreve. Ainda não é algo para commitar — veja [Formato de trace estrutural](../structural-trace-format.md) para a contraparte commitada. |
| `<approved-dir>/<Class>/<slug>.approved.nt` | **Sim**, se o [modo de aprovação](../structural-trace-format.md) estiver ativado | *(since 0.1.4)* A trace de aprovação revisada — `NARRATIVETRACE_APPROVED_DIR` (padrão `narratives`), ative com `NARRATIVETRACE_APPROVAL=true`. Este é o único arquivo desta tabela que é uma decisão deliberada, não uma saída. |
| `<approved-dir>/<Class>/<slug>.received.nt` | Não | Escrito quando a aprovação não bate, ou quando ainda não existe uma trace aprovada. Revise-o, rode `./build.sh Approve` para promovê-lo (ou renomeie-o manualmente), e deixe a promoção removê-lo — nunca commite a trace recebida em si. |
| `<output-dir>/traces/<Class>/<slug>.canonical.json` | Não | Fixture de conformidade opcional (`NARRATIVETRACE_CANONICAL_JSON=true`), pensado para testar o próprio NarrativeTrace contra o esquema canônico — não algo que um projeto de aplicação precise manter. |
| `<output-dir>/traces/<Class>/<slug>.structural.json` | Não | Array de entradas sem valores, opcional (`NARRATIVETRACE_STRUCTURAL_JSON=true`) — mesmo raciocínio do `.canonical.json`. |
| `<output-dir>/clarity-results.json` | Não | Relatório de clareza no nível da suíte, gerado (legível por máquina). Aparece sempre que o fixture da suíte rodou e acumulou pelo menos uma entrada, independentemente de `NARRATIVETRACE_OUTPUT` — regenere, não commite. |
| `<output-dir>/clarity-report.md` | Não | O mesmo relatório, legível por humanos. |
| `clarity/clarity-scan-results.json` / `clarity-scan-report.md` (do `dotnet-narrativetrace clarity-scan`) | Não | Uma varredura estática, apenas por reflexão, de um assembly compilado — regenere em CI, não commite. |
| `glossary.json` | **Sim**, se você usa a coleta do glossário | Veja abaixo — este é o único artefato que esta implementação trata como um arquivo revisado e curado à mão. |
| `glossary.md` | **Sim**, junto com `glossary.json` | Renderização legível por humanos do mesmo arquivo, reescrita apenas quando os bytes do JSON mudam (anti-churn). |
| `<output-dir>/glossary-usage.json` | Não | Estatísticas de uso voláteis por execução — regeneradas, não curadas. |
| `.claude/skills/<segmento>/SKILL.md` | **Sim** | Regenerado por `dotnet run --project src/NarrativeTrace.Cli -- skills render` a partir do catálogo tipado de skills, mas commitado mesmo assim: precisa ser publicado exatamente no caminho onde o Claude Code o descobre. `skills lint` falha o build se ele divergir de uma renderização recente. |
| A seção `<!-- narrativetrace:skills:start -->` … `<!-- narrativetrace:skills:end -->` do `AGENTS.md` | **Sim** | Mesmo renderizador, inserido no arquivo no mesmo lugar — faça commit do arquivo inteiro, não apenas da seção. |

A escrita de trace está **ativada por padrão** *(since 0.1.4)* (defina
`NARRATIVETRACE_OUTPUT=false` para desativar); `<output-dir>` tem como
padrão `./TestResults/narrativetrace` quando `NARRATIVETRACE_OUTPUT_DIR`
não está definido — a convenção do `.NET` que `dotnet test
--results-directory` e o Visual Studio/Rider já tratam como saída
descartável, e que o próprio `.gitignore` deste repositório já exclui.
Mantenha-o fora do controle de versão também nos seus próprios projetos, a
menos que você tenha um motivo específico de CI para arquivá-lo como
artefato de build (o que é uma decisão de retenção de CI, não uma de
"fazer commit no controle de versão").

## A coleta do glossário é opcional pela presença de arquivo

Diferente de tudo o mais nesta página, a coleta do glossário não cria
nada espontaneamente: o `GlossarySuiteReporter` não faz nada
silenciosamente a menos que `glossary.json` **já exista** no local que
ele resolve (uma busca ascendente nos diretórios a partir da execução de
testes, a variável de ambiente `NARRATIVETRACE_GLOSSARY`, ou o valor
literal `off` para desativar a funcionalidade por completo). Se você
quer coleta, faça commit de um arquivo inicial você mesmo:

```json
{
  "schemaVersion": 1,
  "contexts": {},
  "terms": []
}
```

A partir daí, cada execução da suíte coleta vocabulário novo de suas
traces, o mescla de forma aditiva em `glossary.json`, e regenera
`glossary.md` — revise o diff como qualquer outro arquivo curado à mão.
Um `glossary.json` malformado lança uma exceção em vez de ser ignorado
silenciosamente, o que é deliberado: um erro de digitação em um arquivo
revisado e commitado deveria falhar ruidosamente.

## Modo de aprovação, do início ao fim

```text
o teste passa
   |
   v
compara a estrutura atual com a trace aprovada
   |
   +-- igual      --> passa, nada é escrito
   +-- diferente  --> escreve .received.nt e falha
                     |
                     v
                uma pessoa revisa o diff
                     |
                     v
                ./build.sh Approve
                     |
                     v
              .approved.nt atualizado, commite
```

O estado de falha é deliberado: um teste que passou mas cuja *forma*
mudou — incluindo uma mudança que um agente de IA deslizou dentro de um
refactor que, fora isso, estava correto — precisa ser revisado e aprovado
explicitamente, não apenas compilar. Veja
[Formato de trace estrutural](../structural-trace-format.md) para o
comportamento completo do modo de aprovação, e o
[Guia de configuração](../guides/pt-BR/guia-de-configuracao.md) para
`NARRATIVETRACE_APPROVAL` / `NARRATIVETRACE_APPROVED_DIR`.

## Veja também

- [Formato de trace estrutural](../structural-trace-format.md) — o
  formato `.nt`, a identidade de artefato por invocação, a última linha
  de base correta e o modo de aprovação completo.
- [Privacidade e ocultação](privacidade-e-ocultacao.md) — o que há dentro
  desses arquivos antes de decidir se vale a pena arquivá-los em algum
  lugar.
- [Guia de configuração](../guides/pt-BR/guia-de-configuracao.md) — as
  variáveis `NARRATIVETRACE_*` que controlam onde e se esses arquivos são
  escritos.
