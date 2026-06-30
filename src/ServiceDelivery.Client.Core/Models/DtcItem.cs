namespace ServiceDelivery.Client.Core.Models;

/// <summary>
/// A single Diagnostic Trouble Code entry from <c>GET /dtcs</c> (FE-015 AC-2). Mirrors the backend
/// <c>DtcDto(Guid Id, string Code, string Title, string RequiredEquipment)</c> contract. The submit form's
/// DTC dropdown binds <see cref="Id"/> (the selected <c>Guid</c> posted as <c>dtcId</c>) and renders
/// <see cref="Code"/> · <see cref="Title"/>. Lives in Core (no UI dependency) so the ViewModel can hold it.
/// </summary>
public record DtcItem(Guid Id, string Code, string Title);
