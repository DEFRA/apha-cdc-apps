namespace CDC.Web.Models;

/// <summary>Data needed to render the shared <c>_ConfirmActionModal</c> partial: a GOV.UK-styled
/// confirmation dialog whose OK button submits a form to the given page handler.</summary>
/// <param name="ModalId">The modal's element id, referenced by the trigger's <c>data-confirm-target</c>.</param>
/// <param name="Title">The dialog heading.</param>
/// <param name="BodyText">The confirmation question shown in the dialog body.</param>
/// <param name="PageHandler">The Razor Page handler the OK button's form posts to.</param>
/// <param name="ProfileId">The profile id to resubmit as a hidden field.</param>
public sealed record ConfirmActionModalViewModel(string ModalId, string Title, string BodyText, string PageHandler, Guid ProfileId);
