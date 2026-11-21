using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

internal sealed class InstitutionalFile
{
	public long Id { get; set; }

	public string Area { get; set; } = "files";

	public string Channel { get; set; } = "requests";

	public string Title { get; set; } = "";

	public string Description { get; set; } = "";

	public string OriginalFileName { get; set; } = "";

	public string ContentType { get; set; } = "application/octet-stream";

	public int Size { get; set; }

	public string OwnerUserName { get; set; } = "";

	public string OwnerDisplayName { get; set; } = "";

	public string OwnerLogin { get; set; } = "";

	public string Department { get; set; } = "";

	public Guid? FolderId { get; set; }

	[JsonIgnore]
	public bool FolderReadAllowed { get; set; }

	[JsonIgnore]
	public bool FolderWriteAllowed { get; set; }

	public bool CanManageFiles
	{
		get
		{
			if (Area == "files" && FolderId.HasValue)
			{
				return FolderWriteAllowed;
			}
			return false;
		}
	}

	public string? RecipientUserName { get; set; }

	public string? RecipientDepartment { get; set; }

	public string Status { get; set; } = "active";

	public bool Trashed { get; set; }

	public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

	public DateTime? SentUtc { get; set; }

	public DateTime? ReceivedUtc { get; set; }

	public string? ReceivedByUserName { get; set; }

	public string? ReceiverDisplayName { get; set; }

	public string? ReceiverLogin { get; set; }

	public string? ReceiptCode { get; set; }

	public string? VerificationToken { get; set; }

	public long? TemplateId { get; set; }

	public long? ArchivedFromDocumentId { get; set; }

	public double SealX { get; set; } = 73.0;

	public double SealY { get; set; } = 71.0;

	public string? ReceiptSignature { get; set; }

	public string? CorrectionNote { get; set; }

	public string? DecisionBy { get; set; }

	public DateTime? DecisionUtc { get; set; }

	public WarehouseRequestInfo? WarehouseRequest { get; set; }

	public List<WarehouseAssignedEquipment> WarehouseAssigned { get; set; } = new List<WarehouseAssignedEquipment>();

	public List<int> WarehousePendingPurchaseLines { get; set; } = new List<int>();

	public List<WarehouseAssignedEquipment> WarehouseAssignmentDraft { get; set; } = new List<WarehouseAssignedEquipment>();

	public string? WarehousePurchaseNote { get; set; }

	public string? WarehousePreparationNote { get; set; }

	public string? WarehousePreparedBy { get; set; }

	public string? WarehousePreparedLogin { get; set; }

	public DateTime? WarehousePreparedUtc { get; set; }

	public WarehouseDeliveryInfo? WarehouseDelivery { get; set; }

	public int WarehouseRevision { get; set; }

	public bool CanEditWarehouseRequest { get; set; }

	[JsonIgnore]
	public bool TeamWarehouseReaderAllowed { get; set; }

	public WeekendLoanSettlement? WeekendLoanSettlement { get; set; }

	public WarehouseInstallationInfo? WarehouseInstallation { get; set; }

	public AdministrativeRequestInfo? AdministrativeRequest { get; set; }

	public bool AdministrativeRegistrationOnly
	{
		get
		{
			if (AdministrativeRequest?.FormType == "FUEL_LOG")
			{
				return !AdministrativeFuel.IsSulay(OwnerLogin);
			}
			return false;
		}
	}

	public int AdministrativeRevision { get; set; }

	public bool CanEditAdministrativeForm { get; set; }

	public bool CanDecideIncidentTickets { get; set; }

	public AdministrativeDeliveryInfo? AdministrativeDelivery { get; set; }

	public List<DocumentAccess> SharedWith { get; set; } = new List<DocumentAccess>();

	public List<InstitutionalEvent> History { get; set; } = new List<InstitutionalEvent>();

	public bool IsOwner { get; set; }

	public bool IsRecipient { get; set; }

	public bool CanEdit { get; set; }

	public bool CanMove { get; set; }

	public bool CanAuthorizeWarehouseRequest { get; set; }

	public string FileUrl => $"/api/institutional-documents/{Id}/preview";

