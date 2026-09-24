using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.CrossProfileAdmin;
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

    [Fact]
    public async Task ReferenceData_OnGetAsync_OffersOnlyMaintainableTables()
    {
        var pageModel = new ReferenceDataModel(CreateService());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(6, pageModel.Tables.Count);
        Assert.Contains(pageModel.Tables, table => table.Name == "Control Mechanism");
        Assert.Contains(pageModel.Tables, table => table.Name == "Geographic Area");
        Assert.Null(pageModel.SelectedTable);
        Assert.Empty(pageModel.Values);
    }

    [Fact]
    public async Task ReferenceData_OnGetAsync_ShowsValuesForSelectedTableInSequenceOrder()
    {
        var pageModel = new ReferenceDataModel(CreateService()) { SelectedTableId = ControlMechanismId };

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.NotNull(pageModel.SelectedTable);
        Assert.Equal("Control Mechanism", pageModel.SelectedTable.Name);
        Assert.Equal(15, pageModel.Values.Count);
        Assert.Equal("Import controls", pageModel.Values[0].LookupValue);
        Assert.Equal("No practical control method available", pageModel.Values[^1].LookupValue);
        Assert.All(pageModel.Values, value => Assert.Equal("Active", value.Status));
    }

    [Fact]
    public async Task ReferenceData_OnGetAsync_ShowsNoValues_ForANonMaintainableTable()
    {
        // Impact Level - present in the legacy database but IsMaintainable = 0.
        var pageModel = new ReferenceDataModel(CreateService())
        {
            SelectedTableId = new Guid("118E00A5-C8FD-4978-86CC-E35F471E9514")
        };

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Null(pageModel.SelectedTable);
        Assert.Null(pageModel.SelectedTableId);
        Assert.Empty(pageModel.Values);
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

        var updated = await service.GetValueAsync(ControlMechanismId, existing.Id);
        Assert.Equal("Import controls (border)", updated!.LookupValue);
        Assert.Equal(DateOnly.FromDateTime(Now.UtcDateTime), updated.EffectiveDateFrom);
        Assert.True(updated.IsActive);

        var audit = Assert.Single(await service.GetAuditTrailAsync(ControlMechanismId));
        Assert.Equal("Import controls", audit.OldLookupValue);
        Assert.Equal("Import controls (border)", audit.NewLookupValue);
        Assert.Equal("Aligned with the 2026 border operating model", audit.Reason);
        Assert.Equal("Control Mechanism", audit.TableName);
        Assert.False(string.IsNullOrWhiteSpace(audit.UserFullName));
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
        Assert.Equal("Enter a reason for change", pageModel.ErrorFor(nameof(pageModel.Reason)));

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
    public async Task ReferenceDataAudit_OnGetAsync_ShowsChangesMostRecentFirst()
    {
        var timeProvider = new ControllableTimeProvider(Now);
        var service = new InMemoryReferenceDataService(timeProvider);
        var values = await service.GetValuesAsync(ControlMechanismId);

        await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, values[0].Id, "First change", "Reason one", "Profile Editor"));
        timeProvider.Advance(TimeSpan.FromHours(1));
        await service.UpdateValueAsync(new ReferenceDataUpdate(ControlMechanismId, values[1].Id, "Second change", "Reason two", "Profile Editor"));

        var pageModel = new ReferenceDataAuditModel(service) { TableId = ControlMechanismId };
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, pageModel.Entries.Count);
        Assert.Equal("Second change", pageModel.Entries[0].NewLookupValue);
        Assert.Equal("First change", pageModel.Entries[1].NewLookupValue);
        Assert.All(pageModel.Entries, entry => Assert.Equal("Profile Editor", entry.UserFullName));
    }

    [Fact]
    public async Task ReferenceDataAuditDetails_OnGetAsync_ReturnsNotFound_ForUnknownEntry()
    {
        var pageModel = new ReferenceDataAuditDetailsModel(CreateService()) { AuditId = Guid.NewGuid() };

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static InMemoryReferenceDataService CreateService() => new(new ControllableTimeProvider(Now));

    private static EditReferenceValueModel CreateEditPageModel(IReferenceDataService service, ReferenceDataValue existing)
    {
        var httpContext = new DefaultHttpContext();

        return new EditReferenceValueModel(service)
        {
            TableId = existing.ReferenceTableId,
            ValueId = existing.Id,
            PageContext = new PageContext
            {
                HttpContext = httpContext,
                ViewData = new ViewDataDictionary<object>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
        };
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
