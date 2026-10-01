using Entitlement.Domain.Rules;

namespace Entitlement.Tests.Rules;

public class SodRuleCatalogTests
{
    [Fact]
    public void AllRules_ContainsExactlyThreeNamedRules()
    {
        Assert.Equal(3, SodRuleCatalog.AllRules.Count);
        Assert.Contains(SodRuleCatalog.AllRules, r => r.RuleId == "SOD-01");
        Assert.Contains(SodRuleCatalog.AllRules, r => r.RuleId == "SOD-02");
        Assert.Contains(SodRuleCatalog.AllRules, r => r.RuleId == "SOD-03");
    }

    [Fact]
    public void TradeInstructionRule_IsTheNamedCreateAndApproveScenario()
    {
        var rule = SodRuleCatalog.TradeInstructionMakerChecker;
        Assert.Equal(Entitlement.Domain.Models.Permission.CreateTradeInstruction, rule.Maker);
        Assert.Equal(Entitlement.Domain.Models.Permission.ApproveTradeInstruction, rule.Checker);
    }

    [Fact]
    public void NoBusinessRole_IndividuallyGrantsBothSidesOfAnySodRule()
    {
        foreach (var role in RoleCatalogDefinitions.BusinessRoles)
        {
            foreach (var rule in SodRuleCatalog.AllRules)
            {
                var hasMaker = role.Permissions.Contains(rule.Maker);
                var hasChecker = role.Permissions.Contains(rule.Checker);
                Assert.False(hasMaker && hasChecker,
                    $"Role {role.RoleId} alone grants both sides of {rule.RuleId}, which would make " +
                    "the synthetic population's noise-safety check meaningless.");
            }
        }
    }
}
