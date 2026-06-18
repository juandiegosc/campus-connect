namespace Identity.Domain.Users;

/// <summary>
/// Defines the roles available in CampusConnect 360.
/// Exactly four values — no additional values in Phase 2 (ESC-11).
/// </summary>
public enum UserRole
{
    /// <summary>Administrative secretary role.</summary>
    Secretaria = 0,

    /// <summary>Finance department role.</summary>
    Finanzas = 1,

    /// <summary>Teacher role.</summary>
    Docente = 2,

    /// <summary>School director role.</summary>
    Direccion = 3
}
