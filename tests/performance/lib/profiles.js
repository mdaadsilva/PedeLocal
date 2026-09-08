// Load-profile executors shared by every scenario — smoke/load/stress, selected via the
// PROFILE env var (default smoke). See docs/PERFORMANCE_TESTING.md for the rationale and how to
// tune each one. Numbers here are deliberately modest defaults, not the last word — refine
// against a real staging baseline before trusting them as pass/fail gates.
import { config } from "./config.js";

function int(name, fallback) {
  const raw = __ENV[name];
  const parsed = raw !== undefined ? parseInt(raw, 10) : NaN;
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

/** Ramping-VUs executor for read/write flows that scale with "how many users at once" (login,
 * catalog browsing, preview, order creation, status transitions). */
export function rampingExecutor(opts = {}) {
  const {
    smokeVUs = 5,
    loadVUs = 30,
    loadRampTime = "30s",
    loadHoldTime = "1m",
    stressMaxVUsDefault = 80,
    stressRampStep = "30s",
    stressHoldTime = "1m",
    stressSteps = 4,
  } = opts;

  switch (config.profile) {
    case "smoke":
      return {
        executor: "ramping-vus",
        startVUs: 0,
        stages: [
          { duration: "10s", target: smokeVUs },
          { duration: "20s", target: smokeVUs },
          { duration: "5s", target: 0 },
        ],
        gracefulRampDown: "5s",
      };
    case "load":
      return {
        executor: "ramping-vus",
        startVUs: 0,
        stages: [
          { duration: loadRampTime, target: loadVUs },
          { duration: loadHoldTime, target: loadVUs },
          { duration: "15s", target: 0 },
        ],
        gracefulRampDown: "10s",
      };
    case "stress": {
      const maxVUs = int("STRESS_MAX_VUS", stressMaxVUsDefault);
      const steps = int("STRESS_STEPS", stressSteps);
      const stages = [];
      for (let i = 1; i <= steps; i++) {
        stages.push({ duration: stressRampStep, target: Math.round((maxVUs * i) / steps) });
        stages.push({ duration: stressHoldTime, target: Math.round((maxVUs * i) / steps) });
      }
      stages.push({ duration: "20s", target: 0 });
      return { executor: "ramping-vus", startVUs: 0, stages, gracefulRampDown: "10s" };
    }
    default:
      throw new Error(`Unknown PROFILE "${config.profile}" — use smoke, load, or stress.`);
  }
}

/** shared-iterations executor for trial-based scenarios (the driver first-accept-wins race,
 * scenario 5) where what matters is "how many independent trials", not sustained VU-seconds. */
export function trialsExecutor(opts = {}) {
  const { smokeVUs = 1, smokeIterations = 3, loadVUs = 3, loadIterations = 15, stressVUsDefault = 5, stressIterationsDefault = 30 } = opts;

  switch (config.profile) {
    case "smoke":
      return { executor: "shared-iterations", vus: smokeVUs, iterations: smokeIterations, maxDuration: "2m" };
    case "load":
      return { executor: "shared-iterations", vus: loadVUs, iterations: loadIterations, maxDuration: "5m" };
    case "stress": {
      const vus = int("STRESS_RACE_VUS", stressVUsDefault);
      const iterations = int("STRESS_RACE_ITERATIONS", stressIterationsDefault);
      return { executor: "shared-iterations", vus, iterations, maxDuration: "10m" };
    }
    default:
      throw new Error(`Unknown PROFILE "${config.profile}" — use smoke, load, or stress.`);
  }
}

/** How many VUs `trialsExecutor`/`rampingExecutor` will use for the *current* profile — scenario
 * setup() functions need this to know how large a resource pool (e.g. racing drivers) to
 * pre-provision, since setup() runs before k6 assigns VUs. */
export function maxVUsFor(kind, opts = {}) {
  if (kind === "trials") {
    const executor = trialsExecutor(opts);
    return executor.vus;
  }
  const executor = rampingExecutor(opts);
  if (executor.stages) {
    return Math.max(...executor.stages.map((s) => s.target));
  }
  return executor.vus || 1;
}
