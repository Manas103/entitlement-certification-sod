using Entitlement.Domain.Models;

namespace Entitlement.Domain.Rules;

/// <summary>
/// The fixed set of business roles the synthetic population is built from.
/// Individually, no role on its own grants both sides of any
/// <see cref="SodRuleCatalog"/> pair; a conflict only exists when a single user
/// holds two separate roles that together supply both sides within the same
/// business-unit/instrument-class scope. This is what makes the hybrid model
/// meaningful: the role is the baseline unit of access, the scope on the grant
/// is the attribute that determines whether two roles actually collide.
/// </summary>
public static class RoleCatalogDefinitions
{
    public static readonly RoleDefinition TradeMaker = new()
    { RoleId = "TradeMaker", Permissions = new[] { Permission.ViewPosition, Permission.CreateTradeInstruction } };

    public static readonly RoleDefinition TradeApprover = new()
    { RoleId = "TradeApprover", Permissions = new[] { Permission.ViewPosition, Permission.ApproveTradeInstruction } };

    public static readonly RoleDefinition WireMaker = new()
    { RoleId = "WireMaker", Permissions = new[] { Permission.ViewClientData, Permission.CreateWireTransfer } };

    public static readonly RoleDefinition WireApprover = new()
    { RoleId = "WireApprover", Permissions = new[] { Permission.ViewClientData, Permission.ApproveWireTransfer } };

    public static readonly RoleDefinition SecurityMasterAnalyst = new()
    { RoleId = "SecurityMasterAnalyst", Permissions = new[] { Permission.ModifySecurityMaster } };

    public static readonly RoleDefinition SecurityMasterApprover = new()
    { RoleId = "SecurityMasterApprover", Permissions = new[] { Permission.ApproveSecurityMasterChange } };

    public static readonly RoleDefinition OperationsAnalyst = new()
    { RoleId = "OperationsAnalyst", Permissions = new[] { Permission.ViewPosition, Permission.ReconcilePosition } };

    public static readonly RoleDefinition ComplianceReviewer = new()
    { RoleId = "ComplianceReviewer", Permissions = new[] { Permission.ViewAuditLog, Permission.ViewClientData } };

    public static readonly RoleDefinition ClientServicingRep = new()
    { RoleId = "ClientServicingRep", Permissions = new[] { Permission.ViewClientData } };

    public static readonly IReadOnlyList<RoleDefinition> BusinessRoles = new[]
    {
        TradeMaker, TradeApprover, WireMaker, WireApprover,
        SecurityMasterAnalyst, SecurityMasterApprover,
        OperationsAnalyst, ComplianceReviewer, ClientServicingRep
    };
}
