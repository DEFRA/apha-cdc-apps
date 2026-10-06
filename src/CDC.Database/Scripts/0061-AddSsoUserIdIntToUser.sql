-- Entra ID (Microsoft identity platform) authenticates internal users via OIDC; the 'oid' claim is
-- a GUID unique per user per tenant (unlike 'sub', which is pairwise/per-application by default).
-- Named to match SsoUserIdExt's convention (SsoUserId + Int), kept as its own column since it is a
-- different identity space to both SsoUserIdExt and the legacy SsoUserId.
ALTER TABLE [dbo].[User]
ADD [SsoUserIdInt] UNIQUEIDENTIFIER NULL

GO

-- Lookups by SsoUserIdInt happen on every authenticated internal request, so this needs an index.
-- Filtered because the column is NULL for every external/legacy-SSO user.
CREATE UNIQUE NONCLUSTERED INDEX [IX_User_SsoUserIdInt]
ON [dbo].[User] ([SsoUserIdInt])
WHERE [SsoUserIdInt] IS NOT NULL
