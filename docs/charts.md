# Charts

The charts are Astro components for the blog post, drawn as inline SVG with no chart library. They live in
`charts/components/`, and the data they import is in `charts/components/data/`.

| Component                | Data                      | Runs                                                                                                      |
|--------------------------|---------------------------|-----------------------------------------------------------------------------------------------------------|
| `PhaseBreakdown.astro`   | `phase-breakdown.json`    | `baseline-cold`                                                                                           |
| `EndToEnd.astro`         | `end-to-end.json`         | `baseline-cold`                                                                                           |
| `LatencyVsPolling.astro` | `latency-vs-polling.json` | `levers-polling-5`, `levers-polling-10`, `levers-polling-30`, `levers-polling-30-random`                  |
| `LatencyVsCost.astro`    | `latency-vs-cost.json`    | `levers-cooldown-60`, `levers-polling-30-random`, `levers-polling-10`, `levers-polling-5`, `levers-min-1` |

## Regenerating the data

```bash
node charts/export-data.mjs
```

It needs Node and no packages. It reads `results/`, checks each run's scale settings and that every sample charted is
ok, and rewrites the JSON files. The same results give the same files byte for byte.

The cost figures use the rates and the 1,000 wake-ups a month from `docs/costs.md`.

## Using them

Copy `charts/components/` with its `data/` folder into the site and import a component in a post. They use the site's
`--bg`, `--fg`, `--muted`, `--accent`, `--line` and `--font-display` CSS variables.
