// Exports the data behind the post's charts from results/ to charts/components/data/, so the folder copies across as one.
// Run from anywhere with: node charts/export-data.mjs
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const chartsDir = dirname(fileURLToPath(import.meta.url));
const resultsDir = join(chartsDir, "..", "results");
const dataDir = join(chartsDir, "components", "data");

const baselineRun = "baseline-cold";

const variants = [
    { variant: "jit", label: "JIT" },
    { variant: "jit-chiseled", label: "JIT, chiseled" },
    { variant: "r2r", label: "ReadyToRun" },
    { variant: "aot", label: "Native AOT" },
];

const phases = [
    { name: "polling", label: "KEDA polling" },
    { name: "scheduling", label: "Scheduling" },
    { name: "imagePull", label: "Image pull" },
    { name: "containerStart", label: "Container start" },
    { name: "dotnetStartup", label: ".NET startup" },
    { name: "serviceBusConnection", label: "Service Bus connection" },
    { name: "handling", label: "Handling" },
];

// The jit runs at each polling interval, with the trigger sent as soon as zero replicas is seen.
const pollingRuns = [
    { run: "levers-polling-5", scaleConfig: "jit-polling-5", pollingInterval: 5 },
    { run: "levers-polling-10", scaleConfig: "jit-polling-10", pollingInterval: 10 },
    { run: "levers-polling-30", scaleConfig: "jit-polling-30", pollingInterval: 30 },
];

const randomRun = { run: "levers-polling-30-random", scaleConfig: "jit-polling-30", pollingInterval: 30 };

const costConfigs = [
    { key: "cooldown60", label: "Cooldown 60 s", run: "levers-cooldown-60", scaleConfig: "jit-cooldown-60" },
    { key: "defaults", label: "Defaults", run: randomRun.run, scaleConfig: randomRun.scaleConfig },
    { key: "polling10", label: "Polling 10 s", run: "levers-polling-10", scaleConfig: "jit-polling-10" },
    { key: "polling5", label: "Polling 5 s", run: "levers-polling-5", scaleConfig: "jit-polling-5" },
];

const alwaysOnRun = { run: "levers-min-1", scaleConfig: "jit-min-1" };

// UK South list prices in USD, Consumption plan, from the Retail Prices API; docs/costs.md has the sources.
const rates = {
    vcpuActivePerSecond: 0.000034,
    vcpuIdlePerSecond: 0.000004,
    memoryActivePerGibSecond: 0.000004,
    memoryIdlePerGibSecond: 0.000004,
};
const secondsPerMonth = 730 * 3600;
const wakeUpsPerMonth = 1000;

function readJson(run, file) {
    return JSON.parse(readFileSync(join(resultsDir, run, file), "utf8"));
}

function readSamples(run) {
    const [header, ...lines] = readFileSync(join(resultsDir, run, "samples.csv"), "utf8").trim().split(/\r?\n/);
    const columns = header.split(",");
    return lines.map((line) => {
        const values = line.split(",");
        return Object.fromEntries(columns.map((column, index) => [column, values[index]]));
    });
}

function median(values) {
    const sorted = [...values].sort((a, b) => a - b);
    const middle = (sorted.length - 1) / 2;
    return (sorted[Math.floor(middle)] + sorted[Math.ceil(middle)]) / 2;
}

const round = (value, digits) => Number(value.toFixed(digits));

function appOf(config, variant) {
    const app = config.apps.find((candidate) => candidate.variant === variant);
    if (!app) {
        throw new Error(`${config.run} has no ${variant} app`);
    }
    return app;
}

// Checks the run against the scale settings it's charted as, and that every sample is ok.
function checkRun(run, { kind, scaleConfig, pollingInterval, cooldownPeriod, minReplicas, randomDelay = false }) {
    const config = readJson(run, "config.json");
    if (config.kind !== kind || Boolean(config.randomDelay) !== randomDelay) {
        throw new Error(`${run} isn't a ${kind} run with randomDelay ${randomDelay}`);
    }
    const app = appOf(config, "jit");
    const { scale } = app.template;
    if (
        (scaleConfig !== undefined && app.scaleConfig !== scaleConfig) ||
        (pollingInterval !== undefined && scale.pollingInterval !== pollingInterval) ||
        (cooldownPeriod !== undefined && scale.cooldownPeriod !== cooldownPeriod) ||
        (minReplicas !== undefined && scale.minReplicas !== minReplicas)
    ) {
        throw new Error(`${run} has the wrong scale settings`);
    }
    const samples = readSamples(run).filter((sample) => sample.variant === "jit");
    if (samples.some((sample) => sample.status !== "ok")) {
        throw new Error(`${run} has samples that aren't ok`);
    }
    return { config, app, samples };
}

function summaryGroup(run, variant) {
    const group = readJson(run, "summary.json").groups.find((candidate) => candidate.variant === variant);
    if (!group || group.statuses.ok !== group.samples) {
        throw new Error(`${run} has no complete ${variant} group`);
    }
    return group;
}

const phaseMedian = (group, name) => group.phases.find((phase) => phase.name === name).duration.medianMs;

