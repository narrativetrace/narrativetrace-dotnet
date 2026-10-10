---
name: narrativetrace-feedback
description: "Reports a defect in NarrativeTrace itself — the library, the doctor, an agent skill, or the published install prompt. Use when a doctor finding is wrong or its fix does not work, when a skill step cannot be followed or its verify cannot be met, when the install prompt is wrong, or when the library misbehaves and the project is configured correctly. Drafts the report from this project (the install coordinates, the doctor's own JSON report, and at most one structural trace), refuses to write one that carries a value from your traces and names the rule that refused it, shows you the whole draft, and then asks once whether to file it publicly. Files nothing without your answer and sends nothing anywhere. Say \"report this to NarrativeTrace\", \"the doctor's fix did not work\", or \"file a bug about this skill\" to invoke it."
when_to_use: "Non-obvious triggers: a doctor fix that leaves the same finding failing; a skill step whose verify cannot be met on a correctly configured project; wording in the install prompt that led somewhere wrong."
---

# narrativetrace-feedback

## 1. Gather what the report needs

```bash
dotnet tool run dotnet-narrativetrace doctor --report
```

**verify:** the command left the doctor's JSON report under TestResults/narrativetrace, so the report can carry it; a project whose doctor cannot run is reported under the prompt or library category instead

**failure:** command not found: dotnet-narrativetrace — NarrativeTrace.Cli is not installed as a local tool in this project yet — fix: report under the prompt or library category without a doctor report, or run the add-narrative-tracing skill's install step first

## 2. Draft the report and let the gate check it

```bash
dotnet tool run dotnet-narrativetrace feedback draft --category <category> --step <where it happened> --did <what you did> --happened <what happened> --expected <what you expected> --language <language tag>
```

**verify:** the command exited 0 and wrote the draft and body files it names — a run that named a vf.* rule instead wrote neither, and the field it named is what to fix

**failure:** the command exits 2 naming a vf.* rule — a field carries a value from this project's own run — a rendered call line, an elapsed time, a credential-shaped string, an address — fix: rewrite that one field to describe what happened instead of pasting it, and draft again; never work around the rule by moving the text to another field

## 3. Show the whole draft, not a summary of it

**verify:** the whole text of the draft the command printed is in the reply, attachments included, not a summary of it and not a parenthetical standing in for an attachment

## 4. Ask once whether to file it, then stop the turn

**verify:** the reply ends with the question and nothing after it — the answer is the user's next message, never something assumed in this one. Do not print the issue URL or run gh before the user says yes — showing the URL is the filing.

## 5. Print the way to file it, and nothing else

```bash
dotnet tool run dotnet-narrativetrace feedback url --category <category> --step <where it happened> --did <what you did> --happened <what happened> --expected <what you expected> --language <language tag>
```

**verify:** the printed URL is in the reply, together with the name of the body file the command printed, to paste into the form's last box

## Always
- Show the whole draft before asking anything (filing is public and permanent, and a person can only approve what they have actually read)
- Ask in the user's own language, and write the report in it (the report may be written in any language, and a question nobody understands is not a question)
- Tell the user that filing is public, under their own account, before they answer (a public issue shows that their project uses NarrativeTrace, and that is their decision to make knowingly)

## Never
- Never attach a rendered trace, a log file or a source file (those carry the values from the user's own run; the structural trace carries the same shape of the same call without any of them, and the verb attaches it on its own)
- Never file in the turn that asked (approval is the user's next message — a yes assumed in the same turn is not one)
- Never edit the draft after showing it (what was approved has to be what is filed, so a changed report is drafted again and shown again)
- Never open the URL or run the printed command (submitting is the user's act, in their own browser or their own shell, under their own account)
- Never route a rule's refusal around the gate (a field that cannot be filed is a field to rewrite, not to move somewhere the rule does not look)

