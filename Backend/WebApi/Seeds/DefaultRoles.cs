// WebApi/Seeds/DefaultRoles.cs
using System;
using System.Collections.Generic;

namespace WebApi.Seeds;

/// <summary>
/// Static catalog containing strongly-typed system roles defined for Identity access management.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Declares constant string literals for system roles (Administrator, Manager, User).</para>
/// <para><b>Business &amp; Technical Justification:</b> Centralizes the authorization roles to eliminate typos and establish strict compile-time checks in controller decorations.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Utilized by RoleSeeder during host startup, and referenced within [Authorize(Roles = ...)] claims validation attributes.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Seals global application role settings, preventing unauthenticated privilege escalation and ensuring correct RBAC checks.</para>
/// </remarks>
public static class DefaultRoles
{
    /// <summary>
    /// System Administrator with full global rights, bypassing tenant boundaries for administration.
    /// </summary>
    public const string Administrator = "Administrator";

    /// <summary>
    /// Manager role with intermediate access rights to oversee multiple workspaces.
    /// </summary>
    public const string Manager = "Manager";

    /// <summary>
    /// Standard User role with access limited to their specific workspace.
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// Gets all roles as a read-only list.
    /// </summary>
    public static readonly IReadOnlyList<string> AllRoles = new[] { Administrator, Manager, User };
}