function exportPhaseBreakdown() {
    const config = readJson(baselineRun, "config.json");
    return {
        run: baselineRun,
        phases,
        variants: variants.map(({ variant, label }) => {
            const { scale } = appOf(config, variant).template;
            if (scale.pollingInterval !== 30 || scale.cooldownPeriod !== 300 || scale.minReplicas !== 0) {
                throw new Error(`${baselineRun} ${variant} isn't on the default scale settings`);
            }
            const group = summaryGroup(baselineRun, variant);
            return {
                variant,
                label,
                endToEndMedianMs: group.endToEnd.medianMs,
                phaseMediansMs: Object.fromEntries(phases.map(({ name }) => [name, phaseMedian(group, name)])),
            };
        }),
    };
}

function exportEndToEnd() {
    const samples = readSamples(baselineRun);
    return {
        run: baselineRun,
        variants: variants.map(({ variant, label }) => {
            const group = summaryGroup(baselineRun, variant);
            const rows = samples.filter((sample) => sample.variant === variant && sample.status === "ok");
            return {
                variant,
                label,
                medianMs: group.endToEnd.medianMs,
                p90Ms: group.endToEnd.p90Ms,
                samplesMs: rows
                    .sort((a, b) => Number(a.sample) - Number(b.sample))
                    .map((sample) => Number(sample.endToEndMs)),
            };
        }),
    };
}

function latencyPoint(run, settings) {
    checkRun(run, { kind: "cold", cooldownPeriod: 300, minReplicas: 0, ...settings });
    const group = summaryGroup(run, "jit");
    return {
        run,
        pollingIntervalSeconds: settings.pollingInterval,
        endToEndMedianMs: group.endToEnd.medianMs,
        endToEndP90Ms: group.endToEnd.p90Ms,
        pollingMedianMs: phaseMedian(group, "polling"),
    };
}

function exportLatencyVsPolling() {
    return {
        variant: "jit",
        afterScaleIn: pollingRuns.map(({ run, scaleConfig, pollingInterval }) =>
            latencyPoint(run, { scaleConfig, pollingInterval }),
        ),
        randomArrival: latencyPoint(randomRun.run, {
            scaleConfig: randomRun.scaleConfig,
            pollingInterval: randomRun.pollingInterval,
            randomDelay: true,
        }),
    };
}

// Replica time per wake-up: from a sample's scale-out to the zero replicas the next sample waited for.
function secondsPerWakeUp(samples) {
    const ordered = [...samples].sort((a, b) => Number(a.sample) - Number(b.sample));
    const gaps = ordered
        .slice(0, -1)
        .map((sample, index) => (Date.parse(ordered[index + 1].zeroObserved) - Date.parse(sample.scaledOut)) / 1000);
    if (gaps.some((gap) => !Number.isFinite(gap))) {
        throw new Error("a sample is missing scaledOut or zeroObserved");
    }
    return median(gaps);
}

function perSecond(app, state) {
    const { cpu, memory } = app.template.containers[0].resources;
    const gib = Number(memory.replace(/Gi$/, ""));
    return state === "active"
        ? cpu * rates.vcpuActivePerSecond + gib * rates.memoryActivePerGibSecond
        : cpu * rates.vcpuIdlePerSecond + gib * rates.memoryIdlePerGibSecond;
}

function exportLatencyVsCost() {
    const scaleToZero = costConfigs.map(({ key, label, run, scaleConfig }) => {
        const randomDelay = run === randomRun.run;
        const { app, samples } = checkRun(run, { kind: "cold", scaleConfig, minReplicas: 0, randomDelay });
        const group = summaryGroup(run, "jit");
        const lifetime = secondsPerWakeUp(samples);
        const perWakeUp = lifetime * perSecond(app, "active");
        return {
            key,
            label,
            run,
            arrival: randomDelay ? "random" : "afterScaleIn",
            medianMs: group.endToEnd.medianMs,
            replicaSecondsPerWakeUp: round(lifetime, 1),
            costPerWakeUp: round(perWakeUp, 6),
            monthlyCost: round(perWakeUp * wakeUpsPerMonth, 2),
        };
    });
    const { app, samples } = checkRun(alwaysOnRun.run, {
        kind: "warm",
        scaleConfig: alwaysOnRun.scaleConfig,
        minReplicas: 1,
    });
    return {
        variant: "jit",
        wakeUpsPerMonth,
        hoursPerMonth: secondsPerMonth / 3600,
        scaleToZero,
        alwaysOn: {
            label: "Always on",
            run: alwaysOnRun.run,
            medianMs: median(samples.map((sample) => Number(sample.endToEndMs))),
            monthlyCostIdle: round(secondsPerMonth * perSecond(app, "idle"), 2),
            monthlyCostActive: round(secondsPerMonth * perSecond(app, "active"), 2),
        },
    };
}

const write = (file, data) => writeFileSync(join(dataDir, file), JSON.stringify(data, null, 4) + "\n");

mkdirSync(dataDir, { recursive: true });
write("phase-breakdown.json", exportPhaseBreakdown());
write("end-to-end.json", exportEndToEnd());
write("latency-vs-polling.json", exportLatencyVsPolling());
write("latency-vs-cost.json", exportLatencyVsCost());
