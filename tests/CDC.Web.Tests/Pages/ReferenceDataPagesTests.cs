using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.CrossProfileAdmin;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace CDC.Web.Tests.Pages;

public class ReferenceDataPagesTests
{
    // Control Mechanism, from the legacy Profiles baseline data - IsMaintainable = 1.
    private static readonly Guid ControlMechanismId = new("D9889426-96A2-4C69-A3DB-C26F256C5FC1");

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly Microsoft.Extensions.Logging.ILogger<ReferenceDataModel> NullLogger =
        Microsoft.Extensions.Logging.Abstractions.NullLogger<ReferenceDataModel>.Instance;

    private static readonly Microsoft.Extensions.Logging.ILogger<ReferenceDataAuditModel> NullAuditLogger =
        Microsoft.Extensions.Logging.Abstractions.NullLogger<ReferenceDataAuditModel>.Instance;

    [Fact]
    public async Task ReferenceData_OnGetAsync_OffersOnlyMaintainableTables()
    {
        var pageModel = new ReferenceDataModel(CreateService(), NullLogger);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(
            [
                "Surveillance outcome", "Spread mechanism", "Transmission mechanism", "Surveillance mechanism",
                "Geographic area", "Laboratory", "Sample type", "Diagnosis and detection mechanism",
                "Control mechanism", "Contractor"
            ],
            pageModel.Tables.Select(table => table.Name));
        Assert.Null(pageModel.SelectedTable);
        Assert.Empty(pageModel.Values);
    }

    [Fact]
    public async Task ReferenceData_OnGetAsync_SortsByValueAscendingByDefault_AsLegacy()
    {
        var pageModel = new ReferenceDataModel(CreateService(), NullLogger) { SelectedTableId = ControlMechanismId };

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.NotNull(pageModel.SelectedTable);
        Assert.Equal("Control mechanism", pageModel.SelectedTable.Name);
        Assert.Equal(15, pageModel.Values.Count);
        Assert.Equal("Culling", pageModel.Values[0].LookupValue);
        Assert.Equal("Vector control", pageModel.Values[^1].LookupValue);
        Assert.All(pageModel.Values, value => Assert.Equal("1 January 2007 -", value.EffectiveDates));
        Assert.Equal("ascending", pageModel.GetAriaSort(ReferenceValueSortColumn.Value));
        Assert.Equal("none", pageModel.GetAriaSort(ReferenceValueSortColumn.EffectiveDates));
    }

    [Fact]
    public async Task ReferenceData_OnGetAsync_SortsByValueDescending()
    {
        var pageModel = new ReferenceDataModel(CreateService(), NullLogger)
        {
            SelectedTableId = ControlMechanismId,
            SortDirection = "Descending"
        };

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Vector control", pageModel.Values[0].LookupValue);
        Assert.Equal("descending", pageModel.GetAriaSort(ReferenceValueSortColumn.Value));
    }

