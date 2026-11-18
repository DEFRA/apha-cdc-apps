namespace CDC.Web.Models;

/// <summary>Data needed to render the shared <c>_ConfirmActionModal</c> partial: a GOV.UK-styled
/// confirmation dialog whose OK button submits a form to the given page handler. Matches the
/// legacy shared <c>ConfirmDialog.ascx</c> instance (<c>HeaderText="Confirm"</c>, used site-wide
/// for every confirmation), so unlike the legacy control this has no per-call header/OK text.</summary>
/// <param name="ModalId">The modal's element id, referenced by the trigger's <c>data-confirm-target</c>.</param>
/// <param name="BodyText">The confirmation question shown in the dialog body.</param>
/// <param name="PageHandler">The Razor Page handler the OK button's form posts to.</param>
/// <param name="ProfileId">The profile id to resubmit as a hidden field.</param>
/// <param name="SectionId">The profile section the page is showing, kept in the URL the dialog posts to so a page that shows itself again
/// after the action shows the same section; <see langword="null"/> for a page with no sections.</param>
public sealed record ConfirmActionModalViewModel(string ModalId, string BodyText, string PageHandler, Guid ProfileId, Guid? SectionId = null);
