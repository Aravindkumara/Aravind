using ServiceExcellence.Core.Common;
using ServiceExcellence.Core.Constants;
using ServiceExcellence.Core.Dtos;

namespace ServiceExcellence.Core.Interfaces;

public interface IUserRepository
{
    Task<UserCredentials?> GetCredentialsByUsernameAsync(string username);
    Task<UserCredentials?> GetCredentialsByIdAsync(int id);
    Task<PagedResult<UserDto>> SearchAsync(UserQuery query);
    Task<UserDto?> GetByIdAsync(int id);
    Task<bool> UsernameExistsAsync(string username, int? excludeId = null);
    Task<int> CreateAsync(UserSaveRequest request, string passwordHash);
    Task<bool> UpdateAsync(int id, UserSaveRequest request, string? passwordHash);
    Task UpdatePasswordAsync(int id, string passwordHash);
    Task SetLastLoginAsync(int id);
}

public interface IDealerRepository
{
    Task<PagedResult<DealerDto>> SearchAsync(PagedQuery query);
    Task<DealerDto?> GetByIdAsync(int id);
    Task<bool> CodeExistsAsync(string code, int? excludeId = null);
    Task<int> CreateAsync(DealerSaveRequest request);
    Task<bool> UpdateAsync(int id, DealerSaveRequest request);
    Task<IReadOnlyList<LookupItem>> LookupAsync();
}

/// <summary>Divisions, model categories, models and variants share one repository keyed by <see cref="MasterType"/>.</summary>
public interface IMasterRepository
{
    Task<PagedResult<MasterItemDto>> SearchAsync(MasterType type, MasterQuery query);
    Task<MasterItemDto?> GetByIdAsync(MasterType type, int id);
    Task<bool> CodeExistsAsync(MasterType type, string code, int? excludeId = null);
    Task<int> CreateAsync(MasterType type, MasterSaveRequest request);
    Task<bool> UpdateAsync(MasterType type, int id, MasterSaveRequest request);
    Task<IReadOnlyList<LookupItem>> LookupAsync(MasterType type, int? parentId);
    Task<bool> ExistsAsync(MasterType type, int id);

    /// <summary>Returns the full chain of ancestors (division down to the item itself) for a hierarchy item.</summary>
    Task<ClassificationPath?> ResolvePathAsync(MasterType type, int id);
}

public interface IAssemblyRepository
{
    Task<PagedResult<AssemblyDto>> SearchAsync(AssemblyQuery query);
    Task<AssemblyDto?> GetByIdAsync(int id);
    Task<bool> CodeExistsAsync(int variantId, string code, int? excludeId = null);
    Task<int> CreateAsync(AssemblySaveRequest request, short level);
    Task<bool> UpdateAsync(int id, AssemblySaveRequest request, short level);
    Task<bool> HasChildrenAsync(int id);

    /// <summary>All assemblies of a variant with their full path, ordered as a tree.</summary>
    Task<IReadOnlyList<AssemblyDto>> GetTreeAsync(int variantId, bool activeOnly);

    /// <summary>Returns the classification path (division to assembly) for an assembly.</summary>
    Task<ClassificationPath?> ResolvePathAsync(int assemblyId);
}

public interface ISopRepository
{
    Task<PagedResult<SopListItemDto>> SearchAsync(SopQuery query, bool publishedOnly);
    Task<PagedResult<SopListItemDto>> FindApplicableAsync(ClassificationPath path, SopFinderQuery query);
    Task<SopDetailDto?> GetByIdAsync(int id);
    Task<SopListItemDto?> GetSummaryAsync(int id);
    Task<bool> CodeExistsAsync(string sopCode, int? excludeId = null);
    Task<bool> HasOpenVersionAsync(string sopCode);
    Task<int> CreateAsync(SopSaveRequest request, ClassificationPath path, int userId);
    Task<bool> UpdateAsync(int id, SopSaveRequest request, ClassificationPath path, int userId);
    Task<bool> DeleteAsync(int id);

    /// <summary>Moves the SOP from <paramref name="fromStatuses"/> to <paramref name="toStatus"/>; false if it was not in one of them.</summary>
    Task<bool> ChangeStatusAsync(int id, string[] fromStatuses, string toStatus, int userId, string? remarks);

    /// <summary>Publishes an approved SOP and marks any previously published version of the same code obsolete.</summary>
    Task<bool> PublishAsync(int id, int userId);

    /// <summary>Copies a published SOP (with its steps, resources and attachments) into a new draft version.</summary>
    Task<int> CreateNewVersionAsync(int id, int userId);

    Task<SopStepDto?> GetStepAsync(int stepId);
    Task<int> AddStepAsync(int sopId, SopStepSaveRequest request);
    Task<bool> UpdateStepAsync(int stepId, SopStepSaveRequest request);
    Task<bool> DeleteStepAsync(int stepId);
    Task<bool> MoveStepAsync(int stepId, int direction);

    Task<SopResourceDto?> GetResourceAsync(int resourceId);
    Task<int> AddResourceAsync(int sopId, SopResourceSaveRequest request);
    Task<bool> DeleteResourceAsync(int resourceId);

    Task<AttachmentDto?> GetAttachmentAsync(int attachmentId);
    Task<int> AddAttachmentAsync(AttachmentDto attachment, int userId);
    Task<bool> DeleteAttachmentAsync(int attachmentId);
    Task<int> CountAttachmentReferencesAsync(string storedName);

    /// <summary>Marks the SOP as updated by the user (used when steps/resources change).</summary>
    Task TouchAsync(int sopId, int userId);

    Task<DashboardDto> GetDashboardAsync(bool publishedOnly);
}

public interface IAuditRepository
{
    Task LogAsync(string entityType, int entityId, string action, string? details, int? userId, string? username);
    Task<PagedResult<AuditLogDto>> SearchAsync(AuditQuery query);
}
