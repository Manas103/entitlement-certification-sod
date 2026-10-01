using System.Diagnostics;
using System.Runtime.InteropServices;
using Entitlement.Domain.Detection;
using Entitlement.Domain.Engine;
using Entitlement.Domain.Log;
using Entitlement.Domain.Models;
using Entitlement.Domain.Reporting;
using Entitlement.Domain.Synthetic;

var outputDir = args.Length > 0 ? args[0] : "docs";
Directory.CreateDirectory(outputDir);
var outputPath = Path.Combine(outputDir, "measurement_output.txt");

var report = new System.Text.StringBuilder();
void Line(string s = "")
{
    Console.WriteLine(s);
    report.Append(s).Append('\n');
}

Line("Entitlement Certification Service with Segregation-of-Duties Detection - measurement run");
Line($"Run started (UTC): {DateTime.UtcNow:O}");
Line($"Machine: {Environment.ProcessorCount} logical cores, OS {RuntimeInformation.OSDescription}, {RuntimeInformation.FrameworkDescription}");
Line();

// --- Generate the synthetic population ---
var sw = Stopwatch.StartNew();
var population = SyntheticPopulationGenerator.Generate(seed: 42, totalGrantTarget: 4000, noiseUserCount: 1300);
sw.Stop();
Line($"Generated synthetic population: {population.TotalGrantCount} active user-role grants, " +
     $"{population.Plan.TotalDefects} seeded defects ({population.Plan.Sod01Count} SOD-01, " +
     $"{population.Plan.Sod02Count} SOD-02, {population.Plan.Sod03Count} SOD-03, " +
     $"{population.Plan.OrphanUserCount} orphan-user, {population.Plan.OrphanRoleCount} orphan-role) " +
     $"in {sw.ElapsedMilliseconds} ms.");
Line();

// --- Replay the log and run the fast detector over the full population ---
sw.Restart();
var snapshot = EntitlementSnapshotBuilder.Build(population.Log);
var fastFindings = FastConflictDetector.Detect(snapshot);
sw.Stop();
Line("--- Quarterly access-review campaign: fast/indexed detector over full population ---");
Line($"Active user-role grants reviewed: {snapshot.ActiveGrants.Count}");
Line($"Detection wall time: {sw.ElapsedMilliseconds} ms");
Line($"Findings raised: {fastFindings.Count}");
Line();

var seededSet = new HashSet<string>(population.SeededFindingIds, StringComparer.Ordinal);
var fastFindingIds = new HashSet<string>(fastFindings.Select(f => f.FindingId), StringComparer.Ordinal);
var caught = seededSet.Count(id => fastFindingIds.Contains(id));
var falsePositives = fastFindingIds.Except(seededSet).ToList();

Line("--- Seeded defect catch rate ---");
Line($"Seeded defects: {seededSet.Count}");
Line($"Caught by fast detector: {caught} / {seededSet.Count}");
Line($"False positives (findings raised that were not seeded): {falsePositives.Count} over {snapshot.ActiveGrants.Count} user-role pairs reviewed");
if (falsePositives.Count > 0)
{
    Line("False positive finding ids: " + string.Join(", ", falsePositives.Take(20)));
}
Line();

var namedScenario = fastFindings.FirstOrDefault(f =>
    f.SodRuleId == "SOD-01" &&
    f.Type == FindingType.SegregationOfDutiesConflict);
Line("--- Named SoD scenario: a user able to both create and approve a trade instruction ---");
Line(namedScenario is not null
    ? $"Caught: finding {namedScenario.FindingId} - {namedScenario.Description}"
    : "NOT CAUGHT");
Line($"Total SOD-01 (trade instruction maker-checker) findings: {fastFindings.Count(f => f.SodRuleId == "SOD-01")}");
Line();

// --- Reference oracle diff ---
sw.Restart();
var oracleFindings = ReferenceOracleDetector.Detect(snapshot);
sw.Stop();
var oracleIds = new HashSet<string>(oracleFindings.Select(f => f.FindingId), StringComparer.Ordinal);
var onlyInFast = fastFindingIds.Except(oracleIds).ToList();
var onlyInOracle = oracleIds.Except(fastFindingIds).ToList();

Line("--- Reference oracle diff (brute-force O(users x scopes x grants), full population) ---");
Line($"Oracle wall time: {sw.ElapsedMilliseconds} ms");
Line($"Oracle findings: {oracleFindings.Count}");
Line($"Fast findings: {fastFindings.Count}");
Line($"Findings only in fast detector: {onlyInFast.Count}");
Line($"Findings only in oracle: {onlyInOracle.Count}");
Line($"Exact match: {onlyInFast.Count == 0 && onlyInOracle.Count == 0}");
Line();

// --- Evidence pack byte-identity ---
var pack1 = EvidencePackGenerator.Generate(population.Log, "2026-Q3");
var pack2 = EvidencePackGenerator.Generate(population.Log, "2026-Q3");
var hash1 = DigestUtil.Sha256Hex(pack1);
var hash2 = DigestUtil.Sha256Hex(pack2);

Line("--- Reviewer evidence pack byte-identity (two independent regenerations from the same append-only log) ---");
Line($"Pack size: {pack1.Length} chars");
Line($"Run 1 SHA-256: {hash1}");
Line($"Run 2 SHA-256: {hash2}");
Line($"Byte-identical: {hash1 == hash2 && pack1 == pack2}");
Line();

// --- Append-only invariant demonstration ---
var priorCount = population.Log.Entries.Count;
var digestBefore = population.Log.PriorEntriesDigest(priorCount);
population.Log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U-DEMO", roleId: "ClientServicingRep",
    businessUnit: BusinessUnit.ClientServicing, instrumentClass: InstrumentClass.Equity, grantId: "G-DEMO-APPEND");
var digestAfterOnPriorRange = population.Log.PriorEntriesDigest(priorCount);
Line("--- Append-only grant log invariant ---");
Line($"Entries before demo append: {priorCount}");
Line($"Entries after demo append: {population.Log.Entries.Count}");
Line($"Digest of first {priorCount} entries, before append:  {digestBefore}");
Line($"Digest of first {priorCount} entries, after append:   {digestAfterOnPriorRange}");
Line($"Prior entries unchanged by append: {digestBefore == digestAfterOnPriorRange}");
Line();

Line("=== Measured results summary ===");
Line($"Active user-role grants: {snapshot.ActiveGrants.Count} (target 4,000)");
Line($"Seeded SoD conflicts + orphaned grants caught: {caught} / {seededSet.Count} (target 28 / 28)");
Line($"Named create+approve trade instruction scenario caught: {namedScenario is not null}");
Line($"False positives over {snapshot.ActiveGrants.Count} user-role pairs: {falsePositives.Count} (target 0)");
Line($"Reference oracle exact match: {onlyInFast.Count == 0 && onlyInOracle.Count == 0}");
Line($"Evidence pack byte-identical across two regenerations: {hash1 == hash2 && pack1 == pack2}");
Line($"Append-only invariant holds: {digestBefore == digestAfterOnPriorRange}");

File.WriteAllText(outputPath, report.ToString());
Console.WriteLine();
Console.WriteLine($"Full output written to {outputPath}");
