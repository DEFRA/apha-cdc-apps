-- CIDM (DEFRA Customer Identity) authenticates external users via OIDC; the 'sub' claim is a
-- GUID unique per external account. Named to match the existing SsoUserId column's convention
-- (Cidm + SsoId), kept as its own column rather than reusing SsoUserId because that one belongs
-- to the older WCF-based SSO system - a different identity space entirely.
ALTER TABLE [dbo].[User]
ADD [CidmSsoId] UNIQUEIDENTIFIER NULL

GO

-- Lookups by CidmSsoId happen on every authenticated external request, so this needs an index.
-- Filtered because the column is NULL for every internal/legacy-SSO user.
CREATE UNIQUE NONCLUSTERED INDEX [IX_User_CidmSsoId]
ON [dbo].[User] ([CidmSsoId])
WHERE [CidmSsoId] IS NOT NULL
