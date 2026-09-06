<!-- source: documentation/what-to-commit.md blob f0993e56aa0c | translated: 2026-09-03 | reviewed: 2026-09-03 -->
# O que incluir no commit

[English](../what-to-commit.md) | [Español](../es/que-incluir-en-el-commit.md) | **Português** | [简体中文](../zh-CN/应提交的内容.md)

Depois que o tracing estiver rodando, você terá arquivos gerados em
disco. Esta página diz quais são saída descartável e quais são feitos
para serem revisados e commitados — verificado contra o que os writers
deste port realmente produzem, não presumido.

## Os artefatos, um por um

| Artefato | Commit? | Por quê |
|---|---|---|
| `<output-dir>/traces/<Class>/<slug>.md` | Não | Regenerado a cada execução; a trace legível por humanos de um teste. |
| `<output-dir>/traces/<Class>/<slug>.json` | Não | A mesma trace como documento de capítulo JSON — regenerado a cada execução. |
| `<output-dir>/diagrams/<Class>/<slug>.mmd` | Não | Diagrama de sequência Mermaid que a acompanha — regenerado a cada execução. |
| `<output-dir>/structural/<Class>/<slug>.nt` | Não, por enquanto | Trace estrutural sem valores (apenas nomes, hierarquia e tipo de resultado). Determinística e diffável por construção, mas **nada neste port a lê de volta ainda** — não há modo de aprovação nem loop de comparação por delta contra uma execução anterior, então não há linha de base commitada para compará-la. Regenerado a cada execução como o resto. |
| `<output-dir>/traces/<Class>/<slug>.canonical.json` | Não | Fixture de conformidade opcional (`NARRATIVETRACE_CANONICAL_JSON=true`), pensado para testar o próprio NarrativeTrace contra o esquema canônico — não algo que um projeto de aplicação precise manter. |
| `<output-dir>/traces/<Class>/<slug>.structural.json` | Não | Array de entradas sem valores, opcional (`NARRATIVETRACE_STRUCTURAL_JSON=true`) — mesmo raciocínio do `.canonical.json`. |
| `<output-dir>/clarity-results.json` | Não | Relatório de clareza no nível da suíte, gerado (legível por máquina). Aparece sempre que o fixture da suíte rodou e acumulou pelo menos uma entrada, independentemente de `NARRATIVETRACE_OUTPUT` — regenere, não commite. |
| `<output-dir>/clarity-report.md` | Não | O mesmo relatório, legível por humanos. |
| `clarity/clarity-scan-results.json` / `clarity-scan-report.md` (do `dotnet-narrativetrace clarity-scan`) | Não | Uma varredura estática, apenas por reflexão, de um assembly compilado — regenere em CI, não commite. |
| `glossary.json` | **Sim**, se você usa a coleta do glossário | Veja abaixo — este é o único artefato que este port trata como um arquivo revisado e curado à mão. |
| `glossary.md` | **Sim**, junto com `glossary.json` | Renderização legível por humanos do mesmo arquivo, reescrita apenas quando os bytes do JSON mudam (anti-churn). |
| `<output-dir>/glossary-usage.json` | Não | Estatísticas de uso voláteis por execução — regeneradas, não curadas. |

`<output-dir>` tem como padrão `./narrativetrace-output` quando
`NARRATIVETRACE_OUTPUT_DIR` não está definido. Adicione-o ao `.gitignore`
a menos que você tenha um motivo específico de CI para arquivá-lo como
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

## O que este port ainda não tem

Se você conhece a implementação de referência em Java, não presuma que
seu fluxo de trabalho de modo de aprovação se aplica aqui: este port não
tem arquivos `.approved.nt` / `.received.nt`, nem verbo `approve`, nem
nada que compare a trace estrutural de uma execução com uma anterior. O
artefato estrutural `.nt` existe e é determinístico, mas todo artefato
nesta página é apenas saída de regeneração hoje — ainda não há um fluxo
de trabalho de "linha de base commitada que falha um build diante de uma
mudança de comportamento não revisada" para aderir.

## Veja também

- [Privacidade e ocultação](privacidade-e-ocultacao.md) — o que há dentro
  desses arquivos antes de decidir se vale a pena arquivá-los em algum
  lugar.
- [Guia de configuração](../guides/pt-BR/guia-de-configuracao.md) — as
  variáveis `NARRATIVETRACE_*` que controlam onde e se esses arquivos são
  escritos.
