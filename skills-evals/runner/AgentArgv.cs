// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Builds the agent CLI's argv list from a <c>{prompt}</c>-templated command and a prompt, without
/// ever routing the prompt through a shell.
///
/// <para>INTENT: a backtick or <c>$(...)</c> inside the prompt (a check id copied from a grader,
/// say) must never run as a shell command substitution before the agent sees it. Tokenizing the
/// STATIC parts of the template (never the prompt) and substituting the <c>{prompt}</c> placeholder
/// as one opaque argv element closes that: the prompt reaches the process as a single array element
/// the OS hands to the child verbatim — no parser looks inside it again.</para>
///
/// <para>The tokenizer understands only the whitespace/quoting shape of the fixed platform presets
/// (<see cref="Platforms.PresetAgentCommand"/>) or an operator-supplied override — it is not a
/// general shell grammar and must never be handed untrusted text. The prompt itself is never
/// tokenized: it is substituted after tokenizing, as one whole value.</para>
/// </summary>
public static class AgentArgv
{
    private const string PromptPlaceholder = "{prompt}";

    /// <summary>
    /// Tokenizes <paramref name="template"/> (double- or single-quoted segments respected, exactly
    /// as the presets write <c>"{prompt}"</c>) and substitutes any token equal to <c>{prompt}</c>
    /// with <paramref name="prompt"/>, verbatim, as one argv element.
    /// </summary>
    public static IReadOnlyList<string> Build(string template, string prompt)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new ArgumentException("template must not be blank", nameof(template));
        }

        ArgumentNullException.ThrowIfNull(prompt);

        return new Tokenizer(template).Tokenize()
            .Select(token => token == PromptPlaceholder ? prompt : token)
            .ToList();
    }

    /// <summary>A single-use scanner over one template string, discarded after <see cref="Tokenize"/>.</summary>
    private sealed class Tokenizer(string template)
    {
        private readonly List<string> _tokens = [];
        private readonly StringBuilder _current = new();
        private bool _inToken;
        private int _i;

        public List<string> Tokenize()
        {
            while (_i < template.Length)
            {
                Step(template[_i]);
            }

            CloseToken();
            return _tokens;
        }

        private void Step(char c)
        {
            if (char.IsWhiteSpace(c))
            {
                CloseToken();
                _i++;
            }
            else if (c is '"' or '\'')
            {
                ConsumeQuoted(c);
            }
            else
            {
                _current.Append(c);
                _inToken = true;
                _i++;
            }
        }

        private void CloseToken()
        {
            if (!_inToken)
            {
                return;
            }

            _tokens.Add(_current.ToString());
            _current.Clear();
            _inToken = false;
        }

        private void ConsumeQuoted(char quote)
        {
            _i++;
            while (_i < template.Length && template[_i] != quote)
            {
                _current.Append(template[_i]);
                _i++;
            }

            if (_i >= template.Length)
            {
                throw new ArgumentException($"unterminated {quote} in template: {template}");
            }

            _i++;
            _inToken = true;
        }
    }
}