	private static bool SameUser(string? left, string? right)
	{
		return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	public bool AwaitingWarehouseAuthorization()
	{
		if (Channel == "requirements" && RecipientDepartment == "Almacén" && (object)WarehouseDelivery == null)
		{
			if (!(Status == "received"))
			{
				if (Status == "correction_requested")
				{
					return ReceivedUtc.HasValue;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public void NormalizeWarehouseReceiptState()
	{
		if (Area == "communications" && Channel == "requirements" && RecipientDepartment == "Almacén" && Status == "pending" && ReceivedUtc.HasValue && (object)WarehouseDelivery == null)
		{
			Status = "received";
		}
	}

	public bool Recipient(DocumentActor a)
	{
		if (AdministrativeRequest?.FormType == "INCIDENT_REPORT" && AdministrativeProfiles.CanDecideIncidentTickets(a))
		{
			return true;
		}
		if ((RecipientUserName != null) ? SameUser(RecipientUserName, a.Id) : string.Equals(RecipientDepartment, a.Department, StringComparison.OrdinalIgnoreCase))
		{
			if (SameUser(OwnerUserName, a.Id))
			{
				return (object)AdministrativeRequest != null;
			}
			return true;
		}
		return false;
	}

	public bool Shared(DocumentActor a)
	{
		return SharedWith.Any((DocumentAccess s) => (s.RecipientUserName == null) ? string.Equals(s.RecipientDepartment, a.Department, StringComparison.OrdinalIgnoreCase) : SameUser(s.RecipientUserName, a.Id));
	}

	public bool DepartmentWarehouseReader(DocumentActor a)
	{
		if (!Trashed && (object)WarehouseRequest != null && Area == "communications" && Channel == "requirements" && RecipientDepartment == "Almacén" && SentUtc.HasValue && Status != "draft" && Department == a.Department)
		{
			if (!(a.Role != "GroupAdministrator") || (a.Role == "GeneralServices" && a.Team == "TECHNICIANS"))
			{
				return OwnerUserName == a.Id;
			}
			return true;
		}
		return false;
	}

	public bool Accessible(DocumentActor a)
	{
		AdministrativeFuel.Normalize(this);
		if (a.Allowed && (Area != "files" || !FolderId.HasValue || FolderReadAllowed) && (!a.WarehouseRequestOnly || (SameUser(OwnerUserName, a.Id) && (object)WarehouseRequest != null && RecipientDepartment == "Almacén")) && ((object)AdministrativeRequest == null || AdministrativeProfiles.CanRead(a, AdministrativeRequest)) && (!a.WarehouseRequestsBlocked || ((object)WarehouseRequest == null && (!(Area == "communications") || !(Channel == "requirements") || !(RecipientDepartment == "Almacén")))))
		{
			if ((object)AdministrativeRequest == null || !(RecipientDepartment == "Administración"))
			{
				if (!Trashed)
				{
					if (!a.Manager && !SameUser(OwnerUserName, a.Id) && !Recipient(a) && !Shared(a) && !DepartmentWarehouseReader(a) && !TeamWarehouseReaderAllowed)
					{
						if (Area == "files")
						{
							if (!string.Equals(Department, a.Department, StringComparison.OrdinalIgnoreCase))
							{
								return FolderReadAllowed;
							}
							return true;
						}
						return false;
					}
					return true;
				}
				if (!SameUser(OwnerUserName, a.Id))
				{
					return CanManageFiles;
				}
				return true;
			}
			if (!(a.Department == "Administración"))
			{
				return SameUser(OwnerUserName, a.Id) || (AdministrativeRequest?.FormType == "INCIDENT_REPORT" && AdministrativeProfiles.CanReadIncidentReports(a));
			}
			return true;
		}
		return false;
	}

	public InstitutionalFile For(DocumentActor a)
	{
		AdministrativeFuel.Normalize(this);
		CanEditAdministrativeForm = AdministrativeFormEditing.CanEdit(this, a);
		CanDecideIncidentTickets = AdministrativeRequest?.FormType == "INCIDENT_REPORT" && AdministrativeProfiles.CanDecideIncidentTickets(a);
		CanEditWarehouseRequest = WarehouseRequestEditing.CanEdit(this, a);
		NormalizeWarehouseReceiptState();
		CanAuthorizeWarehouseRequest = a.Manager && !a.WarehouseRequestOnly && a.WarehouseAuthorize && !Trashed && Channel == "requirements" && RecipientDepartment == "Almacén" && AwaitingWarehouseAuthorization();
		IsOwner = SameUser(OwnerUserName, a.Id);
		IsRecipient = Recipient(a) || (a.Manager && RecipientDepartment == "Almacén");
		CanMove = SameUser(ReceivedByUserName, a.Id);
		CanEdit = !Trashed && !ReceivedUtc.HasValue && (IsOwner || SharedWith.Any((DocumentAccess s) => s.CanEdit && ((s.RecipientUserName == null) ? string.Equals(s.RecipientDepartment, a.Department, StringComparison.OrdinalIgnoreCase) : SameUser(s.RecipientUserName, a.Id))));
		string role = a.Role;
		bool flag = ((role == "Administrator" || role == "Warehouse") ? true : false);
		bool flag2 = flag || a.Team == "WAREHOUSE";
		if ((object)WarehouseRequest != null && !flag2 && Status != "approved")
		{
			WarehouseAssigned = new List<WarehouseAssignedEquipment>();
			WarehousePreparationNote = null;
			WarehousePreparedBy = null;
			WarehousePreparedLogin = null;
			WarehousePreparedUtc = null;
		}
		if (Area == "files" && FolderId.HasValue)
		{
			CanEdit = !Trashed && FolderWriteAllowed;
			CanMove = FolderWriteAllowed;
		}
		return this;
	}
}
