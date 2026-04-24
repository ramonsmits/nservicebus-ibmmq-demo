# IBM MQ Announcement Video — Production Artifacts

Supporting material for the ~6-minute announcement video described in
[`../superpowers/specs/2026-04-24-ibmmq-video-design.md`](../superpowers/specs/2026-04-24-ibmmq-video-design.md).

## What's in this folder

| File | Purpose | Audience |
|------|---------|----------|
| [`presenter-script.md`](presenter-script.md) | Scene-by-scene voiceover, beats, and on-screen actions. Read top to bottom during recording. | Presenter |
| [`slides.md`](slides.md) | Slide copy for scenes 1 (title), 2 (agenda), 7 (outro). Hand off to whoever renders the deck. | Editor / slide designer |
| [`demo-setup-and-reset.md`](demo-setup-and-reset.md) | Commands and checklists to bring the demo stack into a clean recording state and reset it between takes. | Demo operator (usually also the presenter) |
| [`reference/Program.cs`](reference/Program.cs) | Final-state `Acme.Billing/Program.cs` — what the presenter's typing should converge to. | Presenter |
| [`reference/OrderPlacedHandler.cs`](reference/OrderPlacedHandler.cs) | Final-state handler file. | Presenter |

## Workflow

1. Read the design spec (`../superpowers/specs/2026-04-24-ibmmq-video-design.md`) — understand the why.
2. Run through `demo-setup-and-reset.md` to bring the stack up clean.
3. Strip `src/Acme.Billing/` to scene 3's starting state per `demo-setup-and-reset.md`.
4. Open `reference/Program.cs` and `reference/OrderPlacedHandler.cs` on a second monitor.
5. Record scenes 3–6 following `presenter-script.md`. Retry takes freely — the video is edited.
6. Send recordings + `slides.md` + `presenter-script.md` (for VO reference) to the editor.
