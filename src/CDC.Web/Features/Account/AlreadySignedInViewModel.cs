namespace CDC.Web.Features.Account;

/// <summary>What the "already signed in" page needs to describe the session the user holds.</summary>
/// <param name="SessionName">The kind of session, "internal" or "external".</param>
/// <param name="HomeAction">The <c>LandingController</c> action that is the home page of that session.</param>
public sealed record AlreadySignedInViewModel(string SessionName, string HomeAction);
