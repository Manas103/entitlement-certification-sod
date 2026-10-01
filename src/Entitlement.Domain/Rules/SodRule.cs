using Entitlement.Domain.Models;

namespace Entitlement.Domain.Rules;

/// <summary>
/// One named segregation-of-duties rule: a pair of permissions that must never
/// both be held, within the same business unit and instrument class scope, by a
/// single user. Scoping the conflict to (BusinessUnit, InstrumentClass) rather
/// than raw permission possession is the ABAC half of the hybrid model: a user
/// who can create equity trade instructions for the Equities desk and separately
/// approve bond trade instructions for Fixed Income is not a conflict, because no
/// single scope grants them both sides of one maker-checker control.
/// </summary>
public sealed record SodRule(string RuleId, string Name, Permission Maker, Permission Checker);

/// <summary>
/// The fixed, ordered catalog of segregation-of-duties rules, modeled on the
/// maker-checker controls a real investment-operations application enforces.
/// SOD-01 is the rule named explicitly in this project's claims: a user able to
/// both create and approve a trade instruction.
/// </summary>
public static class SodRuleCatalog
{
    public static readonly SodRule TradeInstructionMakerChecker = new(
        "SOD-01", "Trade instruction maker-checker",
        Permission.CreateTradeInstruction, Permission.ApproveTradeInstruction);

    public static readonly SodRule WireTransferMakerChecker = new(
        "SOD-02", "Wire transfer maker-checker",
        Permission.CreateWireTransfer, Permission.ApproveWireTransfer);

    public static readonly SodRule SecurityMasterMakerChecker = new(
        "SOD-03", "Security master change maker-checker",
        Permission.ModifySecurityMaster, Permission.ApproveSecurityMasterChange);

    public static readonly IReadOnlyList<SodRule> AllRules = new[]
    {
        TradeInstructionMakerChecker,
        WireTransferMakerChecker,
        SecurityMasterMakerChecker
    };
}