    [Theory]
    [InlineData("Ascending", "Blood", "Other")]
    [InlineData("Descending", "Faeces", "Milk")]
    public async Task ReferenceData_OnGetAsync_SortsByEffectiveDate_ThenSequence(string direction, string expectedFirst, string expectedLast)
    {
        // Sample type: Blood and Milk from 26 November 2008, the rest from 2 March 2009.
        var pageModel = new ReferenceDataModel(CreateService(), NullLogger)
        {
            SelectedTableId = new Guid("4050839D-DBAF-4418-ADBA-82AE11BFED31"),
            SortColumn = ReferenceValueSortColumn.EffectiveDates,
            SortDirection = direction
        };

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedFirst, pageModel.Values[0].LookupValue);
        Assert.Equal(expectedLast, pageModel.Values[^1].LookupValue);
        Assert.Equal("26 November 2008 -", pageModel.Values.Single(value => value.LookupValue == "Blood").EffectiveDates);
    }

    [Theory]
    [InlineData(1, "10", 10, 2, "Culling")]
    [InlineData(2, "10", 5, 2, "Raise awareness for rapid detection")]
    [InlineData(9, "10", 5, 2, "Raise awareness for rapid detection")]
    [InlineData(1, "All", 15, 1, "Culling")]
    [InlineData(1, "rubbish", 10, 2, "Culling")]
    public async Task ReferenceData_OnGetAsync_PagesValues(int pageNumber, string pageSize, int expectedCount, int expectedPages, string expectedFirst)
    {
        var pageModel = new ReferenceDataModel(CreateService(), NullLogger)
        {
            SelectedTableId = ControlMechanismId,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedCount, pageModel.PagedValues.Count);
        Assert.Equal(expectedPages, pageModel.TotalPages);
        Assert.Equal(expectedFirst, pageModel.PagedValues[0].LookupValue);
    }

    [Fact]
    public async Task ReferenceData_OnGetAsync_ShowsNoValues_ForANonMaintainableTable()
    {
        // Impact Level - present in the legacy database but IsMaintainable = 0.
        var pageModel = new ReferenceDataModel(CreateService(), NullLogger)
        {
            SelectedTableId = new Guid("118E00A5-C8FD-4978-86CC-E35F471E9514")
        };

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Null(pageModel.SelectedTable);
        Assert.Null(pageModel.SelectedTableId);
        Assert.Empty(pageModel.Values);
    }

    [Fact]
    public async Task EditReferenceValue_OnGetAsync_ShowsTheExistingValueAsOldValue_WithAnEmptyNewValue()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Edit reference data value", pageModel.PanelHeading);
        Assert.True(pageModel.ShowPanel);
        Assert.Equal(existing.LookupValue, pageModel.OldValueText);
        Assert.Null(pageModel.NewLookupValue);
        Assert.Null(pageModel.FixedNewValue);
        Assert.True(pageModel.ShowInUseWarning);
        Assert.Equal("Save", pageModel.SubmitText);
    }

    [Fact]
    public void ReferenceData_RequiresTheReferenceDataPolicy()
    {
        var authorize = Assert.Single(typeof(ReferenceDataModel).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true));

        Assert.Equal(
            CDC.Web.Authorization.Constants.AuthorizationPolicies.CanManageReferenceData,
            ((Microsoft.AspNetCore.Authorization.AuthorizeAttribute)authorize).Policy);
    }

    [Fact]
    public async Task EditReferenceValue_Add_ShowsNewEntryPlaceholder_AndAddsTheValue()
    {
        var service = CreateService();
        var pageModel = CreateEditPageModel(service, ControlMechanismId, Guid.Empty, ReferenceDataChangeAction.Add);

        Assert.IsType<PageResult>(await pageModel.OnGetAsync(CancellationToken.None));
        Assert.Equal("Add reference data value", pageModel.PanelHeading);
        Assert.Equal("- new entry -", pageModel.OldValueText);
        Assert.False(pageModel.ShowInUseWarning);

        pageModel.NewLookupValue = "  Zoning  ";
        pageModel.Reason = "New control type";
        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Reference value change successfully saved", pageModel.ConfirmationMessage);

        var values = await service.GetValuesAsync(ControlMechanismId);
        var added = values[^1];
        Assert.Equal(16, values.Count);
        Assert.Equal("Zoning", added.LookupValue);
        Assert.Equal((byte)16, added.SequenceNumber);
        Assert.True(added.IsActive);
        Assert.False(added.IsInUse);
        Assert.Equal("1 January 2026 -", added.EffectiveDates);

        var audit = Assert.Single(await service.GetAuditTrailAsync(ControlMechanismId));
        Assert.Equal("- new entry -", audit.OldLookupValue);
        Assert.Equal("Zoning", audit.NewLookupValue);
    }

    [Fact]
    public async Task EditReferenceValue_Add_ShowsBothValidationErrors_WhenNothingEntered()
    {
        var service = CreateService();
        var pageModel = CreateEditPageModel(service, ControlMechanismId, Guid.Empty, ReferenceDataChangeAction.Add);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("You need to provide a new value", pageModel.ErrorFor(nameof(pageModel.NewLookupValue)));
        Assert.Equal("You need to provide a reason for this change", pageModel.ErrorFor(nameof(pageModel.Reason)));
        Assert.Equal(15, (await service.GetValuesAsync(ControlMechanismId)).Count);
    }

    [Fact]
    public async Task EditReferenceValue_Inactivate_EndDatesTheValue_AndRecordsAuditEntry()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing.ReferenceTableId, existing.Id, ReferenceDataChangeAction.Inactivate);

        Assert.IsType<PageResult>(await pageModel.OnGetAsync(CancellationToken.None));
        Assert.Equal("- to be inactivated -", pageModel.FixedNewValue);
        Assert.True(pageModel.ShowInUseWarning);
        Assert.Equal("Inactivate", pageModel.SubmitText);

        pageModel.Reason = "No longer used";
        Assert.IsType<RedirectToPageResult>(await pageModel.OnPostAsync(CancellationToken.None));

        var inactivated = await service.GetValueAsync(ControlMechanismId, existing.Id);
        Assert.False(inactivated!.IsActive);
        Assert.Equal("Inactive", inactivated.Status);
        Assert.Equal("1 January 2007 - 1 January 2026", inactivated.EffectiveDates);
        Assert.Equal($"{existing.LookupValue} (Inactive)", inactivated.DisplayValue);

        var audit = Assert.Single(await service.GetAuditTrailAsync(ControlMechanismId));
        Assert.Equal(existing.LookupValue, audit.OldLookupValue);
        Assert.Equal("- to be inactivated -", audit.NewLookupValue);
    }

    [Fact]
    public async Task EditReferenceValue_Inactivate_PreventsSave_WhenNoReasonForChange()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing.ReferenceTableId, existing.Id, ReferenceDataChangeAction.Inactivate);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(pageModel.ErrorFor(nameof(pageModel.NewLookupValue)));
        Assert.Equal("You need to provide a reason for this change", pageModel.ErrorFor(nameof(pageModel.Reason)));
        Assert.True((await service.GetValueAsync(ControlMechanismId, existing.Id))!.IsActive);
    }

    [Fact]
    public async Task EditReferenceValue_ReturnsNotFound_ForAnInactiveValue()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, existing.Id, string.Empty, "Retired", "Profile Editor", ReferenceDataChangeAction.Inactivate));
        var pageModel = CreateEditPageModel(service, existing.ReferenceTableId, existing.Id, ReferenceDataChangeAction.Edit);

        Assert.IsType<NotFoundResult>(await pageModel.OnGetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EditReferenceValue_Delete_RemovesAValueNotInUse_AndRecordsAuditEntry()
    {
        var service = CreateService();
        var valueId = await AddZoningAsync(service);
        var pageModel = CreateEditPageModel(service, ControlMechanismId, valueId, ReferenceDataChangeAction.Delete);

        Assert.IsType<PageResult>(await pageModel.OnGetAsync(CancellationToken.None));
        Assert.Equal("- to be deleted -", pageModel.FixedNewValue);
        Assert.False(pageModel.ShowInUseWarning);
        Assert.Equal("Delete", pageModel.SubmitText);

        pageModel.Reason = "Added in error";
        Assert.IsType<RedirectToPageResult>(await pageModel.OnPostAsync(CancellationToken.None));

        Assert.Null(await service.GetValueAsync(ControlMechanismId, valueId));
        var audit = Assert.Single(await service.GetAuditTrailAsync(ControlMechanismId), entry => entry.NewLookupValue == "- to be deleted -");
        Assert.Equal("Zoning", audit.OldLookupValue);
        Assert.Equal("Added in error", audit.Reason);
    }

    [Fact]
    public async Task EditReferenceValue_Delete_PreventsDeletion_WhenNoReasonForChange()
    {
        var service = CreateService();
        var valueId = await AddZoningAsync(service);
        var pageModel = CreateEditPageModel(service, ControlMechanismId, valueId, ReferenceDataChangeAction.Delete);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("You need to provide a reason for this change", pageModel.ErrorFor(nameof(pageModel.Reason)));
        Assert.NotNull(await service.GetValueAsync(ControlMechanismId, valueId));
    }

    [Fact]
    public async Task EditReferenceValue_Delete_ReturnsNotFound_ForAValueInUse()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing.ReferenceTableId, existing.Id, ReferenceDataChangeAction.Delete);

        Assert.IsType<NotFoundResult>(await pageModel.OnGetAsync(CancellationToken.None));

        var direct = await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, existing.Id, string.Empty, "Reason", "Profile Editor", ReferenceDataChangeAction.Delete));
        Assert.Equal(ReferenceDataUpdateOutcome.ValueInUse, direct.Outcome);
    }

    [Fact]
    public async Task EditReferenceValue_OnGetAsync_ReturnsNotFound_WhenValueDoesNotExist()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing);
        pageModel.ValueId = Guid.NewGuid();

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditReferenceValue_OnGetAsync_ReturnsNotFound_WhenTableDoesNotExist()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing);
        pageModel.SelectedTableId = Guid.NewGuid();

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditReferenceValue_OnPostAsync_UpdatesValue_AndRecordsAuditEntry()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing);
        pageModel.NewLookupValue = "Import controls (border)";
        pageModel.Reason = "Aligned with the 2026 border operating model";

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/CrossProfileAdmin/ReferenceData", redirect.PageName);
        Assert.Equal("Reference value change successfully saved", pageModel.ConfirmationMessage);

        var updated = await service.GetValueAsync(ControlMechanismId, existing.Id);
        Assert.Equal("Import controls (border)", updated!.LookupValue);
        Assert.Equal(new DateOnly(2007, 1, 1), updated.EffectiveDateFrom);
        Assert.True(updated.IsActive);

        var audit = Assert.Single(await service.GetAuditTrailAsync(ControlMechanismId));
        Assert.Equal("Import controls", audit.OldLookupValue);
        Assert.Equal("Import controls (border)", audit.NewLookupValue);
        Assert.Equal("Aligned with the 2026 border operating model", audit.Reason);
        Assert.Equal("Control mechanism", audit.TableName);
        Assert.False(string.IsNullOrWhiteSpace(audit.UserFullName));
    }

    [Fact]
    public async Task EditReferenceValue_OnPostAsync_ReturnsNotFound_WhenValueNoLongerExists()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing);
        pageModel.ValueId = Guid.NewGuid();
        pageModel.NewLookupValue = "New value";
        pageModel.Reason = "Valid reason";

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditReferenceValue_OnPostAsync_PreventsSave_WhenNoReasonForChange()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing);
        pageModel.NewLookupValue = "Import controls (border)";
        pageModel.Reason = "   ";

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("You need to provide a reason for this change", pageModel.ErrorFor(nameof(pageModel.Reason)));

        var unchanged = await service.GetValueAsync(ControlMechanismId, existing.Id);
        Assert.Equal(existing.LookupValue, unchanged!.LookupValue);
        Assert.Empty(await service.GetAuditTrailAsync(ControlMechanismId));
    }

    [Fact]
    public async Task EditReferenceValue_OnPostAsync_PreventsSave_WhenValueAlreadyExistsInTheTable()
    {
        var service = CreateService();
        var values = await service.GetValuesAsync(ControlMechanismId);
        var pageModel = CreateEditPageModel(service, values[0]);
        pageModel.NewLookupValue = values[1].LookupValue;
        pageModel.Reason = "Merging two control types";

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("There is already a reference value with this name", pageModel.ErrorFor(nameof(pageModel.NewLookupValue)));
        Assert.Empty(await service.GetAuditTrailAsync(ControlMechanismId));
    }

    [Fact]
    public async Task EditReferenceValue_OnPostAsync_RejectsBlankValuesAndOverlongReasons()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing);
        pageModel.NewLookupValue = string.Empty;
        pageModel.Reason = new string('x', ReferenceDataModel.ReasonMaxLength + 1);

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("You need to provide a new value", pageModel.ErrorFor(nameof(pageModel.NewLookupValue)));
        Assert.Equal($"Reason for change must be {ReferenceDataModel.ReasonMaxLength} characters or fewer", pageModel.ErrorFor(nameof(pageModel.Reason)));
        Assert.True(pageModel.HasErrors);
        Assert.True(pageModel.ShowPanel);
    }

    [Fact]
    public async Task EditReferenceValue_OnPostAsync_KeepsOnlyItalicsInTheNewValue_AndTreatsEmptyMarkupAsBlank()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var pageModel = CreateEditPageModel(service, existing);
        pageModel.NewLookupValue = "<em></em>";
        pageModel.Reason = "<p></p>";

        Assert.IsType<PageResult>(await pageModel.OnPostAsync(CancellationToken.None));
        Assert.Equal("You need to provide a new value", pageModel.ErrorFor(nameof(pageModel.NewLookupValue)));
        Assert.Equal("You need to provide a reason for this change", pageModel.ErrorFor(nameof(pageModel.Reason)));

        var retry = CreateEditPageModel(service, existing);
        retry.NewLookupValue = "<p><em>Import</em> <b>controls</b></p>";
        retry.Reason = "<p>See <a href=\"https://www.gov.uk\">GOV.UK</a></p>";

        Assert.IsType<RedirectToPageResult>(await retry.OnPostAsync(CancellationToken.None));
        Assert.Equal("<em>Import</em> controls", (await service.GetValueAsync(ControlMechanismId, existing.Id))!.LookupValue);
        Assert.Contains("<a href=", Assert.Single(await service.GetAuditTrailAsync(ControlMechanismId)).Reason);
    }

    [Fact]
    public async Task ReferenceData_OnGetAsync_ShowsNoChangeSection_UntilAValueIsChosen()
    {
        var pageModel = new ReferenceDataModel(CreateService(), NullLogger) { SelectedTableId = ControlMechanismId };

        Assert.IsType<PageResult>(await pageModel.OnGetAsync(CancellationToken.None));

        Assert.False(pageModel.ShowPanel);
        Assert.False(pageModel.HasErrors);
    }

    [Fact]
    public async Task ReferenceDataAudit_OnGetAsync_ShowsChangesMostRecentFirst()
    {
        var timeProvider = new ControllableTimeProvider(Now);
        var service = new InMemoryReferenceDataService(timeProvider);
        var values = await service.GetValuesAsync(ControlMechanismId);

        await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, values[0].Id, "First change", "Reason one", "Profile Editor"));
        timeProvider.Advance(TimeSpan.FromHours(1));
        await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, values[1].Id, "Second change", "Reason two", "Profile Editor"));

        var pageModel = new ReferenceDataAuditModel(service, NullAuditLogger) { TableId = ControlMechanismId };
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, pageModel.Entries.Count);
        Assert.Equal("Second change", pageModel.Entries[0].NewLookupValue);
        Assert.Equal("First change", pageModel.Entries[1].NewLookupValue);
        Assert.All(pageModel.Entries, entry => Assert.Equal("Profile Editor", entry.UserFullName));
    }

    [Fact]
    public async Task ReferenceDataAudit_OnGetAsync_PagesEntries()
    {
        var service = CreateService();
        var values = await service.GetValuesAsync(ControlMechanismId);

        for (var i = 0; i < 12; i++)
        {
            await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, values[i].Id, $"Changed {i}", "Reason", "Profile Editor"));
        }

        var pageModel = new ReferenceDataAuditModel(service, NullAuditLogger) { TableId = ControlMechanismId, PageNumber = 2, PageSize = "10" };
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(12, pageModel.Entries.Count);
        Assert.Equal(2, pageModel.TotalPages);
        Assert.Equal(2, pageModel.PagedEntries.Count);
    }

    [Fact]
    public async Task ReferenceDataAuditDetails_OnGetAsync_ShowsTheEntry()
    {
        var service = CreateService();
        var value = (await service.GetValuesAsync(ControlMechanismId))[0];
        var result = await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, value.Id, "Changed", "Because", "Profile Editor"));

        var pageModel = new ReferenceDataAuditDetailsModel(service) { TableId = ControlMechanismId, AuditId = result.AuditEntry!.Id };

        Assert.IsType<PageResult>(await pageModel.OnGetAsync(CancellationToken.None));
        Assert.Equal("Because", pageModel.Entry!.Reason);
    }

    [Fact]
    public async Task ReferenceDataAuditDetails_OnGetAsync_ReturnsNotFound_ForUnknownEntry()
    {
        var pageModel = new ReferenceDataAuditDetailsModel(CreateService()) { TableId = ControlMechanismId, AuditId = Guid.NewGuid() };

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ReferenceDataAudit_OnGetAsync_ShowsNothing_WithoutATable()
    {
        var service = CreateService();
        var value = (await service.GetValuesAsync(ControlMechanismId))[0];
        await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, value.Id, "Changed", "Because", "Profile Editor"));

        var pageModel = new ReferenceDataAuditModel(service, NullAuditLogger);
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Null(pageModel.Table);
        Assert.Empty(pageModel.Entries);
    }

    [Fact]
    public async Task EditReferenceValue_OnPostAsync_ReportsAConflict_WhenAnotherUserChangedTheValueFirst()
    {
        var service = CreateService();
        var existing = (await service.GetValuesAsync(ControlMechanismId))[0];
        var opened = CreateEditPageModel(service, existing);
        Assert.IsType<PageResult>(await opened.OnGetAsync(CancellationToken.None));
        var rowVersionWhenOpened = opened.RowVersion;

        await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, existing.Id, "Changed elsewhere", "Someone else", "Other editor", LastUpdated: existing.LastUpdated));

        var pageModel = CreateEditPageModel(service, existing);
        pageModel.LastUpdated = rowVersionWhenOpened;
        pageModel.NewLookupValue = "My change";
        pageModel.Reason = "Mine";

        Assert.IsType<PageResult>(await pageModel.OnPostAsync(CancellationToken.None));
        Assert.StartsWith("Another user has changed this reference value", pageModel.GeneralError);
        Assert.True(pageModel.HasErrors);
        Assert.Equal("Changed elsewhere", (await service.GetValueAsync(ControlMechanismId, existing.Id))!.LookupValue);
    }

    [Fact]
    public async Task ReferenceDataPages_ShowAMessage_WhenCdcApiCannotBeReached()
    {
        var listPage = CreateEditPageModel(new UnreachableReferenceDataApiService(), ControlMechanismId, Guid.NewGuid(), ReferenceDataChangeAction.Edit);
        Assert.IsType<PageResult>(await listPage.OnGetAsync(CancellationToken.None));
        Assert.NotNull(listPage.LoadErrorMessage);
        Assert.False(listPage.ShowPanel);

        var savePage = CreateEditPageModel(new UnreachableReferenceDataApiService(), ControlMechanismId, Guid.NewGuid(), ReferenceDataChangeAction.Edit);
        Assert.IsType<PageResult>(await savePage.OnPostAsync(CancellationToken.None));
        Assert.NotNull(savePage.LoadErrorMessage);

        var auditPage = new ReferenceDataAuditModel(new UnreachableReferenceDataApiService(), NullAuditLogger) { TableId = ControlMechanismId };
        await auditPage.OnGetAsync(CancellationToken.None);
        Assert.NotNull(auditPage.LoadErrorMessage);
        Assert.Empty(auditPage.Entries);
    }

    private static async Task<Guid> AddZoningAsync(InMemoryReferenceDataService service)
    {
        await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, Guid.Empty, "Zoning", "New", "Profile Editor", ReferenceDataChangeAction.Add));

        return (await service.GetValuesAsync(ControlMechanismId)).Single(value => value.LookupValue == "Zoning").Id;
    }

    private static InMemoryReferenceDataService CreateService() => new(new ControllableTimeProvider(Now));

    private static ReferenceDataModel CreateEditPageModel(IReferenceDataApiService service, ReferenceDataValue existing) =>
        CreateEditPageModel(service, existing.ReferenceTableId, existing.Id, ReferenceDataChangeAction.Edit);

    private static ReferenceDataModel CreateEditPageModel(
        IReferenceDataApiService service, Guid tableId, Guid valueId, ReferenceDataChangeAction mode)
    {
        var httpContext = new DefaultHttpContext();

        return new ReferenceDataModel(service, NullLogger)
        {
            SelectedTableId = tableId,
            ValueId = valueId,
            Mode = mode,
            PageContext = new PageContext
            {
                HttpContext = httpContext,
                ViewData = new ViewDataDictionary<object>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
        };
    }

    private sealed class UnreachableReferenceDataApiService : IReferenceDataApiService
    {
        public Task<IReadOnlyList<ReferenceTableSummary>> GetTablesAsync(CancellationToken cancellationToken = default) => Fail<IReadOnlyList<ReferenceTableSummary>>();

        public Task<ReferenceTableSummary?> GetTableAsync(Guid referenceTableId, CancellationToken cancellationToken = default) => Fail<ReferenceTableSummary?>();

        public Task<IReadOnlyList<ReferenceDataValue>> GetValuesAsync(Guid referenceTableId, CancellationToken cancellationToken = default) => Fail<IReadOnlyList<ReferenceDataValue>>();

        public Task<ReferenceDataValue?> GetValueAsync(Guid referenceTableId, Guid valueId, CancellationToken cancellationToken = default) => Fail<ReferenceDataValue?>();

        public Task<ReferenceDataUpdateResult> UpdateValueAsync(ReferenceDataUpdate update, CancellationToken cancellationToken = default) => Fail<ReferenceDataUpdateResult>();

        public Task<IReadOnlyList<ReferenceDataAuditEntry>> GetAuditTrailAsync(Guid referenceTableId, CancellationToken cancellationToken = default) => Fail<IReadOnlyList<ReferenceDataAuditEntry>>();

        public Task<ReferenceDataAuditEntry?> GetAuditEntryAsync(Guid referenceTableId, Guid auditId, CancellationToken cancellationToken = default) => Fail<ReferenceDataAuditEntry?>();

        private static Task<T> Fail<T>() => Task.FromException<T>(new HttpRequestException("CDC.Api is unavailable"));
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class ControllableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }
}
