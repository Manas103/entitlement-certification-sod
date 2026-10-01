namespace Entitlement.Domain.Models;

/// <summary>
/// The attribute axes that scope a grant in the roles-plus-attributes (RBAC+ABAC)
/// model. A role grants a baseline set of permissions; the business unit and
/// instrument class on the grant itself further restrict where that permission
/// actually applies. Two grants of the same permission in different business
/// units are not the same entitlement.
/// </summary>
public enum BusinessUnit
{
    Equities,
    FixedIncome,
    ForeignExchange,
    Derivatives,
    ClientServicing
}

public enum InstrumentClass
{
    Equity,
    Bond,
    Fx,
    Derivative,
    MoneyMarket
}

/// <summary>
/// Permissions are deliberately named after the simulated investment-operations
/// application's actions, not generic CRUD verbs, so a segregation-of-duties rule
/// reads like the business rule it represents.
/// </summary>
public enum Permission
{
    ViewPosition,
    CreateTradeInstruction,
    ApproveTradeInstruction,
    CreateWireTransfer,
    ApproveWireTransfer,
    ModifySecurityMaster,
    ApproveSecurityMasterChange,
    ViewClientData,
    ReconcilePosition,
    ViewAuditLog
}

/// <summary>
/// Every mutation to the entitlement world is one of these events, appended to
/// the grant log. There is no "update" event; a changed grant is a Revoke of the
/// old fact plus a Grant of the new one.
/// </summary>
public enum GrantEventType
{
    UserOnboarded,
    RoleDefined,
    Grant,
    Revoke,
    UserTerminated,
    RoleDeleted
}

public enum FindingType
{
    SegregationOfDutiesConflict,
    OrphanedGrantTerminatedUser,
    OrphanedGrantDeletedRole
}

public enum CertificationDecisionType
{
    Approved,
    Revoked,
    Escalated
}
